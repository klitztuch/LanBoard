#!/usr/bin/env bash
# Regression smoke test for issue #44: aborted prerender requests must not corrupt pooled Npgsql
# connections. Requires a running AppHost (`dotnet run --project LanBoard.AppHost`) in Development.
#
# Usage: scripts/prerender-abort-smoke.sh [base-url] [aborted-request-count]
set -euo pipefail

BASE_URL="${1:-https://localhost:7155}"
COUNT="${2:-300}"
BATCH=50
PATTERN='BindComplete|too many clients|Live reload .* failed|Reloading attendees after a live update failed'

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT
jar="$work_dir/cookies"

count_errors() {
    aspire logs web --non-interactive --nologo 2>/dev/null | grep -cE "$PATTERN" || true
}

# Prints per-pattern counts of the log lines added since the given line count.
report() {
    local skip="$1"
    aspire logs web --non-interactive --nologo 2>/dev/null | tail -n "+$((skip + 1))" > "$work_dir/new.log" || true
    for p in 'BindComplete' 'too many clients' 'Live reload .* failed' 'Reloading attendees after a live update failed'; do
        printf '  %-50s %s\n' "$p" "$(grep -cE "$p" "$work_dir/new.log" || true)"
    done
}

lines_before="$(aspire logs web --non-interactive --nologo 2>/dev/null | wc -l)"
errors_before="$(count_errors)"

echo "Dev-login as Alice at $BASE_URL"
curl -sk -c "$jar" -b "$jar" -o /dev/null -L "$BASE_URL/auth/dev-login?name=Alice"

echo "Firing $COUNT aborted /attendees requests"
for ((i = 0; i < COUNT; i++)); do
    # Abort after 4–24 ms, mostly mid-prerender.
    timeout_ms=$((4 + RANDOM % 21))
    curl -sk -b "$jar" -o /dev/null --max-time "0.$(printf '%03d' "$timeout_ms")" "$BASE_URL/attendees" &
    if (((i + 1) % BATCH == 0)); then wait || true; fi
done
wait || true

echo "Firing 20 regular requests"
failed=0
pages=(/ /attendees /parties /tv /settings)
for ((i = 0; i < 20; i++)); do
    page="${pages[i % ${#pages[@]}]}"
    status="$(curl -sk -b "$jar" -o /dev/null -w '%{http_code}' "$BASE_URL$page")"
    if [[ "$status" != "200" ]]; then
        echo "  $page -> $status"
        failed=1
    fi
done

# Give in-flight work and the log pipeline a moment to settle.
sleep 3
errors_after="$(count_errors)"
new_errors=$((errors_after - errors_before))

echo "New log errors: $new_errors"
report "$lines_before"

if ((failed || new_errors > 0)); then
    echo "FAIL"
    exit 1
fi
echo "OK"
