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

- The Windows helper launches Microsoft Edge with a dedicated user-data directory and `--remote-debugging-pipe`, so it controls the browser over the DevTools protocol without opening a debugging port. The conversation runs over two anonymous pipe handles that only the Edge process the helper started inherits. No address or port exists that another process could connect to, so other Windows accounts on the same machine cannot reach the browser or read the signed-in Prime Video session through it.
- The browser is bound to the helper's lifetime by a job object, assigned before Edge starts running, so closing the helper closes the Prime Video window too. The shortcuts the helper installs never carry the debugging flags.
- If Edge relaunches itself (for example to finish an update), the relaunched browser has no pipe. The helper then exits and the window closes; start the app again.
- Request filtering and controller injection run only in the Edge session started by the helper.
- Code running as the same Windows user can still interfere with that user's own processes, including the helper and its browser, as with any application you run. The browser's profile folder is protected by your user account's file permissions, so keep those permissions intact.
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
- The release signing key is held in a protected GitHub Environment named `release`. The Android release job runs in that environment, so its signing secrets are released only to a run a maintainer has approved. `main` cannot be force-pushed or deleted, and only repository administrators can create, move or delete `v*` tags.

### Privacy boundaries

- The project does not include telemetry or an account-data upload feature.
- It does not intentionally read, store, or transmit Prime Video passwords, cookies, tokens, or viewing history.
- User interface preferences, such as playback speed and subtitle appearance, are stored locally in the relevant browser or WebView storage.
- The project does not bypass DRM or download protected media.

## Safe operation

Keep your operating system, Microsoft Edge, Android System WebView, and project dependencies current. Use official release assets only, verify them as described above, and review code before running modified local builds. Close the helper when you are done watching. On a shared computer, other accounts cannot reach the helper's browser, but code running under your own account can still affect it, so do not run untrusted programs as that account.
