"""Check tracked content and reachable Git history. Findings never print matched values."""
import json
import re
import subprocess
import sys

def git(*args):
    return subprocess.check_output(['git', *args])

patterns = {
    'private-key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'),
    'github-token': re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
    'aws-key': re.compile(rb'\bAKIA[A-Z0-9]{16}\b'),
}
forbidden = re.compile(r'(^|/)(\.deployment|downloads|backups|bin|obj)/|\.(eml|sqlite3?|db|pfx|key|zip|exe|dll|tar\.gz)$', re.I)
findings = []
count = 0
seen = set()
for row in git('rev-list', '--objects', '--all').decode().splitlines():
    parts = row.split(' ', 1)
    if len(parts) != 2:
        continue
    object_id, path = parts
    if forbidden.search(path):
        findings.append({'path': path, 'check': 'private-data-or-binary-path', 'object': object_id[:12]})
    if object_id in seen or git('cat-file', '-t', object_id).strip() != b'blob':
        continue
    seen.add(object_id)
    count += 1
    content = git('cat-file', 'blob', object_id)
    for label, pattern in patterns.items():
        if pattern.search(content):
            findings.append({'path': path, 'check': label, 'object': object_id[:12]})
print(json.dumps({'history_blobs_scanned': count, 'findings': findings}, ensure_ascii=False, indent=2))
sys.exit(bool(findings))
