<a id="top"></a>
# Prime Video Speed & Subtitle Controller

Playback speed and subtitle styling for Prime Video on Windows and Android · [![Latest release](https://img.shields.io/github/v/release/IACBI/prime-video-enhancer)](https://github.com/IACBI/prime-video-enhancer/releases/latest) [![Build](https://github.com/IACBI/prime-video-enhancer/actions/workflows/release.yml/badge.svg)](https://github.com/IACBI/prime-video-enhancer/actions/workflows/release.yml) [![License: MIT](https://img.shields.io/github/license/IACBI/prime-video-enhancer)](LICENSE)

**Read this in:** [English](#english) · [Türkçe](#turkce)

---
<a id="english"></a>
## English

### Overview

A local companion for watching Prime Video. On Windows it opens Prime Video in its own Microsoft Edge app window and adds a small floating control for playback speed and subtitle appearance; on Android the same controller runs inside the app's WebView.

It is an unofficial project, not affiliated with Amazon or Prime Video. Prime Video changes its player without notice, so any feature here can stop working until the controller catches up.

What it does not do: it does not bypass DRM, download video, collect telemetry, or send your credentials, cookies or viewing history anywhere. Request filtering stays inside the app's own browser window or WebView; nothing is installed system-wide. The [security policy](SECURITY.md) covers the details, including the local debugging endpoint the desktop app relies on.

### Features

- Playback speed from `0.25x` to `4x`, with presets and keyboard shortcuts.
- Subtitle colour, size and backdrop, applied from the first frame of each line and remembered between sessions.
- Optional pitch correction, so voices keep their natural pitch at higher speeds.
- An ad shield that blocks known ad and telemetry requests and, when an ad still plays, mutes and covers it and runs it at up to 16x. Results vary by title, region and account.
- Automatic Skip Intro / Next Episode when Prime Video offers the button.
- An Android app (APK) built on the same controller. The iOS project is in the repository but no iOS build is published.

### Requirements

- **Windows:** Windows 10 or later and Microsoft Edge. The `Light` download also needs the .NET 8 Runtime; the `Standalone` one does not.
- **Android:** Android 7.0 or later with an up-to-date Android System WebView.
- **Building from source:** the .NET 8 SDK for the desktop app; Flutter 3.47.1 (the version CI builds with), the Android SDK and Java 17 for the Android app.

### Installation

Download from the [latest release](https://github.com/IACBI/prime-video-enhancer/releases/latest):

| File | Use it for |
| --- | --- |
| `PrimeVideoSpeedApp-Standalone.exe` | Windows, no .NET needed |
| `PrimeVideoSpeedApp-Light.exe` | Windows, smaller, needs the .NET 8 Runtime |
| `PrimeVideoSpeedApp-Mobile.apk` | Android |

Releases after 3.7.0 also include `SHA256SUMS.txt` for checking a download, and the Android app is signed with a stable release key; [SECURITY.md](SECURITY.md) shows how to verify both.

To build from source instead:

```powershell
dotnet build -c Release
.\run.cmd
.\publish.cmd

cd mobile
flutter pub get
flutter build apk --release
```

`run.cmd` starts a published build if there is one and runs from source otherwise. `publish.cmd` writes the two Windows packages to `publish/Light/` and `publish/Standalone/`; the APK ends up in `mobile/build/app/outputs/flutter-apk/`. iOS builds need macOS and Xcode; see [mobile/README.md](mobile/README.md).

### Usage

Run the executable and sign in to Prime Video in the window it opens. The floating control appears once a title starts playing. The first run also adds a **Prime Video Enhancer** entry to the Start Menu.

| Action | Control |
| --- | --- |
| Open or close the menu | Click the floating control; `Escape` closes it |
| Move the control | Drag it |
| Change speed | Select a preset, or press `+` / `-`, `]` / `[`, or `↑` / `↓` |
| Reset speed | `\` |
| Toggle subtitle styling | The switch next to **Subtitles**, or press `S`, `Alt` + `C` or `Shift` + `C` |
| Skip intro / next episode | `N`, or the **Skip intro** button in the menu |

The helper and its Prime Video window live and die together: closing the console window closes Prime Video, and closing Prime Video ends the helper. Launching it again while it is running just opens another window.

On Android, tap the control to open the menu; the Back button closes the menu before it navigates.

### Configuration

There is no settings file. Everything you change in the menu is saved automatically in the app's own browser storage.

- **Desktop data** lives in `%LOCALAPPDATA%\PrimeVideoSpeedController\`. Deleting that folder resets every preference and signs you out of the dedicated Edge profile.
- **Debugging port:** the desktop app talks to Edge on `127.0.0.1:9223`, or on a free port it picks (and prints) when another program holds 9223.
- **`PVSC_DATA_DIR`:** set it to a folder to keep the browser profile and icon cache there instead. The Start Menu entry is then left alone, so the copy can run beside a normal installation.
- **Mobile WebView inspection** is off in every build. Developers can enable it with `flutter run --dart-define=PVSC_WEBVIEW_DEBUG=true`.

### Contributing

Bug reports and focused pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) for the checks to run and the rule that `speed-control.js` and `mobile/assets/speed-control.js` stay identical, and [SUPPORT.md](SUPPORT.md) for troubleshooting. Report security problems privately as described in [SECURITY.md](SECURITY.md), never in a public issue.

```text
Program.cs                 Windows helper: launches Edge, injects the controller, blocks ad requests
speed-control.js           The controller itself: speed, subtitles, ad shield, panel
PrimeVideoSpeedApp.Tests/  Desktop tests, a headless controller test and an optional live browser test
mobile/                    Flutter app for Android (and iOS source)
```

Changes are listed in [CHANGELOG.md](CHANGELOG.md).

### License

[MIT](LICENSE) © 𝓐.𝓒.𝓑

[⬆ Back to top](#top)

---
<a id="turkce"></a>
## Türkçe

### Genel Bakış

Prime Video izlerken yanında çalışan yerel bir yardımcı. Windows'ta Prime Video'yu ayrı bir Microsoft Edge uygulama penceresinde açar ve oynatma hızı ile altyazı görünümü için küçük, yüzen bir kontrol ekler. Android'de aynı kontrol, uygulamanın kendi WebView'u içinde çalışır.

Resmî bir proje değildir; Amazon veya Prime Video ile bir bağı yoktur. Prime Video oynatıcısını haber vermeden değiştirebildiği için buradaki herhangi bir özellik, kontrol güncellenene kadar çalışmayabilir.

Yapmadıkları: DRM'i aşmaz, video indirmez, telemetri toplamaz; şifrenizi, çerezlerinizi ya da izleme geçmişinizi hiçbir yere göndermez. İstek filtreleme yalnızca uygulamanın kendi tarayıcı penceresinde veya WebView'unda çalışır, sisteme hiçbir şey kurulmaz. Masaüstü uygulamanın kullandığı yerel hata ayıklama bağlantısı dâhil ayrıntılar [güvenlik politikasında](SECURITY.md).

### Özellikler

- `0.25x` ile `4x` arası oynatma hızı; hazır değerler ve klavye kısayolları.
- Altyazı rengi, boyutu ve arka planı. Her satır ilk karesinden itibaren sizin ayarınızla görünür, ayarlar oturumlar arasında hatırlanır.
- İsteğe bağlı perde (pitch) düzeltmesi: hız artsa da sesler doğal tonunda kalır.
- Reklam kalkanı: bilinen reklam ve telemetri isteklerini engeller; buna rağmen oynayan bir reklamı sessize alır, üstünü kapatır ve 16x'e kadar hızlandırır. Sonuç içeriğe, bölgeye ve hesaba göre değişir.
- Prime Video "Girişi Atla / Sonraki Bölüm" düğmesini gösterdiğinde otomatik atlama.
- Aynı kontrolü kullanan bir Android uygulaması (APK). iOS projesi depoda var, ancak yayımlanmış bir iOS sürümü yok.

### Gereksinimler

- **Windows:** Windows 10 veya üzeri ve Microsoft Edge. `Light` sürümü ayrıca .NET 8 Runtime ister; `Standalone` istemez.
- **Android:** Android 7.0 veya üzeri ve güncel bir Android System WebView.
- **Kaynaktan derlemek için:** masaüstü uygulama için .NET 8 SDK; Android uygulaması için Flutter 3.47.1 (CI'ın kullandığı sürüm), Android SDK ve Java 17.

### Kurulum

[Son sürümden](https://github.com/IACBI/prime-video-enhancer/releases/latest) indirin:

| Dosya | Ne için |
| --- | --- |
| `PrimeVideoSpeedApp-Standalone.exe` | Windows, .NET gerekmez |
| `PrimeVideoSpeedApp-Light.exe` | Windows, daha küçük, .NET 8 Runtime gerekir |
| `PrimeVideoSpeedApp-Mobile.apk` | Android |

3.7.0'dan sonraki sürümlerde indirilen dosyayı doğrulamak için `SHA256SUMS.txt` de yer alır ve Android uygulaması sabit bir sürüm anahtarıyla imzalanır. İkisinin nasıl doğrulanacağı [SECURITY.md](SECURITY.md) dosyasında.

Kaynaktan derlemek isterseniz:

```powershell
dotnet build -c Release
.\run.cmd
.\publish.cmd

cd mobile
flutter pub get
flutter build apk --release
```

`run.cmd` yayımlanmış bir derleme varsa onu, yoksa kaynak kodu çalıştırır. `publish.cmd` iki Windows paketini `publish/Light/` ve `publish/Standalone/` klasörlerine yazar; APK `mobile/build/app/outputs/flutter-apk/` altında oluşur. iOS derlemesi için macOS ve Xcode gerekir; ayrıntılar [mobile/README.md](mobile/README.md) dosyasında.

### Kullanım

Uygulamayı çalıştırın ve açılan pencerede Prime Video hesabınıza girin. Bir içerik oynamaya başlayınca yüzen kontrol belirir. İlk çalıştırmada Başlat menüsüne **Prime Video Enhancer** kısayolu da eklenir.

| İşlem | Kontrol |
| --- | --- |
| Menüyü açmak veya kapatmak | Yüzen kontrole tıklayın; `Escape` kapatır |
| Kontrolü taşımak | Sürükleyin |
| Hızı değiştirmek | Hazır bir değer seçin ya da `+` / `-`, `]` / `[` veya `↑` / `↓` tuşlarına basın |
| Hızı sıfırlamak | `\` |
| Altyazı stilini açıp kapatmak | **Subtitles** yanındaki anahtar, ya da `S`, `Alt` + `C` veya `Shift` + `C` |
| Girişi / sonraki bölümü atlamak | `N` ya da menüdeki **Skip intro** düğmesi |

Yardımcı ve Prime Video penceresi birlikte açılıp kapanır: konsol penceresini kapatırsanız Prime Video da kapanır, Prime Video'yu kapatırsanız yardımcı da sonlanır. Zaten çalışırken yeniden başlatmak yalnızca yeni bir pencere açar.

Android'de menüyü açmak için kontrole dokunun; Geri tuşu önce menüyü kapatır, sonra sayfada geri gider.

### Yapılandırma

Ayrı bir ayar dosyası yok. Menüde değiştirdiğiniz her şey uygulamanın kendi tarayıcı depolamasına otomatik kaydedilir.

- **Masaüstü verileri** `%LOCALAPPDATA%\PrimeVideoSpeedController\` klasöründe durur. Bu klasörü silmek bütün tercihleri sıfırlar ve ayrılmış Edge profilindeki oturumunuzu kapatır.
- **Hata ayıklama portu:** masaüstü uygulama Edge ile `127.0.0.1:9223` üzerinden konuşur; başka bir program 9223'ü tutuyorsa boş bir port seçer ve ekrana yazar.
- **`PVSC_DATA_DIR`:** bir klasör adı verirseniz tarayıcı profili ve simge önbelleği orada tutulur. Başlat menüsü kısayoluna dokunulmaz; böylece kopya normal kurulumun yanında çalışabilir.
- **Mobil WebView incelemesi** hiçbir derlemede açık değildir. Geliştiriciler `flutter run --dart-define=PVSC_WEBVIEW_DEBUG=true` ile açabilir.

### Katkı

Hata bildirimlerine ve odaklı pull request'lere açığız. Çalıştırılması gereken kontroller ve `speed-control.js` ile `mobile/assets/speed-control.js` dosyalarının birebir aynı kalması kuralı için [CONTRIBUTING.md](CONTRIBUTING.md), sorun giderme için [SUPPORT.md](SUPPORT.md) dosyasına bakın. Güvenlik sorunlarını herkese açık bir issue'da değil, [SECURITY.md](SECURITY.md) dosyasında anlatıldığı gibi gizli olarak bildirin.

```text
Program.cs                 Windows helper: launches Edge, injects the controller, blocks ad requests
speed-control.js           The controller itself: speed, subtitles, ad shield, panel
PrimeVideoSpeedApp.Tests/  Desktop tests, a headless controller test and an optional live browser test
mobile/                    Flutter app for Android (and iOS source)
```

Değişiklikler [CHANGELOG.md](CHANGELOG.md) dosyasında.

### Lisans

[MIT](LICENSE) © 𝓐.𝓒.𝓑

[⬆ Başa Dön](#top)
