# Changelog

All notable user-facing changes are documented here. Version tags and GitHub Releases are the authoritative distribution history.

## Unreleased

- **Each title remembers its own speed.** A title reopens at the speed you last set on it, whether you come back to it inside the app or after a restart; a title you have not set a speed on opens at the speed you were using, as before. The last 100 titles are kept. Titles are told apart by the id in the page address, so an episode that Prime Video gives an address of its own counts as its own title.
- **New in the panel:** an Auto-skip switch (on by default, as before), a sleep timer (15 to 90 minutes, waits out an ad break), a running total of the time faster playback saved, and a Raise control that lifts subtitles by up to 40% of the picture height. The panel follows the browser language (English or Turkish), and ad countdowns and skip buttons are recognised in more of Prime Video's languages.
- **Desktop: lighter and sturdier.** Each Prime Video tab now has one persistent connection that injects the controller when a page finishes loading and answers ad requests, instead of reconnecting to every tab every two seconds. If port 9223 is taken the helper uses a free one instead of failing, and says so when Edge was already running for the profile. `PVSC_DATA_DIR` moves the profile and icon cache.
- **Android: the WebView stays on Amazon and Prime Video.** Links to anywhere else are refused with a short notice, and only those sites can receive the protected-media permission. The controller now also installs when it is injected before the page exists, which used to fail until a later injection.
- **Lighter during playback: the subtitle stylesheet is no longer rewritten every second.** The controller reapplied the same stylesheet on every background tick, which makes the browser re-resolve style for the whole page each time even though nothing changed. It now writes only when the rules differ.
- Desktop: the helper only connects to browser-tab addresses on its own local debugging port. Before, whatever answered on that port decided where it connected next. Windows are also no longer skipped for icon updates just because their title contains text such as `.js` or `Cursor`.
- Tests: a new headless-browser test runs the controller for real (install, subtitles, speed keys, the ad shield, teardown), and CI now runs the desktop tests, that test, and the Android analysis and tests on every push and pull request instead of only when a release is tagged.
- **The ad shield actually speeds ads up now.** It asked for 30x, which Chromium (Edge and Android WebView) rejects above 16x, so ads played at your normal speed behind the black cover, and the error also stopped the shield from noticing when the ad ended. Ads now run at 16x, falling back to 8x if playback stalls.
- **Much lighter during playback.** Every progress-bar update used to trigger a full scan of the page for ad markers, around fifteen a second; only changes that can reveal an ad do now. Script time in a measured player page dropped by roughly three quarters.
- **Desktop: fixed memory corruption in the window-icon code**, which wrote past a structure on every refresh.
- Desktop: a request-blocking layer that only lasted a few milliseconds per check has been removed; all blocking now goes through the interceptor, which also starts earlier, so ad requests during the first page load are caught. Regional Amazon ad hosts such as `unagi.amazon.com.tr` are now blocked too.
- Desktop: works behind an HTTP proxy, exits by itself when you close the Prime Video window, and a second launch opens another window instead of starting a competing helper.
- Keyboard shortcuts no longer take over `Ctrl` combinations such as `Ctrl`+`S` or the zoom keys.
- Generic "skip" buttons are only clicked while an ad is showing, so a player control such as "skip forward 10 seconds" is never pressed by mistake. A stuck detection released by the safety valve is no longer counted as a blocked ad.
- Android: the WebView's data, including your signed-in session, is excluded from cloud backup and device transfer. Ad and telemetry filtering now matches the desktop app's host rules, which stops it from catching unrelated sites and adds missing regional Amazon hosts. The browser identity follows the installed WebView instead of a fixed 2023 Chrome version.
- **Android: the APK download is less than half the size** (42.6 MB → 20.0 MB), still supporting every device it did before.
- **Desktop: the network ad blocker no longer switches itself off during quiet stretches.** After 30 seconds without an ad request it tore down its own connection to the tab and stayed off until the next check restarted it, so ads requested in that gap went through.
- Updating the controller while Prime Video is open no longer risks leaving a second, unresponsive panel on the page.
- The ads-blocked counter recovers from a corrupted saved value instead of showing "NaN".
- **Desktop: closing the helper now closes the Prime Video window too**, and the browser's debugging endpoint exists only while the helper runs. Shortcuts no longer carry the debugging flags; the Start Menu entry now starts the helper, and the older shortcuts that re-armed the endpoint on every click are disarmed or removed on the next run.
- Desktop release builds ignore a `speed-control.js` or `Assets\generate-app-icon.ps1` placed next to the executable, and system tools are started by absolute path.
- **Android releases are signed with a stable release key** and published with `SHA256SUMS.txt`, so an update installs over the previous version and a download can be verified. See [SECURITY.md](SECURITY.md) for checking the certificate; moving from a debug-signed release (3.6.6–3.7.0) needs one uninstall.
- Android: a page can no longer swallow the Back button by claiming to close a menu; it is honoured at most once per touch.
- Android: the controller is no longer resent to the page on every in-app navigation when it is already installed, and release builds no longer copy Prime Video's web console output into the device log.

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
