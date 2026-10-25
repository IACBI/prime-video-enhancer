#!/usr/bin/env bash
# Decodes the release keystore for the Android build and decides whether this
# run may go ahead without one.
#
# Inputs (env): PVSC_RELEASE_KEYSTORE_B64, PVSC_RELEASE_KEYSTORE_PASSWORD,
# PVSC_RELEASE_KEY_ALIAS, PVSC_RELEASE_KEY_PASSWORD, IS_TAG ("true" on a tag),
# RUNNER_TEMP, GITHUB_OUTPUT.
# Outputs: path=<keystore file or empty>, require=<true|false>.
set -euo pipefail

out="${GITHUB_OUTPUT:?GITHUB_OUTPUT is not set}"

if [ "${IS_TAG:-false}" = "true" ]; then
  # A tag publishes. A debug-signed APK cannot update the release before it, so
  # a missing secret must stop the release, not quietly change its certificate.
  missing=()
  for name in PVSC_RELEASE_KEYSTORE_B64 PVSC_RELEASE_KEYSTORE_PASSWORD PVSC_RELEASE_KEY_ALIAS PVSC_RELEASE_KEY_PASSWORD; do
    [ -n "${!name:-}" ] || missing+=("$name")
  done
  if [ "${#missing[@]}" -gt 0 ]; then
    echo "::error::Tagged release needs the stable signing key; missing secrets in the release environment: ${missing[*]}"
    exit 1
  fi
  echo "require=true" >> "$out"
else
  echo "require=false" >> "$out"
fi

if [ -n "${PVSC_RELEASE_KEYSTORE_B64:-}" ]; then
  keystore="${RUNNER_TEMP:?RUNNER_TEMP is not set}/release.keystore"
  printf '%s' "$PVSC_RELEASE_KEYSTORE_B64" | base64 -d > "$keystore"
  if [ ! -s "$keystore" ]; then
    echo "::error::PVSC_RELEASE_KEYSTORE_B64 decoded to an empty file."
    exit 1
  fi
  echo "path=$keystore" >> "$out"
else
  echo "No release keystore configured; this untagged build will be debug-signed and must not be published."
  echo "path=" >> "$out"
fi
