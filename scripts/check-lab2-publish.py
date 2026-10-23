#!/usr/bin/env python3
"""Проверка каталогов готового publish лабораторной 2.

Скрипт только читает указанные каталоги: ничего не удаляет, не запускает сервер
и не читает переменные окружения с секретами. Проверяет наличие обязательных
артефактов и отсутствие запрещённых: локальных/UiMock-конфигов, приватных ключей,
PFX/P12 и явно включённого режима UiMock.

Коды выхода:
  0 - проверка пройдена;
  1 - найдены запрещённые или отсутствуют обязательные артефакты;
  2 - неверный путь/аргумент либо проверку нельзя завершить (например, повреждённый JSON).
"""
import argparse
import json
import sys
from pathlib import Path

FORBIDDEN_CONFIG_NAMES = {"appsettings.local.json", "appsettings.uimock.json"}
KEYSTORE_SUFFIXES = {".pfx", ".p12"}
PEM_LIKE_SUFFIXES = {".pem", ".key"}
PRIVATE_KEY_MARKERS = (
    b"-----BEGIN PRIVATE KEY-----",
    b"-----BEGIN RSA PRIVATE KEY-----",
    b"-----BEGIN EC PRIVATE KEY-----",
    b"-----BEGIN DSA PRIVATE KEY-----",
    b"-----BEGIN ENCRYPTED PRIVATE KEY-----",
    b"-----BEGIN OPENSSH PRIVATE KEY-----",
)


class CheckError(Exception):
    """Проверку нельзя завершить достоверно (код выхода 2)."""


def relative(root, path):
    return path.relative_to(root).as_posix()


def required_artifact_problems(web, api):
    problems = []
    if not (web / "wwwroot" / "index.html").is_file():
        problems.append("web: отсутствует обязательный артефакт wwwroot/index.html")
    if not (api / "HospitalInformationSystem.Api.dll").is_file():
        problems.append("api: отсутствует обязательная сборка HospitalInformationSystem.Api.dll")
    return problems


def contains_private_key(path):
    try:
        with path.open("rb") as handle:
            data = handle.read()
    except OSError as error:
        raise CheckError(f"{path.name}: не удалось прочитать файл для проверки ключей") from error
    return any(marker in data for marker in PRIVATE_KEY_MARKERS)


def forbidden_artifact_problems(root, label):
    problems = []
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        name = path.name.lower()
        suffix = path.suffix.lower()
        if name in FORBIDDEN_CONFIG_NAMES:
            problems.append(f"{label}: {relative(root, path)} — запрещённый конфиг не должен попадать в publish")
            continue
        if suffix in KEYSTORE_SUFFIXES:
            problems.append(f"{label}: {relative(root, path)} — хранилище ключей (PFX/P12) не должно публиковаться")
            continue
        if suffix in PEM_LIKE_SUFFIXES and contains_private_key(path):
            problems.append(f"{label}: {relative(root, path)} — содержит приватный ключ")
    return problems


def uimock_enabled_problems(root, label):
    problems = []
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        if not (path.name.lower().startswith("appsettings") and path.suffix.lower() == ".json"):
            continue
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as error:
            raise CheckError(f"{relative(root, path)}: повреждённый JSON, проверку UiMock нельзя завершить") from error
        if ui_mock_is_enabled(document):
            problems.append(f"{label}: {relative(root, path)} — UiMock.Enabled=true не допускается в publish")
    return problems


def lookup(document, key):
    if not isinstance(document, dict):
        return None
    for existing, value in document.items():
        if existing.lower() == key.lower():
            return value
    return None


def ui_mock_is_enabled(document):
    section = lookup(document, "UiMock")
    return lookup(section, "Enabled") is True


def analyze(web, api):
    problems = []
    problems.extend(required_artifact_problems(web, api))
    for root, label in ((web, "web"), (api, "api")):
        problems.extend(forbidden_artifact_problems(root, label))
        problems.extend(uimock_enabled_problems(root, label))
    return problems


def resolve_directory(raw):
    path = Path(raw)
    if not path.is_dir():
        raise CheckError(f"каталог не найден: {raw}")
    return path


def main(argv=None):
    parser = argparse.ArgumentParser(description="Проверить publish Web и API лабораторной 2 на запрещённые артефакты.")
    parser.add_argument("--web", required=True, help="Каталог опубликованного Blazor Web.")
    parser.add_argument("--api", required=True, help="Каталог опубликованного API.")
    arguments = parser.parse_args(argv)

    try:
        web = resolve_directory(arguments.web)
        api = resolve_directory(arguments.api)
        problems = analyze(web, api)
    except CheckError as error:
        print(f"Проверку нельзя завершить: {error}", file=sys.stderr)
        return 2

    if problems:
        print("Проверка publish не пройдена:")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print("Проверка publish пройдена: запрещённых артефактов не найдено.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
