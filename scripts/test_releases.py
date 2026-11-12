import copy
import hashlib
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from releases import GitHub, publish


class FakeGitHub(GitHub):
    def __init__(self, version=None, fail_upload=False):
        self.version = version
        self.fail_upload = fail_upload
        self.release = None
        self.releases = {}
        self.fail_after = 1
        self.promotions = 0
        self.uploads = 0

    def latest(self):
        return {'assets': []} if self.version else None

    def asset_json(self, release, name):
        return {'datasetVersion': self.version}

    def request(self, method, path, data=None, binary=False):
        if method == 'GET':
            if path.startswith('releases/tags/'):
                release = self.releases.get(path.removeprefix('releases/tags/'))
                return copy.deepcopy(release) if release and not release['draft'] else None
            if path.startswith('releases?'):
                return copy.deepcopy(list(self.releases.values()))
            return copy.deepcopy(next((r for r in self.releases.values() if path == f"releases/{r['id']}"), None))
        if binary:
            self.uploads += 1
            if self.fail_upload and self.uploads >= self.fail_after:
                raise RuntimeError('simulated upload failure')
            from urllib.parse import parse_qs, urlparse
            name = parse_qs(urlparse(path).query)['name'][0]
            asset = {'name': name, 'digest': 'sha256:' + hashlib.sha256(data).hexdigest()}
            self.release['assets'].append(asset)
            return asset
        if method == 'POST':
            self.release = {'id': len(self.releases)+1, 'tag_name': data['tag_name'], 'draft': True, 'assets': [], 'upload_url': 'https://uploads.github.com/fake{?name}'}
            self.releases[data['tag_name']] = self.release
            return copy.deepcopy(self.release)
        self.promotions += 1
        self.release['draft'] = False
        return {}


class DraftLookupTests(unittest.TestCase):
    def client(self, responses):
        client = GitHub('owner/repo', 'test-token')
        calls = []
        def request(method, path, data=None, binary=False):
            calls.append((method, path))
            self.assertIn(path, responses)
            return copy.deepcopy(responses[path])
        client.request = request
        return client, calls

    def test_published_tag_needs_no_draft_search(self):
        release = {'id': 1, 'tag_name': 'data-a', 'draft': False}
        client, calls = self.client({'releases/tags/data-a': release})
        self.assertEqual(client.tag('data-a'), release)
        self.assertEqual(len(calls), 1)

    def test_unpublished_draft_is_found_without_a_git_tag(self):
        release = {'id': 2, 'tag_name': 'data-a', 'draft': True}
        client, _ = self.client({
            'releases/tags/data-a': None,
            'releases?per_page=100&page=1': [release],
        })
        self.assertEqual(client.tag('data-a'), release)

    def test_draft_lookup_checks_later_pages(self):
        release = {'id': 2, 'tag_name': 'data-a', 'draft': True}
        client, _ = self.client({
            'releases/tags/data-a': None,
            'releases?per_page=100&page=1': [{'tag_name': f'other-{i}'} for i in range(100)],
            'releases?per_page=100&page=2': [release],
        })
        self.assertEqual(client.tag('data-a'), release)

    def test_absent_tag_and_draft_return_none(self):
        client, _ = self.client({
            'releases/tags/data-a': None,
            'releases?per_page=100&page=1': [],
        })
        self.assertIsNone(client.tag('data-a'))


class ReleaseTests(unittest.TestCase):
    def fixture(self, root, source='original'):
        (root / 'locales').mkdir()
        (root / 'schemas').mkdir()
        for name in ('game.json', 'patches.json', 'assets.json', 'SOURCES.md'):
            (root / name).write_text('{}')
        for locale in ('dede','enus','eses','esmx','frfr','itit','kokr','plpl','ptbr','ruru','zhcn','zhtw'):
            (root / 'locales' / (locale + '.json')).write_text('{}')
        (root / 'schemas' / 'game.schema.json').write_text('{}')
        files = {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob('*') if p.is_file()}
        version = hashlib.sha256((json.dumps(files, indent=2, sort_keys=True, ensure_ascii=False) + '\n').encode()).hexdigest()
        manifest = {'datasetVersion': version, 'files': files, 'sources': {'test': source}}
        (root / 'manifest.json').write_text(json.dumps(manifest))
        with tarfile.open(root / 'dataset.tar.gz', 'w:gz') as package:
            for name in [*files, 'manifest.json']:
                package.add(root / name, arcname=name)
        checksum = hashlib.sha256((root / 'dataset.tar.gz').read_bytes()).hexdigest()
        (root / 'dataset.tar.gz.sha256').write_text(checksum + '  dataset.tar.gz\n')
        return version

    def test_unchanged_never_creates_release(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            version = self.fixture(root)
            client = FakeGitHub(version)
            self.assertFalse(publish(client, root))
            self.assertEqual(client.uploads, 0)
            self.assertEqual(client.promotions, 0)

    def test_upload_failure_does_not_promote_latest(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            client = FakeGitHub('b' * 64, fail_upload=True)
            with self.assertRaisesRegex(RuntimeError, 'upload failure'):
                publish(client, root)
            self.assertEqual(client.promotions, 0)
            self.assertEqual(client.version, 'b' * 64)

    def test_interrupted_unpublished_draft_is_reused_on_retry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            client = FakeGitHub('b' * 64, fail_upload=True)
            client.fail_after = 2
            with self.assertRaisesRegex(RuntimeError, 'upload failure'):
                publish(client, root)
            self.assertTrue(client.release['draft'])
            self.assertEqual(client.promotions, 0)
            client.fail_upload = False
            self.assertTrue(publish(client, root))
            self.assertEqual(len(client.releases), 1)
            self.assertEqual(client.promotions, 1)

    def test_complete_verified_stage_is_promoted_and_retry_is_safe(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            client = FakeGitHub()
            self.assertTrue(publish(client, root))
            self.assertEqual(client.promotions, 1)
            count = client.uploads
            self.assertTrue(publish(client, root))
            self.assertEqual(client.uploads, count)

    def test_missing_release_during_final_verification_is_not_promoted(self):
        class MissingVerification(FakeGitHub):
            def request(self, method, path, data=None, binary=False):
                if method == 'GET' and path.startswith('releases/') and not path.startswith('releases/tags/'):
                    return None
                return super().request(method, path, data, binary)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            client = MissingVerification()
            with self.assertRaisesRegex(RuntimeError, 'Staged release unavailable'):
                publish(client, root)
            self.assertEqual(client.promotions, 0)
            self.assertTrue(client.release['draft'])

    def test_tampered_json_cannot_be_published(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            (root / 'game.json').write_text('corrupt')
            client = FakeGitHub()
            with self.assertRaisesRegex(RuntimeError, 'digest mismatch'):
                publish(client, root)
            self.assertEqual(client.uploads, 0)

    def test_partial_draft_with_new_provenance_can_be_retried(self):
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            first, second = Path(a), Path(b)
            self.assertEqual(self.fixture(first, 'sha-a'), self.fixture(second, 'sha-b'))
            client = FakeGitHub('b' * 64, fail_upload=True)
            client.fail_after = 2
            with self.assertRaises(RuntimeError):
                publish(client, first)
            self.assertEqual(client.promotions, 0)
            client.fail_upload = False
            self.assertTrue(publish(client, second))
            self.assertEqual(client.promotions, 1)
            self.assertEqual(len(client.releases), 2)

    def test_stale_claimed_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root)
            path = root / 'manifest.json'
            manifest = json.loads(path.read_text())
            manifest['datasetVersion'] = 'a' * 64
            path.write_text(json.dumps(manifest))
            with self.assertRaisesRegex(RuntimeError, 'Dataset version'):
                publish(FakeGitHub(), root)


if __name__ == '__main__':
    unittest.main()
