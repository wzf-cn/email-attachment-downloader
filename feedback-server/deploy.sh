#!/bin/sh
set -eu
cd "$(dirname "$0")"
python3 -m unittest -v test_server.py
getent passwd mail-feedback >/dev/null || useradd --system --home /var/lib/mail-intake-feedback --shell /usr/sbin/nologin mail-feedback
install -d -o root -g root -m 755 /opt/mail-intake-feedback
install -m 644 server.py /opt/mail-intake-feedback/server.py
install -m 644 mail-intake-feedback.service mail-intake-feedback-backup.service mail-intake-feedback-backup.timer /etc/systemd/system/
install -m 644 nginx-location.conf /etc/nginx/snippets/mail-intake-feedback.conf
printf '%s\n' 'limit_req_zone $binary_remote_addr zone=mail_feedback:10m rate=2r/s;' > /etc/nginx/conf.d/mail-intake-feedback-limit.conf
# Only add an include to the selected existing virtual host; keep a timestamped rollback copy.
python3 - <<'PY'
from pathlib import Path
import time
p = Path('/etc/nginx/sites-available/home')
s = p.read_text()
line = '    include /etc/nginx/snippets/mail-intake-feedback.conf;\n'
if line not in s:
    if 'server_name wuzhuofei.com' not in s or '    location / {' not in s:
        raise SystemExit('Unexpected nginx configuration; no replacement made')
    p.with_name('home.before-feedback-' + time.strftime('%Y%m%d-%H%M%S')).write_text(s)
    p.write_text(s.replace('    location / {', line + '\n    location / {', 1))
PY
nginx -t
systemctl daemon-reload
systemctl enable --now mail-intake-feedback.service mail-intake-feedback-backup.timer
systemctl reload nginx
systemctl start mail-intake-feedback-backup.service
curl --fail --silent http://127.0.0.1:8891/health
