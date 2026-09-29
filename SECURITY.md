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
- It drives that browser over a Chromium DevTools endpoint on `127.0.0.1` (port 9223, or a free port when another program holds it). The endpoint is never reachable from the network, but **it has no authentication**, and Windows applies no per-user restriction to loopback connections. While the helper runs, any process on the same machine — including one belonging to a different Windows account — can connect to it and control the browser, which means reading the cookies of the signed-in Prime Video session. Treat a shared or multi-user machine accordingly.
- The helper only follows browser-tab addresses that point back at that same local port, so another process holding the port cannot redirect it to a different host.
- The endpoint exists only for as long as the helper runs. The helper binds the browser to its own lifetime, so closing the helper closes the Prime Video window too, and the shortcuts it installs never carry the debugging flags.
- Request filtering and controller injection run only in the Edge session started by the helper.
- Released builds are single-file executables. They use the script and icon compiled into the executable and deliberately ignore a `speed-control.js` or `Assets\generate-app-icon.ps1` placed next to it, so putting the executable in a shared folder cannot turn those files into a way to run someone else's code.

### Mobile

- The Flutter app applies request filtering only inside its embedded WebView.
- The WebView only navigates to `primevideo.com` and Amazon pages; a link to anywhere else is refused, and only those sites can receive the protected-media (DRM) permission. The app has no address bar, so a page from elsewhere would otherwise look exactly like Prime Video.
- It does not configure a device-wide proxy, VPN, root certificate, or HTTPS interception service.
- Release APKs are signed with a stable release key. Before installing, check the download against `SHA256SUMS.txt` on the release page, and confirm the signing certificate matches earlier releases:

  ```
  apksigner verify --print-certs PrimeVideoSpeedApp-Mobile.apk
  ```

  Releases `v3.6.6` through `v3.7.0` were signed with throwaway debug keys and cannot be verified this way; installing a later, properly signed release over one of them requires uninstalling it first, which clears the app's stored session.

### Privacy boundaries

- The project does not include telemetry or an account-data upload feature.
- It does not intentionally read, store, or transmit Prime Video passwords, cookies, tokens, or viewing history.
- User interface preferences, such as playback speed and subtitle appearance, are stored locally in the relevant browser or WebView storage.
- The project does not bypass DRM or download protected media.

## Safe operation

Keep your operating system, Microsoft Edge, Android System WebView, and project dependencies current. Use official release assets only, verify them as described above, and review code before running modified local builds. Close the helper when you are done watching, and be aware that on a shared computer anyone with their own account can reach the debugging endpoint while it runs.
