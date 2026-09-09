import importlib.util
import pathlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('sync', pathlib.Path(__file__).with_name('sync-gitee-release.py'))
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)

class ValidationTests(unittest.TestCase):
    def test_only_expected_release_names(self):
        self.assertEqual(sync.expected_names('v1.7.0')[0], 'MailIntake-Setup-1.7.0.exe')
        for tag in ['main', 'v1.0.0;echo x', '../v1.0.0']:
            with self.assertRaises(ValueError): sync.expected_names(tag)

    def test_checksums_detect_tampering_and_missing_files(self):
        import hashlib
        with tempfile.TemporaryDirectory() as d:
            root = pathlib.Path(d)
            names = sync.expected_names('v1.7.0')[:2]
            for n in names: (root / n).write_bytes(b'good')
            (root / 'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(b'good').hexdigest()+'  '+n+'\n' for n in names))
            sync.verify_files(root, 'v1.7.0')
            (root / names[0]).write_bytes(b'bad')
            with self.assertRaises(ValueError): sync.verify_files(root, 'v1.7.0')
            (root / names[0]).unlink()
            with self.assertRaises((ValueError, FileNotFoundError)): sync.verify_files(root, 'v1.7.0')

if __name__ == '__main__': unittest.main()
