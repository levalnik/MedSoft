#!/usr/bin/env python3
import argparse
import getpass
import json
import os
import secrets
import subprocess
import sys
from pathlib import Path


def write_private(path, contents):
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8") as output:
        output.write(contents)


def read_database_password(path):
    if not path.exists():
        return None
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.startswith("POSTGRES_PASSWORD="):
            continue
        value = line.split("=", 1)[1].strip()
        if value.startswith("'") and value.endswith("'"):
            value = value[1:-1].replace("\\'", "'")
        elif value.startswith('"') and value.endswith('"'):
            value = json.loads(value)
            if "$" in value.replace("$$", ""):
                raise ValueError("Интерполяция переменных в пароле .env не поддерживается. Укажите буквальный пароль.")
            value = value.replace("$$", "$")
        elif "$" in value or " #" in value:
            raise ValueError("Заключите пароль .env в кавычки: интерполяция и inline-комментарии не поддерживаются.")
        return value
    return None


def existing_database():
    try:
        subprocess.run(["docker", "info"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                       timeout=15, check=True)
        return subprocess.run(
            ["docker", "volume", "inspect", "medsoft-his_hospital-data"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10
        ).returncode == 0
    except (OSError, subprocess.TimeoutExpired, subprocess.CalledProcessError) as error:
        raise ValueError("Для --fresh-database запустите Docker, чтобы проверить существующий том БД.") from error


def main():
    parser = argparse.ArgumentParser(description="Создать локальные настройки HIS без записи секретов в Git.")
    parser.add_argument("--fresh-database", action="store_true",
                        help="Сгенерировать пароль для новой БД; не использовать для существующего тома.")
    parser.add_argument("--database-password-stdin", action="store_true",
                        help="Прочитать текущий пароль БД из stdin без отображения.")
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    env_path = root / ".env"
    local_path = root / "src/Presentation/HospitalInformationSystem.Api/appsettings.Local.json"
    saved_password = read_database_password(env_path)
    environment_password = os.environ.get("POSTGRES_PASSWORD")
    if saved_password and environment_password and saved_password != environment_password:
        raise ValueError("POSTGRES_PASSWORD в окружении отличается от .env. Укажите один действительный пароль БД.")
    if env_path.exists() and local_path.exists():
        print("Локальные настройки уже существуют; файлы не изменены.")
        return
    if local_path.exists() and not env_path.exists():
        raise ValueError("appsettings.Local.json уже существует. Создайте .env с тем же паролем БД; настройки не изменены.")
    password = environment_password or saved_password
    if not password and arguments.database_password_stdin:
        password = sys.stdin.readline().rstrip("\r\n")
    if not password:
        if arguments.fresh_database and not existing_database():
            password = secrets.token_urlsafe(32)
        elif sys.stdin.isatty():
            password = getpass.getpass("Текущий пароль PostgreSQL (для существующей БД): ")
        else:
            raise ValueError("Укажите текущий POSTGRES_PASSWORD или используйте --fresh-database для новой БД.")
    if not password or password == "replace-with-a-random-local-password" or any(c in password for c in "\r\n\0"):
        raise ValueError("Нужен действительный пароль БД; шаблон и пустые значения не принимаются.")

    if not env_path.exists():
        quoted = json.dumps(password.replace("$", "$$"), ensure_ascii=False)
        write_private(env_path, "POSTGRES_PASSWORD=" + quoted + "\n")
    if not local_path.exists():
        escaped = password.replace('"', '""')
        config = {
            "ConnectionStrings": {
                "Postgres": 'Host=localhost;Port=5432;Database=hospital;Username=postgres;Password="' + escaped + '"'
            },
            "Authentication": {"BootstrapAdmin": {
                "UserName": "admin", "Password": secrets.token_urlsafe(32) + "Aa1!"
            }}
        }
        write_private(local_path, json.dumps(config, ensure_ascii=False, indent=2) + "\n")
    print("Готово. Логин первого администратора: admin.")
    print("Пароль администратора находится в " + str(local_path))
    print("Существующая база и её пароль не изменялись. Не добавляйте локальные файлы в Git.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
