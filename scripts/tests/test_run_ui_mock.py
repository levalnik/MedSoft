import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class RunUiMockTests(unittest.TestCase):
    script = Path(__file__).resolve().parents[1] / "run-ui-mock.sh"

    def run_script(self, *arguments):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            dotnet = directory / "dotnet"
            dotnet.write_text(
                "#!/usr/bin/env python3\n"
                "import json, sys\n"
                "print(json.dumps(sys.argv[1:]))\n"
            )
            dotnet.chmod(0o700)
            return subprocess.run(
                ["bash", str(self.script), *arguments],
                cwd=directory,
                env={**os.environ, "PATH": str(directory) + os.pathsep + os.environ["PATH"]},
                capture_output=True,
                text=True,
                check=False,
            )

    def test_launch_sets_the_browser_environment_even_from_another_directory(self):
        result = self.run_script("--no-restore")
        self.assertEqual(result.returncode, 0, result.stderr)
        arguments = json.loads(result.stdout)
        self.assertIn("-p:WasmApplicationEnvironmentName=UiMock", arguments)
        self.assertEqual(arguments[arguments.index("--launch-profile") + 1], "ui-mock")
        self.assertTrue(Path(arguments[arguments.index("--project") + 1]).is_dir())
        self.assertIn("--no-restore", arguments)

    def test_no_build_is_rejected_instead_of_starting_an_old_real_API_build(self):
        result = self.run_script("--no-build")
        self.assertEqual(result.returncode, 2)
        self.assertIn("--no-build", result.stderr)
        self.assertEqual(result.stdout, "")


if __name__ == "__main__":
    unittest.main()
