# Prime Video Speed & Subtitle Controller

A local companion for watching Prime Video in Microsoft Edge on Windows. It adds playback-speed controls and subtitle styling without modifying the official Prime Video application or downloading protected content.

[Latest release](https://github.com/IACBI/prime-video-enhancer/releases/latest) · [Mobile app](mobile/README.md) · [Security](SECURITY.md) · [Support](SUPPORT.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md)

> **Unofficial project.** This project is not affiliated with Amazon or Prime Video. Website layouts, availability, and playback behaviour can change without notice.

## What you can do

- Open Prime Video in a dedicated Microsoft Edge app window with a local-only debugging endpoint.
- Choose playback speeds from `0.25x` to `4x`, with presets and keyboard shortcuts.
- Adjust subtitle color, size, and background; preferences are stored locally in the dedicated browser profile.
- Use the optional ad-related request filtering and in-player skip controls. These are best-effort controls: availability and results vary by content, region, account, and changes to Prime Video.
- Use the companion Android WebView app, which is released as an APK. The iOS source project is included, but iOS builds are not currently published in GitHub Releases.

## Download and use

### Windows

Download one of the Windows assets from the [latest release](https://github.com/IACBI/prime-video-enhancer/releases/latest):

| Asset | Choose it when |
| --- | --- |
| `PrimeVideoSpeedApp-Standalone.exe` | You want a self-contained Windows app with no separate .NET installation. |
| `PrimeVideoSpeedApp-Light.exe` | You already have the .NET 8 Desktop Runtime and prefer the smaller download. |

Run the executable, sign in to Prime Video in the Edge window it opens, then start a title. The floating control appears when a compatible video element is available.

### Android

Download `PrimeVideoSpeedApp-Mobile.apk` from the [latest release](https://github.com/IACBI/prime-video-enhancer/releases/latest). Android may ask you to allow installation from the app used to open the download. Review Android's prompt before continuing.

The Android app opens Prime Video in an embedded WebView. Playback and service compatibility depend on the device, the installed WebView, your Prime Video plan, and regional availability.

## Desktop controls

| Action | Control |
| --- | --- |
| Open or close the menu | Click the floating control or press `Escape` to close it |
| Move the control | Drag it to a preferred position |
| Change speed | Select a preset, use `+` / `-`, or press `↑` / `↓` |
| Reset speed | `\` |
| Toggle subtitle styling | Select **Subtitles** or press `S` |
| Toggle captions in the player | `Alt` + `C` or `Shift` + `C` |
| Skip intro / next episode when offered | `N` |

Settings are saved locally and reapplied when the player or subtitle track changes.

## Privacy, safety, and compatibility

- The desktop app creates a dedicated Edge profile and binds its debugging endpoint to `127.0.0.1:9223` only.
- The project does not collect telemetry or transmit Prime Video credentials, cookies, tokens, viewing history, or personal data.
- It does not bypass DRM, remove service restrictions, or download video content.
- Request filtering is limited to the app's own browser session or mobile WebView; it does not install a system proxy, VPN, or HTTPS certificate.
- You are responsible for using the software in accordance with applicable law and the services you use.

Read the full [security and privacy policy](SECURITY.md) before reporting an issue that may involve sensitive information.

## Build from source

### Windows desktop app

Requirements: Windows 10 or later, Microsoft Edge, and the .NET 8 SDK.

```powershell
dotnet build -c Release
.\run.cmd
```

`run.cmd` runs the project from source unless a local published executable already exists. To produce the two Windows release packages:

```powershell
.\publish.cmd
```

Output is written to `publish/Light/` and `publish/Standalone/`.

### Android app

Requirements: Flutter 3.x, a compatible Android SDK, and Java 17.

```powershell
cd mobile
flutter pub get
flutter build apk --release
```

The generated APK is `mobile/build/app/outputs/flutter-apk/app-release.apk`.

For iOS development, use macOS, Xcode, and Flutter to build the project under `mobile/ios/`. App signing, TestFlight distribution, and store submission are intentionally not automated by this repository.

## Verification

Run the checks relevant to your change before opening a pull request:

```powershell
dotnet run --project .\PrimeVideoSpeedApp.Tests\PrimeVideoSpeedApp.Tests.csproj -c Release
node --check .\speed-control.js

cd mobile
flutter analyze
flutter test
flutter build apk --release
```

`PrimeVideoSpeedApp.Tests/browser-smoke.js` is an optional live test. It requires the desktop app to be running and a locally reachable Prime Video debugging session.

## Project layout

```text
.
├── Program.cs                       # Windows desktop helper and Edge/CDP integration
├── speed-control.js                 # Injected playback, subtitle, and request-handling UI
├── PrimeVideoSpeedApp.Tests/        # Desktop regression and optional browser smoke tests
├── mobile/                          # Flutter Android/iOS source project
└── .github/workflows/release.yml    # Tagged release build for Windows and Android
```

## Support and contributions

Bug reports and focused improvements are welcome. Read [SUPPORT.md](SUPPORT.md) for troubleshooting and [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue. For security-sensitive reports, follow [SECURITY.md](SECURITY.md) instead of posting account data or credentials in a public issue.

## License

This project is released under the [MIT License](LICENSE).

---

<a id="turkce"></a>

## Türkçe

Prime Video Speed & Subtitle Controller, Windows üzerinde Prime Video'yu ayrı bir Microsoft Edge uygulama penceresinde açan yerel bir yardımcı araçtır. Oynatma hızı ve altyazı görünümünü kontrol etmenize yardımcı olur; resmi Prime Video uygulamasını değiştirmez, DRM'i aşmaz ve içerik indirmez.

### Hızlı başlangıç

1. [Son sürümden](https://github.com/IACBI/prime-video-enhancer/releases/latest) Windows için uygun `.exe` dosyasını veya Android için `.apk` dosyasını indirin.
2. Windows'ta **Standalone** paket ek .NET kurulumu gerektirmez; **Light** paket .NET 8 Desktop Runtime gerektirir.
3. Uygulamayı açın, Prime Video hesabınıza Edge penceresi içinden giriş yapın ve bir içerik başlatın.
4. Uyumlu oynatıcı algılandığında ekranda hız ve altyazı kontrolü görünür.

### Önemli notlar

- Hız aralığı `0.25x`–`4x`tir; altyazı rengi, boyutu ve arka planı yerel olarak hatırlanır.
- Reklamla ilişkili istek filtreleme ve atlama davranışları isteğe bağlı, en iyi çaba özellikleridir. İçeriğe, bölgeye ve Prime Video'nun değişen arayüzüne göre farklı sonuç verebilir.
- Android APK GitHub Releases üzerinden yayımlanır. iOS kaynak dosyaları depodadır; iOS paketi şu anda Releases üzerinden yayımlanmaz.
- Uygulama telemetri, şifre, çerez, token veya izleme geçmişi toplamaz ya da göndermez.

Kaynak koddan derleme, güvenlik ve katkı bilgileri için yukarıdaki İngilizce bölümü; mobil ayrıntılar için [mobile/README.md](mobile/README.md) dosyasını kullanın.
