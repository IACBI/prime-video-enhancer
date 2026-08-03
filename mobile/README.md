# Prime Video Enhancer — Mobile App (Android & iOS)

This is the mobile adaptation of **Prime Video Enhancer**, built with **Flutter** and `flutter_inappwebview`. It brings the exact same 5-Layer Ad Shield, custom speed controls (`0.25x` to `4.0x`), and personalized subtitle styling (color, size, background) to mobile devices.

---

## 📱 Features

- **Network-Level Mobile Ad Shield:** Intercepts and blocks Amazon ad/telemetry host requests (`amazon-adsystem.com`, `unagi`, `mads`, `aan.amazon.co.*`) directly inside the mobile WebView.
- **Shared Controller Engine:** Uses the shared `speed-control.js` engine for 100% feature parity with the Desktop application.
- **Touch-Optimized Bottom Sheet UI:** Tapping the compact floating badge (`1.2x ⚡`) slides up a mobile-friendly bottom sheet menu designed for single-thumb touch operation.
- **Full-Screen Playback Support:** Automatic sticky immersive mode during video fullscreen.
- **DRM Playback Ready:** Built on native mobile WebViews with Widevine L3 / FairPlay DRM support.

---

## 🛠️ Building the Mobile App

### Requirements

- [Flutter SDK 3.x](https://docs.flutter.dev/get-started/install)
- Android Studio / Android SDK (for Android APK)
- Xcode / macOS (for iOS IPA)

### 1. Android APK Build

To build a standalone `.apk` for Android:

```bash
cd mobile
flutter pub get
flutter build apk --release
```

The compiled APK will be located at:
`mobile/build/app/outputs/flutter-apk/app-release.apk`

Users can download this `.apk` file directly to their Android phone, enable *"Install from unknown sources"*, and install it in seconds.

---

### 2. iOS Build (TestFlight / IPA)

To build an `.ipa` for iOS:

```bash
cd mobile
flutter pub get
flutter build ipa --release
```

- **TestFlight:** Upload the generated `.ipa` to Apple App Store Connect to distribute via a public TestFlight link.
- **Sideloading:** Install directly using AltStore, Sideloadly, or Scarlet.

---

## 📂 Project Structure

```
mobile/
├── assets/
│   └── speed-control.js     # Shared JavaScript controller engine
├── lib/
│   └── main.dart            # Flutter InAppWebView & Ad-Shield Network Interceptor
├── android/                 # Native Android project files & AndroidManifest
├── pubspec.yaml             # Dependencies & Asset configuration
└── README.md                # Mobile documentation
```
