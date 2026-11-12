#!/usr/bin/env python3
"""Stage immutable data releases, verify asset digests, then promote latest.

No deletion of a published asset and no token in command-line arguments/logs.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request


class GitHub:
    def __init__(self, repo, token):
        if len(repo.split('/')) != 2 or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_/.' for c in repo):
            raise ValueError('Invalid repository selector')
        self.repo = repo
        self.token = token

    def request(self, method, path, data=None, binary=False):
        url = path if path.startswith('https://uploads.github.com/') else f'https://api.github.com/repos/{self.repo}/{path}'
        headers = {'Authorization': f'Bearer {self.token}', 'User-Agent': 'hots-reference-release/1', 'Accept': 'application/vnd.github+json'}
        if data is not None:
            headers['Content-Type'] = 'application/octet-stream' if binary else 'application/json'
            if not binary:
                data = json.dumps(data).encode()
        request = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if method == 'GET' and error.code == 404:
                return None
            raise RuntimeError(f'GitHub API {method} failed with HTTP {error.code}') from None

    def latest(self):
        return self.request('GET', 'releases/latest')

    def tag(self, tag):
        release = self.request('GET', 'releases/tags/' + urllib.parse.quote(tag, safe=''))
        if release is not None:
            return release
        # Unpublished drafts may not have a Git tag yet and are absent from the
        # tag endpoint. The authenticated release list includes those drafts.
        page = 1
        while True:
            releases = self.request('GET', f'releases?per_page=100&page={page}')
            if releases is None:
                raise RuntimeError('Unable to inspect staged releases')
            for release in releases:
                if release['tag_name'] == tag:
                    return release
            if len(releases) < 100:
                return None
            page += 1

    def asset_json(self, release, name):
        asset = next((a for a in release['assets'] if a['name'] == name), None)
        if asset is None:
            raise RuntimeError(f'Latest data release lacks {name}')
        # Public download: deliberately no Authorization header on redirecting asset URLs.
        with urllib.request.urlopen(asset['browser_download_url'], timeout=120) as response:
            data = response.read(64 * 1024 * 1024 + 1)
        if len(data) > 64 * 1024 * 1024:
            raise RuntimeError('JSON asset exceeds limit')
        if asset.get('digest') and asset['digest'] != 'sha256:' + hashlib.sha256(data).hexdigest():
            raise RuntimeError('Published JSON digest mismatch')
        return json.loads(data)


def previous(client, target):
    release = client.latest()
    patches = [] if release is None else client.asset_json(release, 'patches.json')
    Path(target).parent.mkdir(parents=True, exist_ok=True)
    Path(target).write_text(json.dumps(patches), encoding='utf-8')


def verify_directory(root, manifest):
    import tarfile
    calculated = hashlib.sha256((json.dumps(manifest['files'], indent=2, sort_keys=True, ensure_ascii=False) + '\n').encode()).hexdigest()
    if calculated != manifest['datasetVersion']:
        raise RuntimeError('Dataset version does not match payload hashes')
    required = {'game.json', 'patches.json', 'assets.json', 'SOURCES.md'}
    required |= {f'locales/{code}.json' for code in ('dede','enus','eses','esmx','frfr','itit','kokr','plpl','ptbr','ruru','zhcn','zhtw')}
    if not required.issubset(manifest['files']):
        raise RuntimeError('Incomplete release file inventory')
    for name, checksum in manifest['files'].items():
        path = Path(name)
        if path.is_absolute() or '..' in path.parts or root.joinpath(path).is_symlink():
            raise RuntimeError('Unsafe release path')
        if hashlib.sha256(root.joinpath(path).read_bytes()).hexdigest() != checksum:
            raise RuntimeError('Release file digest mismatch')
    archive = root / 'dataset.tar.gz'
    expected = (root / 'dataset.tar.gz.sha256').read_text().split()[0]
    if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
        raise RuntimeError('Archive checksum mismatch')
    with tarfile.open(archive, 'r:gz') as package:
        members = package.getmembers()
        names = [m.name for m in members]
        if set(names) != set(manifest['files']) | {'manifest.json'} or len(names) != len(set(names)):
            raise RuntimeError('Archive inventory mismatch')
        for member in members:
            if not member.isfile() or member.size > 64 * 1024 * 1024:
                raise RuntimeError('Invalid archive entry')
            value = package.extractfile(member).read()
            checksum = hashlib.sha256(value).hexdigest()
            target = hashlib.sha256((root / 'manifest.json').read_bytes()).hexdigest() if member.name == 'manifest.json' else manifest['files'][member.name]
            if checksum != target:
                raise RuntimeError('Archive content mismatch')


def publish(client, directory):
    root = Path(directory)
    manifest = json.loads((root / 'manifest.json').read_text())
    verify_directory(root, manifest)
    version = manifest['datasetVersion']
    if len(version) != 64 or any(c not in '0123456789abcdef' for c in version):
        raise ValueError('Invalid dataset version')
    latest = client.latest()
    if latest is not None and client.asset_json(latest, 'manifest.json')['datasetVersion'] == version:
        print('unchanged', version)
        return False
    # Full archive identity avoids collisions with interrupted provenance-only drafts.
    tag = 'data-' + hashlib.sha256((root / 'dataset.tar.gz').read_bytes()).hexdigest()
    release = client.tag(tag)
    if release is None:
        release = client.request('POST', 'releases', {'tag_name': tag, 'name': tag, 'draft': True, 'body': 'Canonical HotS reference data. See manifest.json, schemas and SOURCES.md in the archive.'})
    # Distinct names for language assets, plus complete bulk package and native schemas.
    paths = [root / n for n in ('manifest.json', 'game.json', 'patches.json', 'assets.json', 'dataset.tar.gz', 'dataset.tar.gz.sha256', 'SOURCES.md')]
    paths += sorted((root / 'locales').glob('*.json'))
    paths += sorted((root / 'schemas').glob('*.json'))
    expected = {}
    for path in paths:
        data = path.read_bytes()
        checksum = 'sha256:' + hashlib.sha256(data).hexdigest()
        expected[path.name] = checksum
        asset = next((a for a in release['assets'] if a['name'] == path.name), None)
        if asset is None:
            upload = release['upload_url'].split('{')[0] + '?name=' + urllib.parse.quote(path.name)
            asset = client.request('POST', upload, data, binary=True)
        if asset.get('digest') != checksum:
            raise RuntimeError(f'Asset {path.name} not verified; latest remains unchanged')
    verified = client.request('GET', f"releases/{release['id']}")
    if verified is None:
        raise RuntimeError('Staged release unavailable; latest remains unchanged')
    actual = {a['name']: a.get('digest') for a in verified['assets']}
    if any(actual.get(name) != checksum for name, checksum in expected.items()):
        raise RuntimeError('Incomplete staged release; latest remains unchanged')
    client.request('PATCH', f"releases/{release['id']}", {'draft': False, 'prerelease': False, 'make_latest': 'true'})
    print('published', version)
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('previous', 'publish'))
    parser.add_argument('path')
    args = parser.parse_args()
    client = GitHub(os.environ['GITHUB_REPOSITORY'], os.environ['GH_TOKEN'])
    if args.command == 'previous':
        previous(client, args.path)
    else:
        publish(client, args.path)


if __name__ == '__main__':
    main()
