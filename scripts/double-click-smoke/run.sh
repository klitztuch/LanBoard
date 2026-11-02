#!/usr/bin/env bash
# Regression smoke test for issue #45: same-tick double-clicks on "Set active" must neither crash the
# circuit nor corrupt pooled Npgsql connections. Requires a running AppHost in Development and at least
# two parties.
#
# Usage: scripts/double-click-smoke/run.sh [base-url] [runs]
set -euo pipefail

BASE_URL="${1:-https://localhost:7155}"
RUNS="${2:-30}"
PATTERNS=('second operation' 'ArgumentOutOfRangeException' 'Exception while reading from stream')

dir="$(cd "$(dirname "$0")" && pwd)"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

[[ -d "$dir/node_modules" ]] || npm ci --prefix "$dir" --silent
if [[ -z "${CHROMIUM_PATH:-}" ]]; then
    if command -v chromium > /dev/null; then
        export CHROMIUM_PATH="$(command -v chromium)"
    else
        (cd "$dir" && npx playwright install chromium)
    fi
fi

web_logs() { aspire logs web --non-interactive --nologo 2>/dev/null || true; }

lines_before="$(web_logs | wc -l)"

echo "Double-clicking 'Set active' $RUNS times at $BASE_URL"
failed=0
node "$dir/smoke.mjs" "$BASE_URL" "$RUNS" || failed=1

# Give in-flight work and the log pipeline a moment to settle.
sleep 3
web_logs | tail -n "+$((lines_before + 1))" > "$work_dir/new.log"

hits=0
echo "New log errors:"
for p in "${PATTERNS[@]}"; do
    n="$(grep -c "$p" "$work_dir/new.log" || true)"
    printf '  %-40s %s\n' "$p" "$n"
    hits=$((hits + n))
done

if ((failed || hits > 0)); then
    echo "FAIL"
    exit 1
fi
echo "OK"
