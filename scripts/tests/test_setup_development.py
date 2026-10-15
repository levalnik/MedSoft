import importlib.util
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "setup-development.py"
COMPOSE = SCRIPT.parent.parent / "docker-compose.yml"
spec = importlib.util.spec_from_file_location("setup_development", SCRIPT)
setup_development = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup_development)


class SetupDevelopmentTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="medsoft-setup-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / "scripts").mkdir()
        shutil.copy(SCRIPT, self.root / "scripts/setup-development.py")
        shutil.copy(COMPOSE, self.root / "docker-compose.yml")
        self.local = self.root / "src/Presentation/HospitalInformationSystem.Api/appsettings.Local.json"
        self.env_path = self.root / ".env"
        self.environment = {key: value for key, value in os.environ.items()
                            if key != "POSTGRES_PASSWORD" and not key.startswith("COMPOSE_")}

    def run_setup(self, password=None, arguments=(), stdin=None):
        environment = dict(self.environment)
        if password is not None:
            environment["POSTGRES_PASSWORD"] = password
        return subprocess.run(["python3", str(self.root / "scripts/setup-development.py"), *arguments],
                              input=stdin, capture_output=True, text=True, env=environment, timeout=30)

    def test_settings_are_private_and_do_not_print_passwords(self):
        password = "Temporary.Database-Aa1!"
        result = self.run_setup(password)
        self.assertEqual(result.returncode, 0, result.stderr)
        config = json.loads(self.local.read_text())
        admin_password = config["Authentication"]["BootstrapAdmin"]["Password"]
        self.assertGreaterEqual(len(admin_password), 12)
        self.assertNotIn(password, result.stdout + result.stderr)
        self.assertNotIn(admin_password, result.stdout + result.stderr)
        if os.name != "nt":
            self.assertEqual(self.local.stat().st_mode & 0o777, 0o600)
            self.assertEqual(self.env_path.stat().st_mode & 0o777, 0o600)

    def test_repeat_does_not_overwrite_settings_or_bootstrap_password(self):
        self.assertEqual(self.run_setup("Temporary.Database-Aa1!").returncode, 0)
        before = self.local.read_bytes(), self.env_path.read_bytes()
        self.assertEqual(self.run_setup().returncode, 0)
        self.assertEqual((self.local.read_bytes(), self.env_path.read_bytes()), before)

    def test_conflicting_environment_is_rejected_even_when_both_files_exist(self):
        self.assertEqual(self.run_setup("Temporary.Database-Aa1!").returncode, 0)
        before = self.local.read_bytes(), self.env_path.read_bytes()
        result = self.run_setup("Different.Database-Aa1!")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.local.read_bytes(), self.env_path.read_bytes()), before)

    def test_missing_local_settings_reuse_the_literal_saved_password(self):
        password = "Temporary\\'\";${UNSET_AUDIT_VALUE}$$# Aa1!\\"
        self.assertEqual(self.run_setup(password).returncode, 0)
        before = self.env_path.read_bytes()
        self.local.unlink()
        result = self.run_setup()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.env_path.read_bytes(), before)
        self.assertEqual(setup_development.read_database_password(self.env_path), password)
        config = json.loads(self.local.read_text())
        self.assertIn('Password="' + password.replace('"', '""') + '"', config["ConnectionStrings"]["Postgres"])

    def test_password_from_stdin_preserves_surrounding_spaces(self):
        password = " Temporary.Database-Aa1! "
        result = self.run_setup(arguments=["--database-password-stdin"], stdin=password + "\n")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(setup_development.read_database_password(self.env_path), password)

    def test_invalid_passwords_do_not_create_partial_settings(self):
        for password in ["replace-with-a-random-local-password", "bad\npassword", "bad\rpassword"]:
            with self.subTest(password=password):
                self.assertNotEqual(self.run_setup(password).returncode, 0)
                self.assertFalse(self.env_path.exists())
                self.assertFalse(self.local.exists())

    def test_old_single_quoted_backslashes_are_read_literally(self):
        self.env_path.write_text("POSTGRES_PASSWORD='Temporary\\\\Database-Aa1!'\n")
        self.assertEqual(setup_development.read_database_password(self.env_path), "Temporary\\\\Database-Aa1!")

    @unittest.skipUnless(shutil.which("docker"), "Docker Compose is required for the real dotenv parser check")
    def test_compose_receives_exactly_the_password_used_by_the_api(self):
        version = subprocess.run(["docker", "compose", "version"], capture_output=True, timeout=15)
        if version.returncode != 0:
            self.skipTest("Docker Compose is unavailable")
        passwords = ["Temporary.Database-Aa1!", r"Temporary\Database\Aa1!", "quote'\"Aa1!",
                     "Temporary\\'\";$#=Aa1!", "trailing\\", "cash$$$a${UNSET_AUDIT_VALUE}$a",
                     " surrounding spaces Aa1! ", "Пароль漢字Aa1!"]
        for password in passwords:
            with self.subTest(password=password):
                result = self.run_setup(password)
                self.assertEqual(result.returncode, 0, result.stderr)
                parsed = subprocess.run(
                    ["docker", "compose", "--env-file", str(self.env_path), "-f",
                     str(self.root / "docker-compose.yml"), "config", "--environment"],
                    env=self.environment, capture_output=True, text=True, timeout=15)
                self.assertEqual(parsed.returncode, 0)
                resolved = next(line.split("=", 1)[1] for line in parsed.stdout.splitlines()
                                if line.startswith("POSTGRES_PASSWORD="))
                self.assertEqual(resolved, password)
                self.assertEqual(setup_development.read_database_password(self.env_path), password)
                self.env_path.unlink()
                self.local.unlink()


if __name__ == "__main__":
    unittest.main()
