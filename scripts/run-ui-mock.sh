#!/usr/bin/env bash
set -euo pipefail

medsoft_script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run \
  --project "$medsoft_script_dir/../src/Presentation/HospitalInformationSystem.Web" \
  --launch-profile ui-mock \
  -p:WasmApplicationEnvironmentName=UiMock \
  "$@"
