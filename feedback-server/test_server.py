import json
import http.client
import tempfile
import threading
import unittest
import uuid
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from server import Store, admin_page, serve


class FeedbackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.store = Store(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def payload(self):
        return dict(id=str(uuid.uuid4()), kind='使用问题', title='测试标题', detail='测试说明', contact='', version='1.2.0')

    def test_idempotency_and_conflicting_retry(self):
        data = self.payload()
        self.assertEqual(self.store.submit(data, 'test')[0], 201)
        self.assertEqual(self.store.submit(data, 'test')[0], 200)
        data['detail'] = 'changed'
        self.assertEqual(self.store.submit(data, 'test')[0], 409)
        self.assertEqual(len(self.store.records()), 1)

    def test_rate_limit_survives_restart_and_resets(self):
        for _ in range(5):
            self.assertEqual(self.store.submit(self.payload(), 'test', 3600)[0], 201)
        reloaded = Store(self.temp.name)
        self.assertEqual(reloaded.submit(self.payload(), 'test', 3601)[0], 429)
        self.assertEqual(reloaded.submit(self.payload(), 'test', 7200)[0], 201)

    def test_concurrent_duplicate_is_one_record(self):
        data = self.payload()
        with ThreadPoolExecutor(max_workers=8) as pool:
            results = list(pool.map(lambda _: self.store.submit(data, 'test')[0], range(8)))
        self.assertEqual(results.count(201), 1)
        self.assertEqual(len(self.store.records()), 1)

    def test_validation_and_html_escaping(self):
        for changes in ({'title': ''}, {'detail': 'x' * 10001}, {'id': 'bad'}, {'kind': 'bad'}):
            with self.assertRaises(ValueError):
                self.store.submit(self.payload() | changes, 'test')
        self.store.submit(self.payload() | {'detail': '<script>alert(1)</script>'}, 'test')
        page = admin_page(self.store.records()).decode()
        self.assertNotIn('<script>', page)
        self.assertIn('&lt;script&gt;', page)

    def test_public_listener_never_exposes_admin(self):
        server = serve(self.store, 0)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            base = 'http://127.0.0.1:' + str(server.server_port)
            for path in ('/', '/export.csv', '/status'):
                with self.assertRaises(urllib.error.HTTPError) as caught:
                    urllib.request.urlopen(base + path)
                self.assertEqual(caught.exception.code, 404)
            request = urllib.request.Request(base + '/api/feedback', json.dumps(self.payload()).encode(), {'Content-Type': 'application/json'})
            with urllib.request.urlopen(request) as response:
                self.assertEqual(response.status, 201)
        finally:
            server.shutdown()
            server.server_close()

    def test_admin_rejects_cross_origin_and_updates_status(self):
        payload = self.payload()
        self.store.submit(payload, 'test')
        server = serve(self.store, 0, True)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        try:
            def request(method, path, body=None, headers=None):
                conn = http.client.HTTPConnection('127.0.0.1', server.server_port)
                conn.request(method, path, body=body, headers=headers or {})
                response = conn.getresponse()
                result = response.status
                response.read()
                conn.close()
                return result
            self.assertEqual(request('GET', '/', headers={'Host': 'evil.example'}), 404)
            body = 'id=' + payload['id'] + '&status=%E5%B7%B2%E5%A4%84%E7%90%86'
            self.assertEqual(request('POST', '/status', body, {'Host': '127.0.0.1:18892'}), 403)
            self.assertEqual(request('POST', '/status', body, {'Host': '127.0.0.1:18892', 'Origin': 'http://127.0.0.1:18892'}), 303)
            self.assertEqual(self.store.records()[0]['status'], '已处理')
        finally:
            server.shutdown()
            server.server_close()


if __name__ == '__main__':
    unittest.main()
