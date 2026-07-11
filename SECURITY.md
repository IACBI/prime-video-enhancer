# Security Policy

## Scope

Prime Video Enhancer (Speed, Subtitle & Ad Shield) is a local Windows helper application. It launches Microsoft Edge with a dedicated profile (`--user-data-dir`) and restricts its remote debugging interface strictly to the local loopback adapter (`--remote-debugging-address=127.0.0.1` on port `9223`).

## Supported Versions

Only the latest public version is supported and maintained.

## Reporting A Vulnerability

Please open a GitHub issue with a clear description, reproduction steps, and expected impact. Do not include private account data, cookies, tokens, or credentials.

## Security & Privacy Notes

- The project does not collect or transmit telemetry.
- The project does not transmit Prime Video account data, credentials, cookies, tokens, or viewing history.
- The project does not bypass DRM or download any protected video content.
- **4-Layer Zero-Visibility Ad Shield Security & Privacy:**
  - **CDP Network Blocking (`Network.setBlockedURLs`):** Intercepts and blocks ad servers (`amazon-adsystem.com`), telemetry (`a2z.com/telemetry`), and ad-tracking domains locally at the Chromium network layer inside the isolated Edge instance. No external proxy or VPN is installed.
  - **DOM Blackout & Fast-Forward:** Works entirely within the browser DOM using `MutationObserver`, `HTMLMediaElement.click()`, and playback acceleration (`playbackRate = 16`). Does not inject untrusted third-party binaries or intercept HTTPS system certificates.
- Local browser preferences are stored strictly in the isolated local Edge profile's `localStorage`:
  - `primeVideoSpeedControl.speed` (playback speed setting)
  - `primeVideoSpeedControl.position` (button UI coordinate setting)
  - `primeVideoSpeedControl.subtitleColor` (selected subtitle color hex code, default `#FFCC00`)
  - `primeVideoSpeedControl.subtitleEnabled` (boolean flag for subtitle styling override)
- All network interactions occur over local loopback (`127.0.0.1`) with a 5-second timeout and strict cancellation tokens to prevent resource exhaustion or memory leaks.
