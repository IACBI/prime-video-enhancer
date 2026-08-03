# Security policy

## Supported versions

Security fixes are applied to the latest GitHub Release and the current `main` branch. Older releases are not maintained unless a fix can be safely backported.

## Report a vulnerability privately

Please do **not** open a public issue for a suspected vulnerability or include account data, cookies, tokens, passwords, or personal information in a report.

Use this repository's private vulnerability-reporting flow in the GitHub **Security** tab. If that option is unavailable, contact the maintainers through an existing private channel before disclosing technical details publicly.

Include:

- a clear description of the issue and its security impact;
- affected versions and environment details;
- reproducible, minimal steps or a proof of concept;
- mitigation ideas, if you have them.

## Security model and data handling

### Desktop

- The Windows helper launches Microsoft Edge with a dedicated user-data directory.
- Its Chromium DevTools endpoint is bound to `127.0.0.1:9223`; it is not intended to be exposed on the network.
- Request filtering and controller injection run only in the Edge session started by the helper.

### Mobile

- The Flutter app applies request filtering only inside its embedded WebView.
- It does not configure a device-wide proxy, VPN, root certificate, or HTTPS interception service.

### Privacy boundaries

- The project does not include telemetry or an account-data upload feature.
- It does not intentionally read, store, or transmit Prime Video passwords, cookies, tokens, or viewing history.
- User interface preferences, such as playback speed and subtitle appearance, are stored locally in the relevant browser or WebView storage.
- The project does not bypass DRM or download protected media.

## Safe operation

Keep your operating system, Microsoft Edge, Android System WebView, and project dependencies current. Use official release assets only, avoid exposing the desktop debugging port, and review code before running modified local builds.
