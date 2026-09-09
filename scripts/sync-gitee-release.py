"""Mirror verified public release files. Token is read only from the environment."""
import hashlib
import json
import os
from pathlib import Path
import re
import sys

API = 'https://gitee.com/api/v5/repos/wzFeel/email-attachment-downloader'


def expected_names(tag):
    if not re.fullmatch(r'v\d+\.\d+\.\d+', tag):
        raise ValueError('Expected a stable version tag, e.g. v1.7.0')
    return [f'MailIntake-Setup-{tag[1:]}.exe', 'MailIntake-win-x64.zip', 'SHA256SUMS.txt']


def digest(path):
    with open(path, 'rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def verify_files(root, tag):
    names = expected_names(tag)
    checks = {}
    for line in (root / names[2]).read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'([0-9a-fA-F]{64})\s+\*?([^/\\]+)', line)
        if not match or match[2] in checks:
            raise ValueError('Invalid or duplicate checksum entry')
        checks[match[2]] = match[1].lower()
    if set(checks) != set(names[:2]):
        raise ValueError('Unexpected checksum manifest contents')
    for name in names[:2]:
        if digest(root / name) != checks[name]:
            raise ValueError('Checksum mismatch: ' + name)
    return names


def mirror(tag, root, release):
    import requests
    names = verify_files(root, tag)
    if release['tag_name'] != tag or release.get('draft') or release.get('prerelease'):
        raise ValueError('Source must be the requested stable public release')
    token = os.environ.get('GITEE_TOKEN')
    if not token:
        raise ValueError('Missing GitHub Actions repository secret GITEE_TOKEN')
    session = requests.Session()
    session.headers['Authorization'] = 'Bearer ' + token

    def api(method, path, **kwargs):
        response = session.request(method, API + path, timeout=(20, 300), **kwargs)
        if response.status_code == 404 and method == 'GET':
            return None
        if not response.ok:
            # Do not print response bodies, request headers, or tokens.
            raise RuntimeError(f'Gitee {method} {path}: HTTP {response.status_code}')
        return response.json()

    target = api('GET', '/releases/tags/' + tag)
    if target is None:
        target = api('POST', '/releases', data={
            'tag_name': tag, 'target_commitish': tag,
            'name': release.get('name') or tag,
            'body': release.get('body') or '', 'prerelease': 'false'})
    path = '/releases/' + str(int(target['id'])) + '/attach_files'
    attachments = api('GET', path)
    if not isinstance(attachments, list):
        raise ValueError('Unexpected Gitee attachment response')
    for name in names:
        # Gitee normally uses name; older responses may use filename.
        matches = [a for a in attachments if (a.get('name') or a.get('filename')) == name]
        if len(matches) > 1:
            raise ValueError('Duplicate remote filename: ' + name)
        if not matches:
            with open(root / name, 'rb') as f:
                uploaded = api('POST', path, files={'file': (name, f, 'application/octet-stream')})
            matches = [uploaded]
        asset_id = int(matches[0]['id'])
        # Requests strips Authorization on redirects to a different host.
        with session.get(API + path + '/' + str(asset_id) + '/download',
                         stream=True, timeout=(20, 300)) as response:
            if not response.ok:
                raise RuntimeError('Gitee verification download: HTTP ' + str(response.status_code))
            sha = hashlib.sha256()
            for chunk in response.iter_content(1024 * 1024): sha.update(chunk)
        if sha.hexdigest() != digest(root / name):
            raise ValueError('Gitee attachment differs; no overwrite performed: ' + name)
        print('Verified on Gitee: ' + name)
    print('Gitee release and all three attachment hashes verified: ' + tag)


if __name__ == '__main__':
    try:
        mirror(sys.argv[1], Path(sys.argv[2]), json.loads(Path(sys.argv[3]).read_text(encoding='utf-8-sig')))
    except Exception as error:
        # Known errors contain only controlled text; arbitrary library exceptions may contain secrets.
        if type(error) in (ValueError, RuntimeError):
            print(str(error), file=sys.stderr)
        else:
            print('Release sync failed (' + type(error).__name__ + '); inspect permissions/connectivity.', file=sys.stderr)
        sys.exit(1)
