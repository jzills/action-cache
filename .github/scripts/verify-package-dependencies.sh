#!/usr/bin/env bash
#
# The point of splitting the packages is that a consumer who wants in-memory caching does
# not inherit the Cosmos SDK, SqlClient, StackExchange.Redis or Newtonsoft. That is easy to
# regress by adding one ProjectReference, so it is asserted rather than assumed.

set -euo pipefail

NUPKG_DIR="${1:-./nupkg}"
FORBIDDEN=("StackExchange.Redis" "Microsoft.Data.SqlClient" "Microsoft.Azure.Cosmos" "Newtonsoft.Json")

CORE_NUPKG=$(find "$NUPKG_DIR" -name 'ActionCache.[0-9]*.nupkg' -not -name '*.symbols.nupkg' | head -1)

if [[ -z "$CORE_NUPKG" ]]; then
  echo "No ActionCache core package found in $NUPKG_DIR" >&2
  exit 1
fi

echo "Inspecting $(basename "$CORE_NUPKG")"

# A .nupkg is a zip; python is always present on the runners and avoids depending on unzip.
DEPS=$(python3 - "$CORE_NUPKG" <<'PYTHON'
import re, sys, zipfile

with zipfile.ZipFile(sys.argv[1]) as package:
    nuspec = next(name for name in package.namelist() if name.endswith('.nuspec'))
    content = package.read(nuspec).decode('utf-8')

for dependency in sorted(set(re.findall(r'<dependency id="([^"]+)"', content))):
    print(dependency)
PYTHON
)

echo "Declared dependencies:"
echo "${DEPS:-  (none)}" | sed 's/^/  /'

FAILED=0
for package in "${FORBIDDEN[@]}"; do
  if echo "$DEPS" | grep -qx "$package"; then
    echo "FAIL: ActionCache must not depend on $package — it belongs to a backend package." >&2
    FAILED=1
  fi
done

if [[ "$FAILED" -ne 0 ]]; then
  exit 1
fi

echo "OK: the core package carries no backend dependencies."
