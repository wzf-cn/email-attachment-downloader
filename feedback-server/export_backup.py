"""Run as root after server.py --backup; export latest consistent snapshot for SSH download."""
import os
import pwd
import sqlite3
import tarfile
from pathlib import Path

root = Path('/var/lib/mail-intake-feedback')
snapshot = sorted((root / 'backups').glob('????????-??????.sqlite3'))[-1]
with sqlite3.connect(snapshot) as db:
    if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
        raise SystemExit('Backup integrity check failed')
account = pwd.getpwnam('ubuntu')
folder = Path('/home/ubuntu/mail-feedback-transfer')
folder.mkdir(mode=0o700, exist_ok=True)
folder.chmod(0o700)
os.chown(folder, account.pw_uid, account.pw_gid)
target = folder / 'latest.tar.gz'
with tarfile.open(target.with_suffix('.tmp'), 'w:gz') as archive:
    archive.add(snapshot, arcname='feedback.sqlite3')
    archive.add(root / 'ip-salt', arcname='ip-salt')
target.with_suffix('.tmp').replace(target)
target.chmod(0o600)
os.chown(target, account.pw_uid, account.pw_gid)
print('EXPORT_INTEGRITY_OK')
