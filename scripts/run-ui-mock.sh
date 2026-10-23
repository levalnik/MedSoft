#!/usr/bin/env bash
set -euo pipefail

for medsoft_argument in "$@"; do
  if [[ "$medsoft_argument" == "--no-build" ]]; then
    printf '%s\n' 'UiMock требует сборки среды браузера. Уберите --no-build.' >&2
    exit 2
  fi
done

medsoft_script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run \
  --project "$medsoft_script_dir/../src/Presentation/HospitalInformationSystem.Web" \
  --launch-profile ui-mock \
  -p:WasmApplicationEnvironmentName=UiMock \
  "$@"
