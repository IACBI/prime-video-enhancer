# Changelog

All notable user-facing changes are documented here. Version tags and GitHub Releases are the authoritative distribution history.

## 3.7.0 — 2026-08-06

- **Subtitles no longer flash white before taking your colour.** Styling was applied by JavaScript after each new line already existed, which is one frame too late by construction. Subtitle appearance is now described in a stylesheet, so a new line is drawn in your colour from its very first frame.
- **Changing playback speed no longer stalls the picture.** The speed is written once, in the order that costs the audio pipeline least, and the storage write and panel redraw that used to run in the same step as the change have been moved out of it.
- Added a Pitch control. Leave it on to keep voices sounding natural; turn it off for noticeably smoother speed changes. It is always off while the ad shield is running.
- Subtitle size is now measured against the picture instead of the window, so it stays readable in landscape and no longer jumps when you enter or leave fullscreen. Previously the default rendered at around 10px on a phone held sideways.
- **Rebuilt the control panel.** Three deliberate layouts — an anchored menu on desktop, a bottom sheet in portrait, and a two-column side sheet in landscape that fits without scrolling. Opening the panel in landscape used to bury the button that closes it.
- The panel now follows rotation and window resizing instead of deciding its layout once at startup.
- Much lighter on battery and CPU: a self-sustaining 20-per-second styling loop is gone, and the remaining background work runs once a second during playback, pauses when the app is in the background, and speeds up only while an ad is being skipped.
- Android: camera, microphone, and location requests from web pages are now refused. Only the protected-media permission Prime Video needs for playback is granted.
- Android: ad and telemetry blocking now covers the same hosts as the desktop app, which previously blocked around forty and Android two.
- Fixed the live browser test, which had been pinned to an old version number and was failing before it reached any of its checks.

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
