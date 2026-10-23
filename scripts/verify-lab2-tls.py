#!/usr/bin/env python3
"""Проверка учебного TLS-стенда лабораторной 2 по явным параметрам.

Скрипт проверяет уже запущенный HTTPS endpoint внешним OpenSSL: успешный TLS 1.3
handshake и доверие к указанному CA, имя сервера, срок и издателя сертификата,
а также отказ сервера в TLS 1.2. Скрипт не создаёт сертификаты, не меняет системное
доверие, не отключает проверку и не читает приватные ключи.

Коды выхода:
  0 - все проверки пройдены;
  1 - стенд не соответствует проверкам;
  2 - неверные аргументы либо нет подходящего инструмента (например, OpenSSL без TLS 1.3).
"""
import argparse
import ipaddress
import subprocess
import sys
import tempfile
from collections import namedtuple
from pathlib import Path

Completed = namedtuple("Completed", "returncode stdout stderr")

CONNECTIVITY_MARKERS = ("connect:errno", "connection refused", "getaddrinfo", "name or service not known",
                        "no route to host", "errno=61", "errno=111")


class ToolError(Exception):
    """Инструмент недоступен или не поддерживает нужные возможности (код выхода 2)."""


class VerificationFailed(Exception):
    """Стенд не прошёл проверку (код выхода 1)."""


def default_runner(args, timeout, stdin=""):
    result = subprocess.run(args, input=stdin, capture_output=True, text=True, timeout=timeout)
    return Completed(result.returncode, result.stdout or "", result.stderr or "")


def is_ip(host):
    try:
        ipaddress.ip_address(host)
        return True
    except ValueError:
        return False


def ensure_tls13_tool(runner, openssl):
    try:
        result = runner([openssl, "s_client", "-help"], timeout=15)
    except FileNotFoundError as error:
        raise ToolError(f"OpenSSL не найден: {openssl}") from error
    except subprocess.TimeoutExpired as error:
        raise ToolError("OpenSSL не ответил на запрос возможностей вовремя.") from error
    if "-tls1_3" not in (result.stdout + result.stderr):
        raise ToolError("Указанный OpenSSL не поддерживает TLS 1.3. Передайте путь к подходящей версии через --openssl.")


def handshake_tls13(runner, openssl, host, port, ca, timeout):
    args = [openssl, "s_client", "-connect", f"{host}:{port}", "-CAfile", ca,
            "-tls1_3", "-verify_return_error", "-verify", "5", "-showcerts"]
    if is_ip(host):
        args += ["-verify_ip", host]
    else:
        args += ["-verify_hostname", host, "-servername", host]
    try:
        result = runner(args, timeout=timeout, stdin="")
    except subprocess.TimeoutExpired as error:
        raise VerificationFailed("TLS 1.3 handshake не завершился вовремя — стенд не подтверждён.") from error
    combined = result.stdout + result.stderr
    if result.returncode != 0:
        raise VerificationFailed("TLS 1.3 handshake или проверка цепочки/имени не прошли.")
    if "TLSv1.3" not in combined:
        raise VerificationFailed("Согласован не TLS 1.3.")
    if "Verify return code: 0" not in combined and "Verification: OK" not in combined:
        raise VerificationFailed("Сертификат не прошёл проверку доверия к указанному CA или имени сервера.")
    certificate = extract_certificate(combined)
    if certificate is None:
        raise VerificationFailed("Сервер не прислал сертификат для проверки издателя.")
    return certificate


def extract_certificate(text):
    begin = "-----BEGIN CERTIFICATE-----"
    end = "-----END CERTIFICATE-----"
    start = text.find(begin)
    finish = text.find(end)
    if start == -1 or finish == -1:
        return None
    return text[start:finish + len(end)] + "\n"


def check_certificate(runner, openssl, certificate, isu, timeout):
    with tempfile.NamedTemporaryFile("w", suffix=".pem", delete=True) as handle:
        handle.write(certificate)
        handle.flush()
        issuer = run_x509(runner, openssl, ["-issuer", "-nameopt", "RFC2253"], handle.name, timeout)
        validity = run_x509(runner, openssl, ["-checkend", "0"], handle.name, timeout, allow_failure=True)
    expected_ou = f"OU=MedSoft 2026/27 {isu}"
    if "O=ITMO" not in issuer:
        raise VerificationFailed("Издатель сертификата не содержит O=ITMO.")
    if expected_ou not in issuer:
        raise VerificationFailed(f"Издатель сертификата не содержит '{expected_ou}'.")
    if validity.returncode != 0:
        raise VerificationFailed("Срок действия сертификата истёк.")


def run_x509(runner, openssl, options, path, timeout, allow_failure=False):
    args = [openssl, "x509", "-noout", *options, "-in", path]
    try:
        result = runner(args, timeout=timeout, stdin="")
    except subprocess.TimeoutExpired as error:
        raise VerificationFailed("Разбор сертификата не завершился вовремя.") from error
    if result.returncode != 0 and not allow_failure:
        raise VerificationFailed("Не удалось разобрать сертификат сервера.")
    return result if allow_failure else result.stdout


def ensure_tls12_rejected(runner, openssl, host, port, ca, timeout):
    args = [openssl, "s_client", "-connect", f"{host}:{port}", "-CAfile", ca, "-tls1_2"]
    if is_ip(host):
        args += ["-verify_ip", host]
    else:
        args += ["-verify_hostname", host, "-servername", host]
    try:
        result = runner(args, timeout=timeout, stdin="")
    except subprocess.TimeoutExpired as error:
        raise VerificationFailed("Тайм-аут при проверке TLS 1.2 — отказ не подтверждён.") from error
    combined = (result.stdout + result.stderr).lower()
    if result.returncode == 0:
        raise VerificationFailed("Сервер принял TLS 1.2 — требование TLS 1.3-only не выполнено.")
    if any(marker in combined for marker in CONNECTIVITY_MARKERS):
        raise VerificationFailed("Проблема соединения, а не отказ TLS 1.2 — требование не подтверждено.")


def verify(arguments, runner):
    checks = []
    ensure_tls13_tool(runner, arguments.openssl)
    certificate = handshake_tls13(runner, arguments.openssl, arguments.host, arguments.port, arguments.ca,
                                  arguments.timeout)
    checks.append("TLS 1.3 handshake и доверие к CA подтверждены")
    check_certificate(runner, arguments.openssl, certificate, arguments.isu_id, arguments.timeout)
    checks.append(f"Издатель содержит O=ITMO и OU=MedSoft 2026/27 {arguments.isu_id}, срок действителен")
    ensure_tls12_rejected(runner, arguments.openssl, arguments.host, arguments.port, arguments.ca, arguments.timeout)
    checks.append("Сервер отклонил TLS 1.2")
    return checks


def main(argv=None, runner=default_runner):
    parser = argparse.ArgumentParser(description="Проверить учебный TLS-стенд лабораторной 2 без изменения доверия.")
    parser.add_argument("--host", required=True, help="DNS-имя или IP сервера.")
    parser.add_argument("--port", required=True, type=int, help="TLS-порт сервера.")
    parser.add_argument("--ca", required=True, help="Файл учебного корневого CA (только для проверки).")
    parser.add_argument("--isu-id", required=True, help="ИСУ ID из OU издателя; задаётся пользователем.")
    parser.add_argument("--openssl", default="openssl", help="Путь к OpenSSL с поддержкой TLS 1.3.")
    parser.add_argument("--timeout", type=float, default=20.0, help="Тайм-аут одного вызова OpenSSL в секундах.")
    arguments = parser.parse_args(argv)

    if not Path(arguments.ca).is_file():
        print(f"Неверный аргумент: файл CA не найден: {arguments.ca}", file=sys.stderr)
        return 2
    if arguments.port <= 0 or arguments.port > 65535:
        print("Неверный аргумент: порт вне диапазона 1–65535.", file=sys.stderr)
        return 2
    if not arguments.isu_id.strip():
        print("Неверный аргумент: укажите непустой --isu-id.", file=sys.stderr)
        return 2

    try:
        checks = verify(arguments, runner)
    except ToolError as error:
        print(f"Инструмент не подходит: {error}", file=sys.stderr)
        return 2
    except VerificationFailed as error:
        print(f"Стенд не прошёл проверку: {error}", file=sys.stderr)
        return 1

    print("TLS-стенд подтверждён:")
    for check in checks:
        print(f"  - {check}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
