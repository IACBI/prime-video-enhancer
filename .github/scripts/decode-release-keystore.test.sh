#!/usr/bin/env bash
# Checks that a tagged build cannot proceed without the release signing secrets.
set -uo pipefail

script="$(dirname "$0")/decode-release-keystore.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fails=0

# run <name> <expected exit> <expected output line or ""> [VAR=value...]
run() {
  local name="$1" want="$2" line="$3"; shift 3
  : > "$work/out"
  env -i PATH="$PATH" RUNNER_TEMP="$work" GITHUB_OUTPUT="$work/out" "$@" bash "$script" > "$work/log" 2>&1
  local got=$?
  if [ "$got" -ne "$want" ] || { [ -n "$line" ] && ! grep -qx "$line" "$work/out"; }; then
    echo "FAIL: $name (exit $got, want $want)"; cat "$work/log" "$work/out"; fails=$((fails + 1))
  else
    echo "ok: $name"
  fi
}

ks="$(printf 'not-a-real-keystore' | base64)"
full=(PVSC_RELEASE_KEYSTORE_B64="$ks" PVSC_RELEASE_KEYSTORE_PASSWORD=p PVSC_RELEASE_KEY_ALIAS=a PVSC_RELEASE_KEY_PASSWORD=k)

run "tag without keystore fails"   1 "" IS_TAG=true
run "tag without password fails"   1 "" IS_TAG=true PVSC_RELEASE_KEYSTORE_B64="$ks" PVSC_RELEASE_KEY_ALIAS=a PVSC_RELEASE_KEY_PASSWORD=k
run "tag with all secrets signs"   0 "require=true" IS_TAG=true "${full[@]}"
run "tag keystore path is set"     0 "path=$work/release.keystore" IS_TAG=true "${full[@]}"
run "untagged without keystore ok" 0 "require=false" IS_TAG=false
run "untagged path is empty"       0 "path=" IS_TAG=false

[ "$fails" -eq 0 ] || { echo "$fails check(s) failed"; exit 1; }
