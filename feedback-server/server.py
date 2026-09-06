"""Dependency-free feedback service. Public submission and SSH-only admin use separate ports."""
import csv
from contextlib import contextmanager
import hashlib
import html
import io
import json
import os
import secrets
import sqlite3
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

KINDS = ('使用问题', '功能建议', '界面体验', '其他')
STATES = ('待处理', '处理中', '已处理')


class Store:
    def __init__(self, root):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        self.path = self.root / 'feedback.sqlite3'
        salt = self.root / 'ip-salt'
        if not salt.exists():
            with salt.open('xb') as f:
                f.write(secrets.token_bytes(32))
            salt.chmod(0o600)
        self.salt = salt.read_bytes()
        with self.connect() as db:
            db.execute('PRAGMA journal_mode=WAL')
            db.executescript('''
              CREATE TABLE IF NOT EXISTS feedback (
                id TEXT PRIMARY KEY, payload_hash TEXT NOT NULL, created TEXT NOT NULL,
                kind TEXT NOT NULL, title TEXT NOT NULL, detail TEXT NOT NULL,
                contact TEXT NOT NULL, version TEXT NOT NULL, status TEXT NOT NULL);
              CREATE TABLE IF NOT EXISTS limits (bucket TEXT PRIMARY KEY, count INTEGER NOT NULL);
              PRAGMA user_version=1;
            ''')

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.path, timeout=10)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    def submit(self, data, ip, now=None):
        now = time.time() if now is None else now
        if not isinstance(data, dict):
            raise ValueError('invalid object')
        try:
            receipt = str(uuid.UUID(data['id']))
        except (ValueError, KeyError, TypeError, AttributeError):
            raise ValueError('invalid id')
        clean = {}
        for key, limit in [('kind', 20), ('title', 120), ('detail', 10000), ('contact', 200), ('version', 64)]:
            value = data.get(key, '')
            if not isinstance(value, str) or len(value) > limit or '\x00' in value:
                raise ValueError('invalid field')
            clean[key] = value.strip()
        if clean['kind'] not in KINDS or not clean['title'] or not clean['detail']:
            raise ValueError('required field')
        digest = hashlib.sha256(json.dumps(clean, sort_keys=True).encode()).hexdigest()
        peer = hashlib.sha256(self.salt + ip.encode()).hexdigest()
        hour, day = int(now // 3600), int(now // 86400)
        buckets = [(f'h:{hour}:{peer}', 5), (f'd:{day}:all', 1000)]
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            old = db.execute('SELECT payload_hash FROM feedback WHERE id=?', (receipt,)).fetchone()
            if old:
                if old['payload_hash'] != digest:
                    return 409, {'error': 'id_conflict'}
                return 200, {'id': receipt, 'accepted': True}
            for key, limit in buckets:
                row = db.execute('SELECT count FROM limits WHERE bucket=?', (key,)).fetchone()
                if row and row[0] >= limit:
                    return 429, {'error': 'rate_limited'}
            for key, _ in buckets:
                db.execute('INSERT INTO limits VALUES (?,1) ON CONFLICT(bucket) DO UPDATE SET count=count+1', (key,))
            db.execute("DELETE FROM limits WHERE (bucket LIKE 'h:%' AND bucket NOT LIKE ?) OR (bucket LIKE 'd:%' AND bucket NOT LIKE ?)", (f'h:{hour}:%', f'd:{day}:%'))
            db.execute('INSERT INTO feedback VALUES (?,?,?,?,?,?,?,?,?)',
                       (receipt, digest, time.strftime('%Y-%m-%d %H:%M:%S UTC', time.gmtime(now)),
                        clean['kind'], clean['title'], clean['detail'], clean['contact'], clean['version'], '待处理'))
        return 201, {'id': receipt, 'accepted': True}

    def records(self):
        with self.connect() as db:
            return [dict(r) for r in db.execute('SELECT id,created,kind,title,detail,contact,version,status FROM feedback ORDER BY created DESC LIMIT 1000')]


def admin_page(records):
    rows = []
    for r in records:
        value = lambda k: html.escape(r[k], quote=True)
        options = ''.join(f'<option {"selected" if r["status"] == s else ""}>{s}</option>' for s in STATES)
        rows.append(f'<article><div class="meta">{value("created")} · {value("kind")} · v{value("version")}</div>'
                    f'<h2>{value("title")}</h2><pre>{value("detail")}</pre><p>联系方式：{value("contact") or "未填写"}</p>'
                    f'<small>编号：{value("id")}</small><form method="post" action="/status">'
                    f'<input type="hidden" name="id" value="{value("id")}"><select name="status">{options}</select><button>更新状态</button></form></article>')
    return ('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width">'
            '<title>邮件接收管理 · 反馈后台</title><style>body{font:16px system-ui;background:#f4f6fa;color:#243247;max-width:960px;margin:40px auto;padding:0 20px}'
            'article{background:white;padding:24px;margin:18px 0;border-radius:10px}h1{font-size:25px}h2{font-size:19px}.meta,small{color:#64748b}'
            'pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}select,button{padding:9px;margin:12px 8px 0 0}a{color:#1677ff}</style>'
            '<h1>用户反馈</h1><p>通过 SSH 私密访问 · 最近 1000 条 · <a href="/">刷新</a> · <a href="/export.csv">导出 CSV</a></p>'
            + (''.join(rows) or '<article>暂无反馈</article>') + '</html>').encode()


class Handler(BaseHTTPRequestHandler):
    server_version = 'Feedback'

    def log_message(self, *_):
        pass  # Do not put submitted content or contact information in service logs.

    def reply(self, code, data, content_type='application/json; charset=utf-8'):
        if isinstance(data, dict):
            data = json.dumps(data, ensure_ascii=False).encode()
        self.send_response(code)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Content-Security-Policy', "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'")
        if code == 429:
            self.send_header('Retry-After', '3600')
        self.end_headers()
        self.wfile.write(data)

    def local_admin(self):
        host = self.headers.get('Host', '')
        return self.server.admin and host in ('127.0.0.1:18892', 'localhost:18892', '127.0.0.1:8892')

    def do_GET(self):
        if not self.server.admin and self.path == '/health':
            return self.reply(200, {'status': 'ok', 'schema': 1})
        if not self.local_admin():
            return self.reply(404, {'error': 'not_found'})
        if self.path == '/':
            return self.reply(200, admin_page(self.server.store.records()), 'text/html; charset=utf-8')
        if self.path == '/export.csv':
            output = io.StringIO()
            writer = csv.writer(output)
            writer.writerow(['编号', '时间', '类型', '标题', '内容', '联系方式', '版本', '状态'])
            for r in self.server.store.records():
                values = [r[k] for k in ('id', 'created', 'kind', 'title', 'detail', 'contact', 'version', 'status')]
                writer.writerow(["'" + v if v.lstrip().startswith(('=', '+', '-', '@')) else v for v in values])
            return self.reply(200, output.getvalue().encode('utf-8-sig'), 'text/csv; charset=utf-8')
        self.reply(404, {'error': 'not_found'})

    def do_POST(self):
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 65536 or self.headers.get('Transfer-Encoding'):
                return self.reply(413, {'error': 'body_too_large'})
            self.connection.settimeout(10)
            if self.server.admin:
                if not self.local_admin() or self.path != '/status' or self.headers.get('Origin') != 'http://' + self.headers.get('Host', ''):
                    return self.reply(403, {'error': 'forbidden'})
                from urllib.parse import parse_qs
                data = parse_qs(self.rfile.read(length).decode())
                state, receipt = data.get('status', [''])[0], data.get('id', [''])[0]
                if state not in STATES:
                    return self.reply(400, {'error': 'invalid_status'})
                with self.server.store.connect() as db:
                    db.execute('UPDATE feedback SET status=? WHERE id=?', (state, receipt))
                self.send_response(303)
                self.send_header('Location', '/')
                self.send_header('Content-Length', '0')
                self.end_headers()
                return
            if self.path != '/api/feedback':
                return self.reply(404, {'error': 'not_found'})
            if self.headers.get('Content-Type', '').split(';')[0] != 'application/json':
                return self.reply(415, {'error': 'json_required'})
            data = json.loads(self.rfile.read(length))
            # This listener is loopback-only and nginx overwrites X-Real-IP.
            code, result = self.server.store.submit(data, self.headers.get('X-Real-IP', self.client_address[0]))
            self.reply(code, result)
        except (ValueError, UnicodeError):
            self.reply(400, {'error': 'invalid_request'})
        except (TimeoutError, BrokenPipeError, ConnectionResetError):
            pass
        except Exception:
            self.reply(503, {'error': 'temporarily_unavailable'})


def serve(store, port, admin=False):
    server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    server.store, server.admin = store, admin
    return server


if __name__ == '__main__':
    store = Store(os.environ.get('FEEDBACK_DATA', '/var/lib/mail-intake-feedback'))
    if '--backup' in __import__('sys').argv:
        backups = store.root / 'backups'
        backups.mkdir(exist_ok=True)
        target = backups / (time.strftime('%Y%m%d-%H%M%S', time.gmtime()) + '.sqlite3')
        with store.connect() as source, sqlite3.connect(target) as destination:
            source.backup(destination)
        print('BACKUP_OK', target.name)
    else:
        threading.Thread(target=serve(store, 8892, True).serve_forever, daemon=True).start()
        serve(store, 8891).serve_forever()
