# Changelog

All notable user-facing changes are documented here. Version tags and GitHub Releases are the authoritative distribution history.

## 3.6.8 — 2026-08-05

- **Android: fixed playback failing on every title.** The WebView denied the protected-media permission Prime Video needs to request a Widevine licence, so playback stopped with "Video Unavailable". Verified end to end on a physical device.
- Android: the enhancer script is now installed before the page runs and reinstalled across Prime Video's client-side navigations, so the panel is present on player pages instead of intermittently missing.
- Android: the hardware Back button now closes the panel menu, then walks the browsing history, and only leaves the app when there is nothing left to go back to.
- Touch support: the panel can be dragged with a finger, the control stays visible and tappable while idle, and buttons, colour swatches, and the size field meet a 44px minimum touch target.
- Touch layout: the menu is keyed to pointer type rather than screen width, so landscape keeps the mobile layout; it no longer overflows the right edge, scrolls when it does not fit, and leaves the picture visible in landscape.
- The panel now follows the video into fullscreen instead of disappearing, and Android rotates to landscape when fullscreen starts.
- Added a Skip Intro / Next button, previously reachable only through a keyboard shortcut and therefore unavailable on phones.
- Subtitle styling now keeps working while the player controls are on screen, and covers players that do not use the desktop caption class names.
- The panel no longer drifts off-screen when the speed label grows, when the device rotates, or when the on-screen keyboard opens.
- Ad and telemetry blocking is matched on hostname, and telemetry requests get an empty response instead of a fake ad document.
- Removed an unused `shared_preferences` dependency and a dead ProGuard rules file.

## 3.6.7 — 2026-08-03

- Align desktop package metadata, controller version checks, mobile package metadata, and regression checks.
- Replace stale mobile starter-test content with a project-specific smoke test.
- Improve the Android application label and refresh repository documentation for current release behaviour.
- Upgrade official GitHub Actions to Node 24-based releases to remove deprecated action-runtime warnings.

## 3.6.6 — 2026-08-03

- Restored successful Windows and Android release builds after the mobile controller import fix.
- Published Windows Light, Windows Standalone, and Android APK assets.

## Earlier releases

See the [GitHub Releases page](https://github.com/IACBI/prime-video-enhancer/releases) for prior published versions and generated release notes.
