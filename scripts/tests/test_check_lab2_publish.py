import contextlib
import importlib.util
import io
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "check-lab2-publish.py"
spec = importlib.util.spec_from_file_location("check_lab2_publish", SCRIPT)
check_lab2_publish = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check_lab2_publish)

PUBLIC_CERTIFICATE = "-----BEGIN CERTIFICATE-----\nMIIBpublic\n-----END CERTIFICATE-----\n"
PRIVATE_KEY = "-----BEGIN PRIVATE KEY-----\nMIIBsecret\n-----END PRIVATE KEY-----\n"


class CheckPublishTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="medsoft-publish-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.web = self.root / "web"
        self.api = self.root / "api"
        (self.web / "wwwroot").mkdir(parents=True)
        (self.web / "wwwroot" / "index.html").write_text("<html></html>", encoding="utf-8")
        self.api.mkdir()
        (self.api / "HospitalInformationSystem.Api.dll").write_bytes(b"MZ")

    def run_check(self):
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = check_lab2_publish.main(["--web", str(self.web), "--api", str(self.api)])
        return code, stdout.getvalue(), stderr.getvalue()

    def test_clean_publish_passes(self):
        (self.web / "wwwroot" / "server.pem").write_text(PUBLIC_CERTIFICATE, encoding="utf-8")
        (self.api / "appsettings.json").write_text('{"UiMock": {"Enabled": false}}', encoding="utf-8")
        code, _, _ = self.run_check()
        self.assertEqual(code, 0)

    def test_local_and_uimock_configs_are_rejected(self):
        (self.api / "appsettings.Local.json").write_text("{}", encoding="utf-8")
        (self.web / "wwwroot" / "appsettings.UiMock.json").write_text("{}", encoding="utf-8")
        code, out, _ = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("appsettings.Local.json", out)
        self.assertIn("appsettings.UiMock.json", out)

    def test_private_key_in_any_extension_is_rejected_but_public_pem_is_allowed(self):
        (self.api / "jwt-signing.key").write_text(PRIVATE_KEY, encoding="utf-8")
        (self.web / "wwwroot" / "chain.pem").write_text(PUBLIC_CERTIFICATE, encoding="utf-8")
        code, out, _ = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("jwt-signing.key", out)
        self.assertNotIn("chain.pem", out)
        self.assertNotIn("secret", out)

    def test_keystores_are_rejected(self):
        (self.api / "server.pfx").write_bytes(b"\x30\x82")
        code, out, _ = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("server.pfx", out)

    def test_enabled_uimock_configuration_is_rejected(self):
        (self.api / "appsettings.Production.json").write_text('{"uimock": {"enabled": true}}', encoding="utf-8")
        code, out, _ = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("appsettings.Production.json", out)

    def test_corrupted_configuration_cannot_be_completed(self):
        (self.api / "appsettings.json").write_text("{ broken", encoding="utf-8")
        code, _, err = self.run_check()
        self.assertEqual(code, 2)
        self.assertIn("повреждённый JSON", err)

    def test_missing_directory_is_an_argument_error(self):
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = check_lab2_publish.main(["--web", str(self.web / "missing"), "--api", str(self.api)])
        self.assertEqual(code, 2)

    def test_missing_required_artifact_fails(self):
        (self.web / "wwwroot" / "index.html").unlink()
        code, out, _ = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("index.html", out)


if __name__ == "__main__":
    unittest.main()
