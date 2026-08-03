# Prime Video Enhancer mobile app

The mobile app is a Flutter project that opens Prime Video in an embedded WebView. It shares the desktop controller's playback-speed and subtitle preferences, and applies best-effort filtering to selected ad-related and telemetry requests within that WebView.

For desktop downloads, privacy information, and the general quick start, see the [repository README](../README.md).

## Availability

| Platform | Status |
| --- | --- |
| Android | Release APKs are published on the [latest GitHub Release](https://github.com/IACBI/prime-video-enhancer/releases/latest). |
| iOS | Source files are included under `ios/`; signed iOS builds, TestFlight distribution, and App Store delivery are not provided by this repository. |

WebView playback support depends on the device, operating-system WebView, account, region, and Prime Video itself. The project does not promise DRM playback, content availability, or request-filtering results on any particular device.

## What the mobile app does

- Loads `https://www.primevideo.com` in `flutter_inappwebview`.
- Injects the bundled `assets/speed-control.js` at document start and reinjects it after navigation when necessary.
- Offers playback speeds from `0.25x` to `4x` and locally stored subtitle preferences when the target page exposes compatible elements.
- Uses `shouldInterceptRequest` to return an empty response for a small set of ad-related or telemetry URL patterns.
- Uses immersive mode while the WebView enters fullscreen.

The app does not install a VPN, proxy, root certificate, or system-wide request blocker. It does not collect account credentials or transmit telemetry.

## Build an Android APK

Requirements:

- Flutter 3.x
- Android SDK and a supported emulator or device for testing
- Java 17

```bash
cd mobile
flutter pub get
flutter analyze
flutter test
flutter build apk --release
```

The release APK is written to:

```text
build/app/outputs/flutter-apk/app-release.apk
```

Install only APKs you trust. On Android, enabling installation from an unknown source is a device-level decision; review the platform warning before proceeding.

## Build for iOS

Use macOS with Xcode and a configured Apple developer account:

```bash
cd mobile
flutter pub get
flutter build ipa --release
```

You are responsible for bundle identifiers, signing certificates, provisioning profiles, and any TestFlight or App Store submission. Validate Prime Video and WebView behaviour on physical devices before distribution.

## Project structure

```text
mobile/
├── assets/speed-control.js  # Mobile copy of the shared controller
├── lib/main.dart            # WebView, local request filtering, fullscreen handling
├── android/                 # Android host project
├── ios/                     # iOS host project
├── test/                    # Flutter smoke tests
└── pubspec.yaml             # Package metadata and dependencies
```

## Keeping the controller in sync

`../speed-control.js` and `assets/speed-control.js` are intentionally kept in sync. When changing the controller, update both copies and the corresponding version check in the desktop project, then run the checks listed in [CONTRIBUTING.md](../CONTRIBUTING.md).
