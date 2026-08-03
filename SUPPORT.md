# Support

## Start here

1. Install the latest asset from the [GitHub Releases page](https://github.com/IACBI/prime-video-enhancer/releases/latest).
2. On Windows, choose **Standalone** if you do not have the .NET 8 Desktop Runtime; otherwise the smaller **Light** package is suitable.
3. Confirm that Microsoft Edge is installed and up to date.
4. Start a supported Prime Video title and wait for the floating control to appear.

## Common questions

### The control does not appear

- Confirm that the app opened its own Edge window rather than an existing browser tab.
- Start playback and wait briefly for a video element to become available.
- Reload the page or restart the helper after a Prime Video page update.
- Check that no security product or local policy prevents Edge from starting with a local debugging endpoint.

### The Android APK will not install or load content

- Download the APK only from the repository's [GitHub Releases page](https://github.com/IACBI/prime-video-enhancer/releases/latest).
- Review Android's install-source prompt and ensure your device has enough storage.
- Update Android System WebView and Chrome, then retry.
- WebView playback and service availability vary by device, account, plan, and region; the project cannot guarantee compatibility for every title.

### A playback, subtitle, or request-filtering feature stopped working

Prime Video can change its website and player markup at any time. Before reporting a problem, try the latest release and include the affected platform, app version, browser/WebView version, region, and reproducible steps. Never include passwords, cookies, tokens, or personal account data.

## Open an issue

Use the issue forms supplied by the repository for bugs and feature requests. Search existing reports first, keep one problem per issue, and attach only redacted screenshots or logs.

For suspected security issues, use [SECURITY.md](SECURITY.md) rather than a public issue.
