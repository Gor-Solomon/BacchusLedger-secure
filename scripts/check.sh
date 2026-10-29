#!/usr/bin/env bash
# Refactor safety net: starts the app on a fresh temporary database, calls /api/selftest and every
# endpoint in scripts/endpoints.txt, and compares status code + raw body with docs/refactor/baseline/.
#
#   scripts/check.sh --baseline   # record the baseline
#   scripts/check.sh              # compare; exit code 1 on any difference
# Options: --project <dir> (default src/BacchusLedger.Api), --port <n> (default 5199), --no-build
set -euo pipefail

BASELINE=0; PROJECT="src/BacchusLedger.Api"; PORT=5199; BUILD=1
while [ $# -gt 0 ]; do
  case "$1" in
    --baseline|-Baseline) BASELINE=1 ;;
    --project) PROJECT="$2"; shift ;;
    --port) PORT="$2"; shift ;;
    --no-build|-NoBuild) BUILD=0 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
  shift
done

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT_DIR="$ROOT/$PROJECT"
PROJECT_NAME="$(basename "$PROJECT_DIR")"
DLL="$PROJECT_DIR/bin/Debug/net8.0/$PROJECT_NAME.dll"
BASELINE_DIR="$ROOT/docs/refactor/baseline"
BASE="http://127.0.0.1:$PORT"

if [ "$BUILD" = 1 ]; then
  dotnet build "$ROOT/BacchusLedger.sln" -c Debug --nologo -v q || { echo "BUILD FAILED"; exit 1; }
fi

TMP="$(mktemp -d "${TMPDIR:-/tmp}/bacchus-check-XXXXXX")"
if [ "$BASELINE" = 1 ]; then OUT="$BASELINE_DIR"; rm -rf "$BASELINE_DIR"; else OUT="$TMP/actual"; fi
mkdir -p "$OUT"

APP_PID=""
FAILED=0
cleanup() {
  if [ -n "$APP_PID" ]; then kill "$APP_PID" 2>/dev/null || true; wait "$APP_PID" 2>/dev/null || true; fi
  if [ "$FAILED" = 0 ]; then rm -rf "$TMP"; else echo "Actual responses kept in $OUT"; fi
}
trap cleanup EXIT

# Fresh, deterministic environment for the child process (AI keys blanked so the assistant status is stable).
(
  cd "$PROJECT_DIR"
  unset OPENAI_API_KEY GEMINI_API_KEY ASSISTANT_PROVIDER ASSISTANT_ENABLED OPENAI_MODEL GEMINI_MODEL ASPNETCORE_HTTP_PORTS
  export DB_PATH="$TMP/bacchus.db" RESET_ON_START=true SIGNING_KEY_PATH="$TMP/demo-key.pem" \
         ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="$BASE"
  exec dotnet "$DLL"
) > "$TMP/app.log" 2>&1 &
APP_PID=$!

UP=0
for _ in $(seq 1 120); do
  if curl -sf -o /dev/null "$BASE/health"; then UP=1; break; fi
  kill -0 "$APP_PID" 2>/dev/null || break
  sleep 0.5
done
if [ "$UP" != 1 ]; then echo "App did not start. Log:"; tail -n 30 "$TMP/app.log"; FAILED=1; exit 1; fi

MANIFEST="$OUT/manifest.txt"
: > "$MANIFEST"
COUNT=0
while read -r name path; do
  case "$name" in ''|'#'*) continue ;; esac
  status="$(curl -s -o "$OUT/$name.json" -w '%{http_code}' "$BASE$path")"
  printf '%s %s %s\n' "$name" "$status" "$path" >> "$MANIFEST"
  COUNT=$((COUNT + 1))
done < <(tr -d '\r' < "$SCRIPT_DIR/endpoints.txt")

SELF="$(head -c 200 "$OUT/selftest.json")"
echo "selftest: $(printf '%s' "$SELF" | grep -o '"pass":[a-z]*,"passed":[0-9]*,"total":[0-9]*' || echo "$SELF")"

if [ "$BASELINE" = 1 ]; then echo "Baseline saved to docs/refactor/baseline ($COUNT endpoints)."; exit 0; fi

snip() { # file, byte offset -> 200 bytes around the offset
  local from=$(( $2 > 80 ? $2 - 80 : 0 ))
  tail -c +"$((from + 1))" "$1" | head -c 200
}

declare -A EXPECTED=()
while read -r name status _; do [ -n "$name" ] && EXPECTED["$name"]="$status"; done < <(tr -d '\r' < "$BASELINE_DIR/manifest.txt")
declare -A SEEN=()
while read -r name status path; do
  SEEN["$name"]=1
  bf="$BASELINE_DIR/$name.json"; af="$OUT/$name.json"
  if [ -z "${EXPECTED[$name]:-}" ] || [ ! -f "$bf" ]; then echo "FAIL $path : not in baseline"; FAILED=$((FAILED + 1)); continue; fi
  status_ok=1; body_ok=1
  [ "${EXPECTED[$name]}" = "$status" ] || status_ok=0
  cmp -s "$bf" "$af" || body_ok=0
  if [ "$status_ok" = 1 ] && [ "$body_ok" = 1 ]; then echo "PASS $path"; continue; fi
  FAILED=$((FAILED + 1))
  echo "FAIL $path"
  [ "$status_ok" = 1 ] || echo "  status: expected ${EXPECTED[$name]}, got $status"
  if [ "$body_ok" = 0 ]; then
    at="$(cmp "$bf" "$af" 2>&1 | sed -n 's/.*differ: [a-z]* \([0-9]*\).*/\1/p' || true)"
    if [ -n "$at" ]; then at=$((at - 1)); else at=$(( $(wc -c < "$bf") < $(wc -c < "$af") ? $(wc -c < "$bf") : $(wc -c < "$af") )); fi
    echo "  body differs at byte $at (expected $(wc -c < "$bf" | tr -d ' ') bytes, got $(wc -c < "$af" | tr -d ' '))"
    echo "  expected: ...$(snip "$bf" "$at")..."
    echo "  actual:   ...$(snip "$af" "$at")..."
  fi
done < "$MANIFEST"
for name in "${!EXPECTED[@]}"; do
  [ -n "${SEEN[$name]:-}" ] || { echo "FAIL $name : in baseline but not checked"; FAILED=$((FAILED + 1)); }
done

if [ "$FAILED" -gt 0 ]; then echo "$FAILED endpoint(s) differ from the baseline."; exit 1; fi
echo "ALL PASS ($COUNT endpoints identical to the baseline)."
