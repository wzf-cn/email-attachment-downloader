"""Offline restore rehearsal; only writes to a fresh temporary directory."""
import hashlib
import json
import sqlite3
import sys
import tarfile
import tempfile
from pathlib import Path

archive_path = Path(sys.argv[1])
with tarfile.open(archive_path, 'r:gz') as archive, tempfile.TemporaryDirectory() as temp:
    if set(archive.getnames()) != {'feedback.sqlite3', 'ip-salt'}:
        raise SystemExit('Unexpected backup members')
    # Do not extract arbitrary archive paths.
    target = Path(temp) / 'restored.sqlite3'
    with archive.extractfile('feedback.sqlite3') as source, target.open('wb') as output:
        import shutil
        shutil.copyfileobj(source, output)
    db = sqlite3.connect(target)
    try:
        integrity = db.execute('PRAGMA integrity_check').fetchone()[0]
        count = db.execute('SELECT COUNT(*) FROM feedback').fetchone()[0]
        if integrity != 'ok':
            raise SystemExit('Restored database is invalid')
    finally:
        db.close()
print(json.dumps({'restore': 'ok', 'records': count, 'sha256': hashlib.sha256(archive_path.read_bytes()).hexdigest()}))
