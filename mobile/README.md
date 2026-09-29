# Prime Video Enhancer mobile app

The mobile app is a Flutter project that opens Prime Video in an embedded WebView. It runs the same speed and subtitle controller as the desktop app, with its preferences stored in the WebView, and applies best-effort filtering to selected ad-related and telemetry requests within that WebView.

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
- Uses `shouldInterceptRequest` to answer known ad requests with an empty VAST document and telemetry requests with an empty `204`, so neither reaches Amazon.
- Uses immersive mode while the WebView enters fullscreen.
- Keeps the WebView on `primevideo.com` and Amazon pages: any other link is refused with a short notice, and only those sites can receive the protected-media permission.

The app does not install a VPN, proxy, root certificate, or system-wide request blocker. It does not collect account credentials or transmit telemetry.

## Build an Android APK

Requirements:

- Flutter 3.47.1 (the version CI builds with)
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
├── lib/main.dart            # WebView, script injection, fullscreen handling
├── lib/web_rules.dart       # Ad/telemetry request rules and the user agent
├── android/                 # Android host project
├── ios/                     # iOS host project
├── test/                    # Unit tests for the request rules, widget smoke test
└── pubspec.yaml             # Package metadata and dependencies
```

## Notes for maintainers

- **Request filtering has a cost.** `shouldInterceptRequest` sends every request the WebView makes through the platform channel to Dart. Measured on an Android 16 emulator against the real site, a request answered by the Dart handler took about 22 ms against about 6.5 ms for a local fetch, roughly 16 ms more sequentially and 3.6 ms more when requests overlap. That is noticeable while a page loads and irrelevant for video segments. The plugin skips its native content blockers whenever this callback is on, and a native block returns an empty body rather than the VAST document ad calls get today, so it is not a drop-in replacement.
- **Android toolchain.** The project sits on Flutter's minimum supported versions (Gradle 8.14, Android Gradle Plugin 8.11.1, Kotlin 2.2.20); Flutter 3.47.1 warns and asks for 9.1.0, 9.0.1 and 2.3.20. CI pins Flutter, so nothing breaks until that pin moves. The migration was tried: Gradle 9.1.0 and the two plugin versions resolve, the app's own files need Flutter's current template (drop `kotlin-android`, move the JVM target to a top-level `kotlin { compilerOptions { ... } }`, and set `android.newDsl=false` and `android.builtInKotlin=false` in `gradle.properties`), but `flutter_inappwebview_android` 1.1.3 then fails because it still uses `proguard-android.txt`, which AGP 9 rejects. Only the 1.2.0 beta line fixes that, so do this when a stable release of the plugin ships, then run `PrimeVideoSpeedApp.Tests/android-e2e.js`.
- **Tool edits.** `flutter analyze` and `flutter build` rewrite `analysis_options.yaml` and `android/gradle.properties` in place; revert them unless the change is intended.

## Keeping the controller in sync

`../speed-control.js` and `assets/speed-control.js` are intentionally kept in sync. When changing the controller, update both copies and the corresponding version check in the desktop project, then run the checks listed in [CONTRIBUTING.md](../CONTRIBUTING.md).
