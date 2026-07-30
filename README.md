# Prime Video Speed & Subtitle Controller

**Prime Video Speed & Subtitle Controller** is an open-source, lightweight Windows helper that opens Prime Video in a dedicated Microsoft Edge app window, adding a clean playback speed control, a fully-customizable subtitle overlay (color, size, and background), and an always-on **5-Layer Zero-Visibility Ad Shield** (`🛡️ Ad Shield`).

It is built for viewers who want a seamless, commercial-free streaming experience with simple speed selection and consistent subtitle styling without modifying the official Prime Video app, bypassing DRM, downloading video, or installing a browser extension.

**Author:** **𝓐.𝓒.𝓑**  
**License:** MIT  
**Platform:** Windows, Microsoft Edge, .NET 8

## Languages

[English](#english) · [Türkçe](#turkce) · [Español](#espanol) · [Deutsch](#deutsch) · [Français](#francais) · [Português](#portugues) · [中文](#zh) · [हिन्दी](#hindi) · [العربية](#arabic) · [Русский](#russian) · [日本語](#japanese) · [Bahasa Indonesia](#indonesian)

---

<a id="english"></a>
## English

### Overview

Prime Video Speed & Subtitle Controller launches Prime Video in an Edge app-style window and adds a sleek floating button when a real video player is detected. The control stays out of the way, can be dragged to a preferred position, and remembers both your selected playback speed and custom subtitle color preferences locally.

In addition to multi-layer subtitle stabilization (defaulting to **Yellow `#FFCC00`**), this project features an **Always-On 5-Layer Zero-Visibility Ad Shield** that blocks ads at the network request level using Chromium's CDP Fetch interception (similar to uBlock Origin), blocks tracking URLs, and covers unskippable stitched ad segments with an opaque overlay while silently fast-forwarding them at `16x`, ensuring you never see or hear commercials (`Zero-Visibility`). Normal user playback speed customization ranges smoothly from `0.25x` to `4.0x`.

### Features

- Opens Prime Video in a dedicated Microsoft Edge app window (`--remote-debugging-address=127.0.0.1`).
- Shows the speed & subtitle control only when video playback is available.
- **Always-On 5-Layer Zero-Visibility Ad Shield (`🛡️ Ad Shield`):**
  - **Layer 1 (CDP Fetch Interception - uBlock Origin Style):** Uses Chromium's `Fetch.enable` and `Fetch.requestPaused` protocol domains to block ads at the request stage before any bytes are loaded. Returns custom empty VAST/VPAID response XMLs for stitched player-level ads.
  - **Layer 2 (Network-Level Blocker):** Blocks Amazon ad servers (`amazon-adsystem.com`), telemetry, and tracking networks right at the Chromium network layer (`Network.setBlockedURLs`).
  - **Layer 3 (CSS Banner & Countdown Destroyer):** Permanently removes "Ad 1 of 2", ad countdown banners, and ad overlays (`opacity: 0 !important`).
  - **Layer 4 (Opaque Ad Cover & Auto-Mute):** During unskippable stitched ad breaks, instantly mutes commercial audio (`video.muted = true`) and hides the ad stream behind an opaque cover overlay. The shield only engages when a real ad countdown (e.g. `0:27`) is visible, and a 45-second safety valve with a 2-minute cooldown guarantees a stuck or false detection can never lock normal playback behind the cover.
  - **Layer 5 (Auto-Skip Clicker & 16x Hyper-Speed):** Automatically clicks "Skip Ad" the millisecond it appears, or accelerates unskippable ads at `16x` speed to finish them in seconds before restoring normal playback.
- **Smart Auto-Hide During Playback:** Exactly 2 seconds after video playback begins or the mouse stops moving, the floating button smoothly fades out for an ultra-clean viewing experience. Reappears instantly on mouse movement or pause.
- **Compact Icon Indicator:** Displays your current speed and a clean indicator icon:
  - **`1.2x ●`** when custom subtitle styling is ON (the dot glows in your selected subtitle color).
  - **`1.2x ⚡`** when subtitle override is OFF (speed control only).
- **Custom Taskbar & Window Icon:** Uses Win32 COM to set `AppUserModelID` exclusively for dedicated `msedge.exe` windows, ensuring clean taskbar grouping.
- **Multi-layer Subtitle Customizer:** Fully control how subtitles look:
  - **Color:** Choose from 5 presets — Yellow (`#FFCC00`), Gold (`#FFD700`), White (`#FFFFFF`), Green (`#00FF66`), Cyan (`#00FFFF`).
  - **Size:** Type any percentage from **50% to 400%** in the size input and press Enter. Setting is saved across sessions.
  - **Background:** Cycle through **Shadow** (semi-transparent dark box), **Solid** (opaque black box), and **None** (transparent). Applied via inline style injection to win over Prime Video's own styling.
  - Persistent across episodes via `MutationObserver` + direct inline style injection.
- Includes common speed presets: `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` and fine adjustments (`+` / `-` from `0.25x` to `4.0x`).
- Lets you drag the floating button to a comfortable place on screen.
- Remembers selected speed, button position, subtitle color, size, background, and toggle state locally.
- Reapplies settings automatically if Prime Video resets the video element or subtitle tracks.

### What It Does Not Do

- It does not modify the official Prime Video desktop app.
- It does not bypass DRM, remove restrictions, or download videos.
- It does not read, store, or transmit passwords, cookies, tokens, watch history, or private account data.
- It does not send telemetry.

### Requirements

- Windows 10 or Windows 11
- Microsoft Edge
- .NET 8 Runtime or .NET 8 SDK

### Quick Start

1. Download or clone this repository.
2. Run `run.cmd`.
3. Sign in to Prime Video in the window that opens.
4. Start a movie or episode.
5. Click the floating button (`1.2x ●`) to adjust playback speed or pick your subtitle color. Enjoy zero ads automatically!

### Controls & Shortcuts

| Action | Method |
|--------|--------|
| Open/close menu | Click the floating button |
| Move button | Click-and-drag the button |
| Change speed | Click a preset or use `+` / `-` |
| Change subtitle color | Click a color swatch |
| Change subtitle size | Type a value (50–400) in the **Size** input and press Enter |
| Change subtitle background | Click **Bg** to cycle: Shadow → Solid → None |
| Toggle subtitle styling | Click **Subtitles: ON / OFF** or press `s` |
| Toggle subtitle (keyboard) | `Alt + C` or `Shift + C` |
| Speed up +0.1x | `]` or `+` or `↑` |
| Speed down −0.1x | `[` or `-` or `↓` |
| Reset speed to 1x | `\` |
| Skip intro / next episode | `n` |
| Close menu | `Escape` |

### Build From Source & Single-File Releases

You can build or run from source using the .NET CLI:
```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Automated Single-File EXE Generation (Hybrid Architecture)
Thanks to our **Hybrid Priority Architecture**, `speed-control.js` and `AppIcon.ico` are both read from the external directory when present (enabling instant developer hot-reloading) OR loaded seamlessly from `<EmbeddedResource>` inside C# when running as a standalone single-file `.exe`.

To generate production-ready single-file executables for GitHub Releases, run our automated script:
```powershell
.\publish.cmd
```
This automatically compiles two distribution formats into the `publish/` directory:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Ultra-lightweight Single-File EXE (Framework-Dependent). Requires `.NET 8 Desktop Runtime` on the target machine.
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Self-Contained Single-File EXE. Bundles the complete `.NET 8 Runtime` and runs instantly on any 64-bit Windows PC without requiring .NET installation!

### Privacy And Security

The app starts Edge with a dedicated local profile and restricts remote debugging strictly to `127.0.0.1:9223`. It uses that local endpoint only to inject the `speed-control.js` script and apply `Network.setBlockedURLs` into Prime Video pages opened by the Edge instance it launched.

The injected script stores only local preferences in `localStorage`:
- selected playback speed
- floating button coordinates
- selected subtitle color, size, and background mode
- subtitle overlay enabled/disabled state
- ad statistics (ads blocked count, time saved)

No credentials, cookies, tokens, viewing history, or personal data are collected or transmitted. Strict hexadecimal regex filtering (`/^#[0-9A-Fa-f]{6}$/`) protects against any XSS attempts.

### License

Released under the MIT License. See [LICENSE](LICENSE).

---

<a id="turkce"></a>
## Türkçe

### Genel Bakış

Prime Video Speed & Subtitle Controller, Prime Video'yu Microsoft Edge üzerinde özel bir uygulama penceresinde açan ve ekranda gerçek bir video oynatıcı algılandığında zarif, kaydırılabilir bir buton gösteren açık kaynaklı ve hafif bir Windows aracıdır. Bu kontrol butonu izleme keyfinizi bölmez, ekranda dilediğiniz konuma taşınabilir ve seçtiğiniz oynatma hızı, altyazı rengi, altyazı boyutu ve arka plan stili tercihlerinizi yerel olarak hatırlar.

Çok katmanlı altyazı özelleştiricisine ek olarak, projemizde entegre bir **5 Katmanlı Sıfır Görünürlük Reklam Kalkanı (Zero-Visibility Ad Shield)** yer alır. Bu kalkan, reklam sunucu isteklerini doğrudan ağ isteği seviyesinde Chromium CDP Fetch protokolü (uBlock Origin tarzı) aracılığıyla engelleyip, diğer takip ağlarını engeller ve atlanamayan reklamları opak bir perde arkasında 16x hiper sessiz hızda eritir (`Zero-Visibility`). Kullanıcının normal izleme hızı ise serbestçe `0.25x` ile `4.0x` arasında ayarlanabilir.

### Özellikler

- Prime Video'yu özel ve yerel bir Microsoft Edge penceresinde açar (`--remote-debugging-address=127.0.0.1`).
- Hız ve altyazı butonunu (`1.2x ●` / `1.2x ⚡`) sadece video oynatımı hazır olduğunda gösterir.
- **5 Katmanlı Sıfır Görünürlük Reklam Kalkanı (`🛡️ Reklam Kalkanı`):**
  - **Katman 1 (CDP Fetch İstek Kesicisi - uBlock Origin Tarzı):** Chromium'un `Fetch.enable` ve `Fetch.requestPaused` protokol etki alanlarını kullanarak, reklam dosyalarının byte'ları daha yüklenmeye başlamadan istek aşamasında bloke eder. Oynatıcı düzeyinde gömülü reklamlar için boş VAST/VPAID XML yanıtı döner.
  - **Katman 2 (Ağ Reklam ve Takipçi Engelleyici):** Amazon reklam sunucularını (`amazon-adsystem.com`), telemetri ve takip ağlarını doğrudan Chromium ağ katmanında engeller (`Network.setBlockedURLs`).
  - **Katman 3 (CSS Banner ve Geri Sayım Yok Edici):** "Reklam 1/2" uyarılarını, reklam sayacı banner'larını ve katmanlarını tamamen görünmez yapar (`opacity: 0 !important`).
  - **Katman 4 (Opak Reklam Perdesi ve Otomatik Sessize Alma):** Atlanamayan zorunlu gömülü reklam aralarında ses otomatik kesilir (`video.muted = true`) ve reklam akışı opak bir perde ile gizlenir. Kalkan yalnızca gerçek bir reklam geri sayımı (örn. `0:27`) görünürken devreye girer; 45 saniyelik emniyet valfi ve 2 dakikalık bekleme süresi, takılı veya hatalı bir algılamanın normal izlemeyi asla perde arkasına kilitleyememesini garanti eder.
  - **Katman 5 (Otomatik Skip ve 16x Hiper Hız):** "Reklamı Atla / Skip Ad" butonu çıktığı milisaniye otomatik tıklanır. Atlanamayan reklamlarda ise video `16x` hiper hıza alınarak birkaç saniyede aşılır ve asıl içerik normal hızda pürüzsüzce geri gelir.
- **Akıllı Otomatik Gizleme (Auto-Hide):** Video oynatımı başladıktan tam 2 saniye sonra veya fare hareketsiz kaldığında, buton yumuşak bir animasyonla ekrandan kaybolur ve tertemiz sinematik bir ekran sunar. Fare hareketinde veya duraklatıldığında anında görünür hale gelir.
- **Kompakt Durum İkonu:** Mevcut hızı ve aktif modu simgeyle gösterir:
  - **`1.2x ●`** altyazı özelleştirmesi AÇIK olduğunda (nokta seçilen altyazı renginde parlar).
  - **`1.2x ⚡`** altyazı özelleştirmesi KAPALI olduğunda (sadece hız kontrolü aktif).
- **Özel Görev Çubuğu ve Pencere İkonu:** Win32 COM arayüzü kullanılarak sadece adanmış `msedge.exe` pencerelerine `AppUserModelID` (`PrimeVideoSpeedController.App`) atanır ve görev çubuğunda kusursuz bir gruplama sağlanır.
- **Gelişmiş Altyazı Özelleştirici:**
  - **Renk:** 5 renk önayarı — Sarı (`#FFCC00`), Altın (`#FFD700`), Beyaz (`#FFFFFF`), Yeşil (`#00FF66`), Mavi (`#00FFFF`).
  - **Boyut:** **Boyut (Size)** kutusuna **%50 ile %400** arasında istenilen yüzde değeri yazılıp Enter'a basılabilir.
  - **Arka Plan:** **Bg** butonuna basılarak arka plan modu seçilebilir: **Shadow** (yarı saydam koyu kutu), **Solid** (opak siyah kutu), **None** (şeffaf). Doğrudan inline style enjeksiyonu ile Prime Video'nun kendi stillerini kesinlikle ezer.
  - `MutationObserver` ve doğrudan inline style enjeksiyonu sayesinde bölüm geçişlerinde ve DOM yenilenmelerinde ayarlarınız korunur.
- Yaygın hız önayarlarını (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) ve hassas adım butonlarını (`+` / `-` ile `0.25x` - `4.0x`) içerir.
- Buton sürüklenerek ekranda istenilen yere taşınabilir.
- Tercihleri (hız, konum, renk, boyut, arka plan, açık/kapalı durumu) yerel olarak `localStorage` üzerinde saklar.
- Oynatıcı veya altyazı parçası yenilendiğinde tercihleri anında yeniden uygular.

### Ne Yapmaz

- Resmi Prime Video masaüstü uygulamasını değiştirmez veya modifiye etmez.
- DRM korumasını aşmaz, kısıtlamaları kaldırmaz veya video indirmez.
- Şifreleri, çerezleri, tokenları, izleme geçmişini veya kişisel verileri asla okumaz, saklamaz veya iletmez.
- Dışarıya telemetri verisi göndermez.

### Gereksinimler

- Windows 10 veya Windows 11
- Microsoft Edge
- .NET 8 Runtime veya .NET 8 SDK

### Hızlı Başlangıç

1. Bu projeyi indirin veya klonlayın.
2. `run.cmd` dosyasını çalıştırın.
3. Açılan özel Edge penceresinde Prime Video hesabınıza giriş yapın.
4. Dilediğiniz bir film veya diziyi başlatın.
5. Ekranda beliren `1.2x ●` butonuna tıklayarak hızınızı veya altyazı ayarlarınızı seçin. Reklamsız keyfin tadını çıkarın!

### Kontroller ve Kısayollar

| Eylem | Yöntem |
|-------|--------|
| Menüyü aç / kapat | Yüzen butona tıklayın |
| Butonu taşı | Butona basılı tutup sürükleyin |
| Hız değiştir | Önayarlara tıklayın veya `+` / `-` kullanın |
| Altyazı rengi değiştir | Bir renk dairesine tıklayın |
| Altyazı boyutu değiştir | **Size** kutusuna %50–400 arası değer girip Enter'a basın |
| Altyazı arka planı değiştir | **Bg** butonuna tıklayarak geçiş yapın: Shadow → Solid → None |
| Altyazı özelleştirmeyi aç/kapat | **Subtitles: ON / OFF** butonuna tıklayın veya `s` tuşuna basın |
| Altyazı modu (klavye) | `Alt + C` veya `Shift + C` |
| Hızı +0.1x artır | `]` veya `+` veya `↑` |
| Hızı −0.1x azalt | `[` veya `-` veya `↓` |
| Hızı 1x yap (sıfırla) | `\` |
| İntroyu / sonraki bölümü atla | `n` |
| Menüyü kapat | `Escape` |

### Kaynaktan Derleme ve Tek Dosyalı Sürümler

Projemizi .NET CLI ile standart şekilde derleyebilir veya çalıştırabilirsiniz:
```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Otomatik Tek Dosyalı `.exe` Üretimi (Hibrit Öncelik Mimarisi)
**Hibrit Öncelik Mimarimiz** sayesinde; `speed-control.js` ve `AppIcon.ico` dosyaları klasörde mevcutsa öncelikle harici olarak okunur (böylece geliştiriciler yeniden derleme yapmadan anında düzenleme yapabilir). Eğer harici dosya yoksa (örneğin sadece `.exe` indirildiyse), C# içine gömülü olan (`<EmbeddedResource>`) yedek kaynaklar devreye girer!

GitHub Releases (Yayınlar) için tek dosyalık sürümler üretmek üzere hazırladığımız otomatik scripti çalıştırabilirsiniz:
```powershell
.\publish.cmd
```
Bu script, `publish/` klasörü altına iki farklı dağıtım paketi derler:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Süper hafif Çerçeve Bağımlı (Framework-Dependent) sürüm. Çalışması için bilgisayarda `.NET 8 Desktop Runtime` yüklü olmalıdır.
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Tamamen Bağımsız (Self-Contained) sürüm. Tüm .NET 8 çalışma zamanını içinde barındırır; hiçbir gereksinim veya kurulum olmadan herhangi bir 64-bit Windows bilgisayarda anında çalışır!

### Gizlilik ve Güvenlik

Uygulama, Edge tarayıcısını tamamen izole bir yerel profille başlatır ve uzaktan hata ayıklama portunu kesin olarak yalnızca `127.0.0.1:9223` adresine kilitler. Bu yerel bağlantı sadece `speed-control.js` kodunu enjekte etmek ve `Network.setBlockedURLs` ile reklam alanlarını engellemek için kullanılır.

Enjekte edilen kod, yalnızca yerel tarayıcı hafızasına (`localStorage`) şu tercihleri kaydeder:
- seçilen izleme hızı
- buton ekran koordinatları
- seçilen altyazı rengi, boyutu (%50-400) ve arka plan modu
- altyazı özelleştirme aktiflik durumu
- engellenen reklam istatistikleri (sayı ve tasarruf edilen süre)

Hiçbir hesap bilgisi, çerez, token veya kişisel veri toplanmaz ve dışarı aktarılmaz. Katı Hex regex denetimi (`/^#[0-9A-Fa-f]{6}$/`) olası tüm XSS saldırılarını sıfır hata ile engeller.

### Lisans

MIT Lisansı altında yayınlanmıştır. Ayrıntılar için [LICENSE](LICENSE) dosyasına bakınız.

---

<a id="espanol"></a>
## Español

### Resumen

Prime Video Speed & Subtitle Controller es una herramienta ligera y de código abierto para Windows que abre Prime Video en una ventana dedicada de Microsoft Edge y añade un botón flotante cuando se detecta el reproductor de vídeo. Este control no molesta, se puede arrastrar a cualquier parte de la pantalla y recuerda localmente tu velocidad de reproducción, color, tamaño y fondo de subtítulos preferidos.

Además de la personalización avanzada de subtítulos, este proyecto incluye un **Escudo de Anuncios de 5 Capas (Zero-Visibility Ad Shield)** que bloquea servidores de publicidad a nivel de red (`Network.setBlockedURLs`), intercepta solicitudes en la fase de petición mediante CDP Fetch (estilo uBlock Origin) y silencia las pausas publicitarias obligatorias avanzando a velocidad ultrarrápida `16x` tras una cubierta opaca. La velocidad de reproducción normal para el usuario se ajusta libremente desde `0.25x` hasta `4.0x`.

### Funciones

- Abre Prime Video en una ventana dedicada de Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Muestra el control flotante (`1.2x ●` / `1.2x ⚡`) únicamente durante la reproducción de vídeo.
- **Escudo de Anuncios de 5 Capas (`🛡️ Reklam Kalkanı`):**
  - **Capa 1 (Intercepción CDP Fetch - estilo uBlock Origin):** Bloquea los anuncios en la fase de solicitud antes de que se cargue un solo byte. Devuelve respuestas XML VAST/VPAID vacías para anuncios integrados.
  - **Capa 2 (Bloqueo de Red y Rastreadores):** Bloquea servidores de publicidad de Amazon (`amazon-adsystem.com`), telemetría y rastreadores (`Network.setBlockedURLs`).
  - **Capa 3 (Destrucción de Banners y Contadores CSS):** Oculta permanentemente avisos de "Anuncio 1 de 2", banners temporizadores y superposiciones (`opacity: 0 !important`).
  - **Capa 4 (Cubierta Opaca de Anuncios y Silencio Automático):** Silencia el audio (`video.muted = true`) y cubre el vídeo con una capa oscura. La válvula de seguridad de 45 segundos y el enfriamiento de 2 minutos garantizan que las falsas detecciones nunca bloqueen la reproducción normal.
  - **Capa 5 (Salto Automático y Velocidad 16x):** Hace clic en "Omitir anuncio" al instante o acelera anuncios obligatorios a velocidad `16x` para consumirlos en segundos antes de restaurar tu velocidad normal.
- **Ocultación Automática Inteligente:** 2 segundos después de iniciar la reproducción o de que el ratón deje de moverse, el botón se desvanece suavemente. Vuelve a aparecer al mover el ratón o pausar.
- **Indicador Compacto de Estado:** Muestra tu velocidad actual junto con un icono distintivo:
  - **`1.2x ●`** cuando la personalización de subtítulos está ACTIVADA.
  - **`1.2x ⚡`** cuando está DESACTIVADA (sólo velocidad).
- **Icono Personalizado en Barra de Tareas:** Asigna `AppUserModelID` (`PrimeVideoSpeedController.App`) en exclusiva a las ventanas dedicadas de `msedge.exe`.
- **Personalizador de Subtítulos Multicapa:**
  - **Color:** 5 colores preestablecidos — Amarillo (`#FFCC00`), Dorado (`#FFD700`), Blanco (`#FFFFFF`), Verde (`#00FF66`), Cian (`#00FFFF`).
  - **Tamaño:** Introduce cualquier porcentaje entre **50% y 400%** en la casilla **Size** y pulsa Enter.
  - **Fondo:** Pulsa **Bg** para alternar: **Shadow** (caja oscura translúcida), **Solid** (caja negra opaca), **None** (transparente). La inyección directa de estilo en línea garantiza superar los estilos propios de Prime Video.
  - Persistente entre episodios mediante `MutationObserver` e inyección de estilos en línea.
- Incluye ajustes preestablecidos de velocidad (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) y ajustes finos (`+` / `-` de `0.25x` a `4.0x`).
- Permite arrastrar libremente el botón flotante en la pantalla.
- Recuerda localmente la velocidad, la posición del botón, el color, el tamaño, el fondo y el estado en `localStorage`.

### Qué No Hace

- No modifica la aplicación de escritorio oficial de Prime Video.
- No elude la protección DRM, ni elimina restricciones, ni descarga vídeos.
- No lee, almacena ni transmite contraseñas, cookies, tokens, historial de visualización ni datos privados.
- No envía datos de telemetría.

### Requisitos

- Windows 10 o Windows 11
- Microsoft Edge
- .NET 8 Runtime o .NET 8 SDK

### Inicio Rápido

1. Descarga o clona este repositorio.
2. Ejecuta `run.cmd`.
3. Inicia sesión en Prime Video dentro de la ventana dedicada que se abre.
4. Reproduce cualquier película o serie.
5. Haz clic en el botón flotante (`1.2x ●`) para ajustar la velocidad o personalizar los subtítulos. ¡Disfruta sin publicidad!

### Controles y Atajos

| Acción | Método |
|--------|--------|
| Abrir / cerrar menú | Haz clic en el botón flotante |
| Mover botón | Arrastra el botón con el clic izquierdo |
| Cambiar velocidad | Haz clic en un ajuste preestablecido o usa `+` / `-` |
| Cambiar color de subtítulo | Haz clic en una muestra de color |
| Cambiar tamaño de subtítulo | Escribe un valor (50–400) en **Size** y pulsa Enter |
| Cambiar fondo de subtítulo | Haz clic en **Bg** para alternar: Shadow → Solid → None |
| Activar/desactivar subtítulos | Haz clic en **Subtitles: ON / OFF** o pulsa `s` |
| Modo de subtítulo (teclado) | `Alt + C` o `Shift + C` |
| Aumentar velocidad +0.1x | `]` o `+` o `↑` |
| Reducir velocidad −0.1x | `[` o `-` o `↓` |
| Restablecer velocidad a 1x | `\` |
| Omitir intro / siguiente episodio | `n` |
| Cerrar menú | `Escape` |

### Compilar desde el Código y Versiones de Archivo Único

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Generación automática de ejecutables de archivo único (Arquitectura Híbrida)
Gracias a nuestra **Arquitectura de Prioridad Híbrida**, los archivos `speed-control.js` y `AppIcon.ico` se leen externamente si existen, O BIEN se cargan como recursos incrustados (`<EmbeddedResource>`) si se ejecuta como un archivo único.

Para generar ejecutables `.exe` para GitHub Releases, ejecute nuestro constructor automatizado:
```powershell
.\publish.cmd
```
Este comando compila dos formatos en el directorio `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Versión ligera dependiente del marco (requiere .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Versión independiente (Self-Contained). Incluye el tiempo de ejecución de .NET 8 completo y funciona al instante.

### Privacidad y Seguridad

La aplicación inicia Edge con un perfil local dedicado y bloquea la depuración remota exclusivamente a `127.0.0.1:9223`.

El script inyectado sólo almacena preferencias locales en `localStorage`:
- velocidad de reproducción seleccionada
- coordenadas del botón flotante
- color, tamaño y modo de fondo de subtítulos seleccionados
- estado de activación del control de subtítulos
- estadísticas de anuncios (anuncios bloqueados y tiempo ahorrado)

No se recopilan ni transmiten datos privados. La validación regex hexadecimal estricta (`/^#[0-9A-Fa-f]{6}$/`) evita cualquier vulnerabilidad XSS.

### Licencia

Publicado bajo la Licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

---

<a id="deutsch"></a>
## Deutsch

### Überblick

Prime Video Speed & Subtitle Controller ist ein leichtgewichtiges Open-Source-Hilfsprogramm für Windows, das Prime Video in einem dedizierten Microsoft-Edge-Appfenster startet und einen eleganten, verschiebbaren Button einblendet, sobald ein Videoplayer erkannt wird. Die Steuerung bleibt unaufdringlich, lässt sich frei auf dem Bildschirm platzieren und speichert Ihre bevorzugte Wiedergabegeschwindigkeit, Untertitelfarbe, -größe und den -hintergrund lokal ab.

Zusätzlich zur erweiterten Untertitel-Anpassung bietet dieses Projekt einen **5-stufigen Werbeschutz (Zero-Visibility Ad Shield)**, der Werbe-Tracker auf Netzwerkebene (`Network.setBlockedURLs`) blockiert, Anfragen über CDP Fetch abfängt (uBlock-Origin-Stil) und unüberspringbare Werbung stummschaltet und beschleunigt. Die reguläre Wiedergabegeschwindigkeit lässt sich flexibel zwischen `0.25x` und `4.0x` anpassen.

### Funktionen

- Öffnet Prime Video in einem dedizierten Microsoft-Edge-Fenster (`--remote-debugging-address=127.0.0.1`).
- Zeigt den Geschwindigkeits- & Untertitelbutton (`1.2x ●` / `1.2x ⚡`) nur während der Videowiedergabe an.
- **5-stufiger Werbeschutz (`🛡️ Reklam Kalkanı`):**
  - **Stufe 1 (CDP-Fetch-Abfangen – uBlock-Origin-Stil):** Blockiert Werbung in der Anfragephase vor dem Laden. Liefert leere VAST/VPAID-XML-Antworten.
  - **Stufe 2 (Netzwerk- & Tracker-Blocker):** Blockiert Amazon-Werbeserver (`amazon-adsystem.com`), Telemetrie und Tracking-Netzwerke (`Network.setBlockedURLs`).
  - **Stufe 3 (CSS-Banner & Countdown-Zerstörer):** Entfernt „Werbung 1 von 2“-Hinweise und Overlays dauerhaft (`opacity: 0 !important`).
  - **Stufe 4 (Abdeckung & Auto-Stummschaltung):** Schaltet den Ton während Werbung stumm und deckt das Bild ab. Ein 45-Sekunden-Sicherheitsventil verhindert Fehlauslösungen.
  - **Stufe 5 (Auto-Skip Klicker & 16x Hyper-Geschwindigkeit):** Klickt auf „Werbung überspringen“ oder beschleunigt Werbung mit `16x` Geschwindigkeit.
- **Intelligentes Auto-Hide:** Genau 2 Sekunden nach Start des Videos oder bei Stillstand der Maus blendet sich der Button sanft aus. Erscheint bei Mausbewegung sofort wieder.
- **Kompakte Statusanzeige:** Zeigt Ihre aktuelle Geschwindigkeit und ein klares Modus-Symbol:
  - **`1.2x ●`** wenn Untertitel-Anpassung AKTIV ist.
  - **`1.2x ⚡`** wenn INAKTIV (nur Geschwindigkeitskontrolle).
- **Benutzerdefiniertes Taskleisten- & Fenster-Icon:** Nutzt `AppUserModelID` (`PrimeVideoSpeedController.App`) für `msedge.exe`-Fenster.
- **Erweiterter Untertitel-Anpasser:**
  - **Farbe:** 5 Voreinstellungen — Gelb (`#FFCC00`), Gold (`#FFD700`), Weiß (`#FFFFFF`), Grün (`#00FF66`), Cyan (`#00FFFF`).
  - **Größe:** Beliebigen Prozentwert von **50% bis 400%** im Feld **Size** eingeben und Eingabe drücken.
  - **Hintergrund:** Auf **Bg** klicken für: **Shadow** (halbtransparente dunkle Box), **Solid** (deckend schwarze Box), **None** (transparent). Inline-Style-Injektion garantiert Vorrang vor Prime Video.
  - Dauerhaft über Episoden hinweg via `MutationObserver` und Inline-Style-Injektion.
- Enthält gängige Geschwindigkeits-Voreinstellungen (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) sowie Feinabstimmung (`+` / `-` von `0.25x` bis `4.0x`).
- Der Button lässt sich per Drag & Drop frei verschieben.
- Speichert alle Einstellungen (Geschwindigkeit, Position, Farbe, Größe, Hintergrund, Status) in `localStorage`.

### Was dieses Tool nicht tut

- Es verändert die offizielle Prime Video Desktop-Anwendung in keiner Weise.
- Es umgeht keinen DRM-Schutz, entfernt keine Beschränkungen und lädt keine Videos herunter.
- Es liest, speichert oder überträgt niemals Passwörter, Cookies, Token oder private Daten.
- Es sendet keinerlei Telemetriedaten.

### Systemanforderungen

- Windows 10 oder Windows 11
- Microsoft Edge
- .NET 8 Runtime oder .NET 8 SDK

### Schnellstart

1. Laden Sie dieses Repository herunter oder klonen Sie es.
2. Führen Sie die Datei `run.cmd` aus.
3. Melden Sie sich im Edge-Fenster bei Prime Video an.
4. Starten Sie einen Film oder eine Episode.
5. Klicken Sie auf den schwebenden Button (`1.2x ●`), um Einstellungen festzulegen.

### Steuerung & Tastenkürzel

| Aktion | Methode |
|--------|--------|
| Menü öffnen / schließen | Auf den schwebenden Button klicken |
| Button verschieben | Mit gedrückter linker Maustaste ziehen |
| Geschwindigkeit ändern | Auf Voreinstellung klicken oder `+` / `-` nutzen |
| Untertitelfarbe ändern | Auf Farbfeld klicken |
| Untertitelgröße ändern | Wert (50–400) in **Size** eingeben & Enter drücken |
| Untertitelhintergrund ändern | Auf **Bg** klicken: Shadow → Solid → None |
| Untertitel-Anpassung ein/aus | Auf **Subtitles: ON / OFF** klicken oder `s` drücken |
| Untertitel-Modus (Tastatur) | `Alt + C` oder `Shift + C` |
| Geschwindigkeit um +0.1x erhöhen | `]` oder `+` oder `↑` |
| Geschwindigkeit um −0.1x verringern | `[` oder `-` oder `↓` |
| Geschwindigkeit auf 1x zurücksetzen | `\` |
| Intro / nächste Folge überspringen | `n` |
| Menü schließen | `Escape` |

### Aus dem Quellcode erstellen & Single-File-Releases

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Automatische Erstellung von Single-File-Ausführungsdateien (Hybride Architektur)
Dank unserer **Hybriden Prioritätsarchitektur** werden `speed-control.js` und `AppIcon.ico` extern geladen ODER als `<EmbeddedResource>` abgerufen.

Um `.exe`-Dateien für GitHub Releases zu generieren:
```powershell
.\publish.cmd
```
Dies generiert zwei Versionen im Ordner `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Framework-abhängig (.NET 8 Runtime erforderlich).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Vollständig unabhängig (Self-Contained).

### Datenschutz und Sicherheit

Die App startet Edge mit einem dedizierten Profil.

Das Skript speichert nur lokale Einstellungen in `localStorage`:
- ausgewählte Wiedergabegeschwindigkeit
- Koordinaten des schwebenden Buttons
- Untertitelfarbe, -größe und -hintergrundmodus
- Aktivierungsstatus der Untertitel-Anpassung
- Werbestatistiken (blockierte Werbung, gesparte Zeit)

Es werden keine privaten Daten erfasst. Hex-Regex-Prüfung (`/^#[0-9A-Fa-f]{6}$/`) verhindert XSS.

### Lizenz

Veröffentlicht unter der MIT-Lizenz. Weitere Informationen finden Sie unter [LICENSE](LICENSE).

---

<a id="francais"></a>
## Français

### Présentation

Prime Video Speed & Subtitle Controller est un utilitaire léger et open-source pour Windows qui lance Prime Video dans une fenêtre Microsoft Edge dédiée et affiche un bouton flottant élégant lorsqu'un lecteur vidéo est détecté. Ce contrôle reste discret, peut être glissé à n'importe quel endroit de l'écran et mémorise localement votre vitesse de lecture ainsi que vos préférences de couleur, taille et fond des sous-titres.

En plus de la personnalisation avancée des sous-titres, ce projet intègre un **Bouclier Anti-Pub à 5 Niveaux (Zero-Visibility Ad Shield)** qui bloque les serveurs publicitaires sur le réseau (`Network.setBlockedURLs`), intercepte les requêtes via CDP Fetch (style uBlock Origin) et coupe le son des publicités obligatoires en les accélérant à vitesse `16x`. La vitesse de lecture normale par l'utilisateur s'étend de `0.25x` à `4.0x`.

### Fonctionnalités

- Ouvre Prime Video dans une fenêtre Microsoft Edge dédiée (`--remote-debugging-address=127.0.0.1`).
- Affiche le bouton de contrôle (`1.2x ●` / `1.2x ⚡`) uniquement lorsque la vidéo est disponible.
- **Bouclier Anti-Pub à 5 Niveaux (`🛡️ Reklam Kalkanı`):**
  - **Niveau 1 (Interception CDP Fetch - style uBlock Origin):** Bloque les publicités dès la phase de requête avant tout chargement.
  - **Niveau 2 (Blocage Réseau et Traqueurs):** Bloque les serveurs publicitaires d'Amazon (`amazon-adsystem.com`), la télémétrie et les traqueurs (`Network.setBlockedURLs`).
  - **Niveau 3 (Suppression des Bannières et Comptes à Rebours):** Masque définitivement les bannières et superpositions (`opacity: 0 !important`).
  - **Niveau 4 (Couverture Opaque et Silencieux Automatique):** Coupe le son (`video.muted = true`) et masque la vidéo. Une soupape de sécurité de 45s empêche les faux positifs.
  - **Niveau 5 (Clic de Saut et Vitesse 16x):** Clique sur "Passer l'annonce" ou accélère les publicités à vitesse `16x`.
- **Masquage Automatique Intelligent:** 2 secondes après le début de la vidéo ou l'arrêt de la souris, le bouton disparaît en douceur. Réapparaît au mouvement de la souris ou en pause.
- **Indicateur Compact d'État:** Affiche la vitesse actuelle et un icône de mode:
  - **`1.2x ●`** lorsque la personnalisation des sous-titres est ACTIVE.
  - **`1.2x ⚡`** lorsque la personnalisation est INACTIVE (vitesse uniquement).
- **Icône de Barre des Tâches et de Fenêtre Personnalisée:** Attribue `AppUserModelID` (`PrimeVideoSpeedController.App`) exclusivement aux fenêtres de `msedge.exe`.
- **Personnalisateur de Sous-titres Multicouche:**
  - **Couleur:** 5 préréglages — Jaune (`#FFCC00`), Or (`#FFD700`), Blanc (`#FFFFFF`), Vert (`#00FF66`), Cyan (`#00FFFF`).
  - **Taille:** Saisissez un pourcentage de **50% à 400%** dans le champ **Size** et appuyez sur Entrée.
  - **Fond:** Cliquez sur **Bg** pour alterner: **Shadow** (boîte sombre semi-transparente), **Solid** (boîte noire opaque), **None** (transparent). L'injection de style en ligne garantit le remplacement des styles de Prime Video.
  - Persistant d'un épisode à l'autre via `MutationObserver` et injection de style en ligne.
- Préréglages de vitesse courants (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) et réglage précis (`+` / `-` de `0.25x` à `4.0x`).
- Déplacement libre du bouton par glisser-déposer.
- Mémorisation locale de la vitesse, position, couleur, taille, fond et état dans `localStorage`.

### Ce qu'il ne fait pas

- Il ne modifie pas l'application de bureau officielle Prime Video.
- Il ne contourne pas les protections DRM, ne supprime pas les restrictions et ne télécharge pas de vidéos.
- Il ne lit, ne stocke et ne transmet jamais de données privées.
- Il n'envoie aucune télémétrie.

### Prérequis

- Windows 10 ou Windows 11
- Microsoft Edge
- .NET 8 Runtime ou .NET 8 SDK

### Démarrage Rapide

1. Téléchargez ou clonez ce dépôt.
2. Exécutez le fichier `run.cmd`.
3. Connectez-vous à Prime Video dans la fenêtre dédiée.
4. Lancez un film ou une série.
5. Cliquez sur le bouton flottant (`1.2x ●`) pour régler vos préférences.

### Contrôles et Raccourcis

| Action | Méthode |
|--------|--------|
| Ouvrir / fermer le menu | Clic sur le bouton flottant |
| Déplacer le bouton | Clic gauche enfoncé et glisser |
| Changer la vitesse | Clic sur un préréglage ou `+` / `-` |
| Changer la couleur du sous-titre | Clic sur une pastille de couleur |
| Changer la taille du sous-titre | Saisir une valeur (50–400) dans **Size** & Entrée |
| Changer le fond du sous-titre | Clic sur **Bg**: Shadow → Solid → None |
| Activer/désactiver sous-titres | Clic sur **Subtitles: ON / OFF** ou touche `s` |
| Mode sous-titres (clavier) | `Alt + C` ou `Shift + C` |
| Augmenter vitesse +0.1x | `]` ou `+` ou `↑` |
| Diminuer vitesse −0.1x | `[` ou `-` ou `↓` |
| Réinitialiser vitesse à 1x | `\` |
| Passer l'intro / épisode suivant | `n` |
| Fermer le menu | `Escape` |

### Compiler à partir des sources et versions à fichier unique

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Génération automatique d'exécutables à fichier unique (Architecture Hybride)
Grâce à notre **Architecture à Priorité Hybride**, `speed-control.js` et `AppIcon.ico` sont lus en priorité depuis le dossier externe s'ils sont présents, OU chargés depuis `<EmbeddedResource>`.

Pour générer des exécutables pour GitHub Releases:
```powershell
.\publish.cmd
```
Ce script crée deux formats dans le dossier `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Dépend du framework (.NET 8 Desktop Runtime requis).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Version autonome (Self-Contained).

### Confidentialité et Sécurité

L'application démarre Edge avec un profil local dédié.

Le script injecté stocke uniquement des préférences dans `localStorage`:
- vitesse de lecture sélectionnée
- coordonnées d'affichage du bouton
- couleur, taille et mode de fond des sous-titres
- état d'activation des sous-titres
- statistiques de publicités (nombre de pubs bloquées, temps gagné)

Aucune donnée privée n'est collectée. La vérification Hex (`/^#[0-9A-Fa-f]{6}$/`) empêche toute faille XSS.

### Licence

Publié sous licence MIT. Consultez le fichier [LICENSE](LICENSE) pour plus d'informations.

---

<a id="portugues"></a>
## Português

### Visão Geral

Prime Video Speed & Subtitle Controller é uma ferramenta leve e de código aberto para Windows que abre o Prime Video em uma janela dedicada do Microsoft Edge e exibe um botão flutuante elegante assim que o reprodutor de vídeo é detectado. Esse controle não atrapalha, pode ser arrastado livremente pela tela e armazena localmente suas preferências de velocidade de reprodução, cor, tamanho e fundo das legendas.

Além da personalização avançada de legendas, este projeto possui um **Escudo de 5 Camadas de Zero Visibilidade (Zero-Visibility Ad Shield)**, que bloqueia servidores de anúncios na rede (`Network.setBlockedURLs`), intercepta requisições via CDP Fetch e silencia comerciais acelerando-os em `16x` atrás de uma cobertura opaca. A velocidade normal para o usuário vai de `0.25x` a `4.0x`.

### Recursos

- Abre o Prime Video em uma janela dedicada do Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Exibe o controle flutuante (`1.2x ●` / `1.2x ⚡`) exclusivamente durante a reprodução de vídeo.
- **Escudo de 5 Camadas de Zero Visibilidade (`🛡️ Reklam Kalkanı`):**
  - **Camada 1 (Interceptação CDP Fetch - estilo uBlock Origin):** Bloqueia anúncios antes de qualquer carregamento.
  - **Camada 2 (Bloqueador de Rede e Rastreadores):** Bloqueia servidores de anúncios da Amazon (`amazon-adsystem.com`), telemetria e rastreadores (`Network.setBlockedURLs`).
  - **Camada 3 (Destruidor de Banners e Contadores CSS):** Oculta permanentemente banners e sobreposições (`opacity: 0 !important`).
  - **Camada 4 (Cobertura Opaca e Silenciamento Automático):** Silencia o áudio (`video.muted = true`) e cobre o vídeo. Uma válvula de segurança de 45s evita falsos positivos.
  - **Camada 5 (Pular Automático e Velocidade 16x):** Clica em "Pular Anúncio" ou acelera anúncios a velocidade `16x`.
- **Ocultação Automática Inteligente:** 2 segundos após o início do vídeo ou parada do mouse, o botão desaparece suavemente. Reaparece ao mover o mouse ou pausar.
- **Indicador Compacto de Status:** Mostra a velocidade atual junto com um ícone nítido:
  - **`1.2x ●`** quando a personalização de legenda está ATIVA.
  - **`1.2x ⚡`** quando está DESATIVADA (apenas velocidade).
- **Ícone Personalizado na Barra de Tarefas:** Define `AppUserModelID` (`PrimeVideoSpeedController.App`) nas janelas do `msedge.exe`.
- **Personalizador de Legendas Multicamada:**
  - **Cor:** 5 cores predefinidas — Amarelo (`#FFCC00`), Dourado (`#FFD700`), Branco (`#FFFFFF`), Verde (`#00FF66`), Ciano (`#00FFFF`).
  - **Tamanho:** Digite um valor de **50% a 400%** no campo **Size** e pressione Enter.
  - **Fundo:** Clique em **Bg** para alternar: **Shadow** (caixa escura semitransparente), **Solid** (caixa preta opaca), **None** (transparente). Injeção direta de estilo inline garante sobressair aos estilos do Prime Video.
  - Persistente entre episódios via `MutationObserver` e injeção de estilo inline.
- Predefinições de velocidade (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) e ajustes finos (`+` / `-` de `0.25x` a `4.0x`).
- Arraste o botão flutuante livremente na tela.
- Lembra velocidade, posição, cor, tamanho, fundo e estado no `localStorage`.

### O Que Não Faz

- Não altera o aplicativo oficial de desktop do Prime Video.
- Não burla o DRM, não remove restrições e não baixa vídeos.
- Não lê, armazena ou transmite dados privados.
- Não envia telemetria.

### Requisitos

- Windows 10 ou Windows 11
- Microsoft Edge
- .NET 8 Runtime ou .NET 8 SDK

### Início Rápido

1. Baixe ou clone este repositório.
2. Execute o arquivo `run.cmd`.
3. Faça login no Prime Video na janela dedicada.
4. Inicie um filme ou episódio.
5. Clique no botão flutuante (`1.2x ●`) para ajustar suas preferências.

### Controles e Atalhos

| Ação | Método |
|------|--------|
| Abrir / fechar menu | Clique no botão flutuante |
| Mover botão | Clique com botão esquerdo e arraste |
| Alterar velocidade | Clique em uma predefinição ou use `+` / `-` |
| Alterar cor da legenda | Clique em uma amostra de cor |
| Alterar tamanho da legenda | Digite um valor (50–400) em **Size** & Enter |
| Alterar fundo da legenda | Clique em **Bg**: Shadow → Solid → None |
| Ativar/desativar legendas | Clique em **Subtitles: ON / OFF** ou pressione `s` |
| Modo de legenda (teclado) | `Alt + C` ou `Shift + C` |
| Aumentar velocidade +0.1x | `]` ou `+` ou `↑` |
| Diminuir velocidade −0.1x | `[` ou `-` ou `↓` |
| Redefinir velocidade para 1x | `\` |
| Pular introdução / próximo episódio | `n` |
| Fechar menu | `Escape` |

### Compilar a Partir do Código e Versões de Arquivo Único

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Geração automática de executáveis de arquivo único (Arquitetura Híbrida)
Graças à nossa **Arquitetura de Prioridade Híbrida**, `speed-control.js` e `AppIcon.ico` são lidos externamente OU carregados de `<EmbeddedResource>`.

Para gerar os arquivos `.exe` para o GitHub Releases:
```powershell
.\publish.cmd
```
Compila dois formatos no diretório `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Requer .NET 8 Desktop Runtime.
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Versão independente (Self-Contained).

### Privacidade e Segurança

O aplicativo inicia o Edge com um perfil local isolado.

O script salva apenas preferências no `localStorage`:
- velocidade de reprodução selecionada
- coordenadas da tela do botão flutuante
- cor, tamanho e fundo da legenda
- estado de ativação da legenda
- estatísticas de anúncios (número de anúncios bloqueados, tempo economizado)

Nenhum dado privado é coletado. A verificação rigorosa Hex (`/^#[0-9A-Fa-f]{6}$/`) evita vulnerabilidades XSS.

### Licença

Distribuído sob a Licencia MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---

<a id="zh"></a>
## 中文

### 概览

Prime Video Speed & Subtitle Controller 是一个开源、轻量级的 Windows 辅助工具。它在 Microsoft Edge 的独立应用窗口中打开 Prime Video，并在检测到视频播放器时添加一个优雅的悬浮控制按钮。该控制按钮界面精简，可自由拖拽放置在屏幕任何位置，并能本地保存您的播放倍速、字幕颜色、尺寸与背景样式偏好。

除了高级字幕自定义功能外，本项目还内置了**5层零可见广告屏蔽盾 (Zero-Visibility Ad Shield)**。它不仅能在底层网络拦截广告跟踪器 (`Network.setBlockedURLs`)，利用 CDP Fetch 拦截广告请求，更能在不可跳过的广告播放时自动开启静音并以 `16x` 极速快进消除广告。常规视频播放倍速调整范围为 `0.25x` 至 `4.0x`。

### 功能特性

- 在专用的 Microsoft Edge 独立窗口 (`--remote-debugging-address=127.0.0.1`) 中打开 Prime Video。
- 仅在视频播放器准备就绪时显示悬浮按钮 (`1.2x ●` / `1.2x ⚡`)。
- **5层零可见广告屏蔽盾 (`🛡️ Reklam Kalkanı`):**
  - **第 1 层 (CDP Fetch 请求拦截 - uBlock Origin 风格):** 在请求阶段拦截广告，返回空的 VAST/VPAID XML 响应。
  - **第 2 层 (网络广告与跟踪拦截):** 拦截 Amazon 广告服务器 (`amazon-adsystem.com`)、遥测及追踪请求 (`Network.setBlockedURLs`)。
  - **第 3 层 (CSS 广告横幅消灭):** 永久隐藏“广告 1/2”倒计时提示及弹窗 (`opacity: 0 !important`)。
  - **第 4 层 (不透明遮罩与自动静音):** 遇到强制广告时自动静音 (`video.muted = true`) 并遮挡画面。45 秒安全阀门防误判。
  - **第 5 层 (秒跳与 16 倍速极速消化):** 自动点击“跳过广告”或将广告加速至 `16x` 极速消化。
- **播放时智能隐藏:** 视频播放或鼠标停止移动 2 秒后悬浮按钮自动隐藏，移动鼠标或暂停时立即重新出现。
- **紧凑型状态指示器:** 清晰显示当前倍速与运作模式：
  - **`1.2x ●`**：当自定义字幕开启时（圆点发光）。
  - **`1.2x ⚡`**：当字幕自定义关闭时（仅倍速控制）。
- **自定义任务栏与窗口图标:** 利用 Win32 COM 为 `msedge.exe` 窗口赋予独立 `AppUserModelID` (`PrimeVideoSpeedController.App`)。
- **高级字幕自定义器:**
  - **颜色：** 5 种预设 — 黄色 (`#FFCC00`)、金色 (`#FFD700`)、纯白 (`#FFFFFF`)、荧光绿 (`#00FF66`)、青蓝 (`#00FFFF`)。
  - **尺寸：** 在 **Size** 输入框中输入 **50% 至 400%** 的任意数值并按回车。
  - **背景：** 点击 **Bg** 按钮切换：**Shadow** (半透明暗色背景)、**Solid** (不透明黑色背景)、**None** (透明背景)。内联样式直接注入，确保覆盖 Prime Video 自带样式。
  - 通过 `MutationObserver` 和内联样式注入，在剧集切换时保持持久生效。
- 内置常用倍速预设 (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) 及精细调节按钮 (`+` / `-`)。
- 支持自由拖动悬浮按钮坐标。
- 将倍速、坐标、字幕颜色、尺寸、背景及开关状态本地存储于 `localStorage`。

### 它不做什么

- 不会修改官方 Prime Video 桌面客户端文件。
- 不会破解 DRM 版权保护、解除限制或下载视频。
- 不会读取、记录或发送任何隐私数据。
- 不会发送遥测数据。

### 系统要求

- Windows 10 或 Windows 11
- Microsoft Edge 浏览器
- .NET 8 Runtime 或 .NET 8 SDK

### 快速入门

1. 下载或克隆本代码仓库到本地。
2. 双击运行 `run.cmd`。
3. 在打开的专用 Edge 窗口中登录 Prime Video 账户。
4. 点击播放影片。
5. 点击 `1.2x ●` 悬浮按键即可调整设置。

### 控制与快捷键

| 操作 | 方法 |
|------|------|
| 打开 / 关闭菜单 | 点击悬浮按钮 |
| 移动按钮 | 按住左键拖拽悬浮按钮 |
| 调节播放倍速 | 点击预设倍速或使用 `+` / `-` |
| 切换字幕颜色 | 点击颜色圆点 |
| 调节字幕尺寸 | 在 **Size** 框输入数值 (50–400) 并回车 |
| 切换字幕背景 | 点击 **Bg** 按钮切换: Shadow → Solid → None |
| 开启/关闭字幕美化 | 点击 **Subtitles: ON / OFF** 或按 `s` 键 |
| 字幕美化快捷键 | `Alt + C` 或 `Shift + C` |
| 倍速增加 +0.1x | `]` 或 `+` 或 `↑` |
| 倍速减少 −0.1x | `[` 或 `-` 或 `↓` |
| 重置倍速为 1x | `\` |
| 跳过片头 / 下一集 | `n` |
| 关闭菜单 | `Escape` |

### 从源码编译与单文件发布版

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### 单文件可执行程序(`.exe`)自动生成（混合优先架构）
借助我们的**混合优先架构**，`speed-control.js` 与 `AppIcon.ico` 支持外部读取或嵌入资源 (`<EmbeddedResource>`) 加载。

运行一键构建脚本：
```powershell
.\publish.cmd
```
在 `publish/` 目录下生成：
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**：框架依赖版。
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**：完全独立版（Self-Contained）。

### 隐私与安全

本工具启动独立 Edge 配置。

脚本仅在本地 `localStorage` 保存设置：
- 播放倍速
- 悬浮按钮坐标
- 字幕颜色、尺寸及背景模式
- 字幕开启状态
- 广告拦截统计

不收集任何私密数据。十六进制正则校验 (`/^#[0-9A-Fa-f]{6}$/`) 杜绝 XSS 风险。

### 许可证

基于 MIT 开源许可证发布。详情请见 [LICENSE](LICENSE) 文件。

---

<a id="hindi"></a>
## हिन्दी

### परिचय

Prime Video Speed & Subtitle Controller विंडोज़ के लिए एक ओपन-सोर्स और हल्का टूल है जो Prime Video को एक समर्पित Microsoft Edge ऐप विंडो में खोलता है और वीडियो प्लेबैक का पता चलने पर स्क्रीन पर एक आकर्षक, ड्रैग करने योग्य फ्लोटिंग बटन दिखाता है। यह नियंत्रण बटन स्क्रीन पर कहीं भी ले जाया जा सकता है और आपके चुने हुए प्लेबैक स्पीड, सबटाइटल रंग, आकार और बैकग्राउंड को सहेजता है।

सबटाइटल कस्टमाइज़र के अलावा, इस प्रोजेक्ट में **5-Layer Zero-Visibility Ad Shield** शामिल है। यह CDP Fetch द्वारा विज्ञापनों को रोकता है (`Network.setBlockedURLs`) और अनिवार्य विज्ञापनों को स्वतः म्यूट करके `16x` गति से समाप्त कर देता है। देखने की गति को आप `0.25x` से `4.0x` तक बदल सकते हैं।

### सुविधाएँ

- Prime Video को समर्पित Microsoft Edge विंडो (`--remote-debugging-address=127.0.0.1`) में खोलता है।
- स्पीड और सबटाइटल बटन (`1.2x ●` / `1.2x ⚡`) केवल वीडियो चलने पर दिखाता है।
- **5-Layer Zero-Visibility Ad Shield (`🛡️ Reklam Kalkanı`):**
  - **लेयर 1 (CDP Fetch):** विज्ञापनों को लोड होने से पहले अनुरोध चरण में ही ब्लॉक करता है।
  - **लेयर 2 (नेटवर्क ब्लॉकर):** Amazon विज्ञापन सर्वर और ट्रैकर को रोकता है।
  - **लेयर 3 (बैनर रिमूवर):** "Ad 1 of 2" और काउंटडाउन बैनर छुपाता है।
  - **लेयर 4 (कवर और ऑटो-म्यूट):** विज्ञापनों के दौरान ऑडियो म्यूट करता है। 45s सेफ्टी वाल्व गलत पहचान से बचाता है।
  - **लेयर 5 (ऑटो-स्किप और 16x स्पीड):** "Skip Ad" पर क्लिक करता है या 16x गति से विज्ञापन समाप्त करता है।
- **स्मार्ट ऑटो-हाइड:** वीडियो शुरू होने के 2 सेकंड बाद बटन स्वतः गायब हो जाता है। माउस हिलाने पर पुनः दिखाई देता है।
- **कॉम्पैक्ट स्टेटस इंडिकेटर:** आपकी वर्तमान गति और मोड दिखाता है।
- **सबटाइटल कस्टमाइज़र:**
  - **रंग:** 5 रंग प्रीसेट — पीला (`#FFCC00`), सुनहरा (`#FFD700`), सफ़ेद (`#FFFFFF`), हरा (`#00FF66`), सियान (`#00FFFF`)।
  - **आकार (Size):** **Size** बॉक्स में **50% से 400%** तक मान दर्ज करें और Enter दबाएँ।
  - **बैकग्राउंड (Bg):** **Bg** पर क्लिक करें: **Shadow** (हल्का काला), **Solid** (गहरा काला), **None** (पारदर्शी)।
- स्पीड प्रीसेट (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) और बारीक समायोजन (`+` / `-`) शामिल हैं।
- सेटिंग्स को स्थानीय `localStorage` में सुरक्षित रखता है।

### यह क्या नहीं करता है

- आधिकारिक Prime Video डेस्कटॉप ऐप में कोई बदलाव नहीं करता।
- DRM सुरक्षा नहीं तोड़ता और वीडियो डाउनलोड नहीं करता।
- निजी डेटा या कुकीज़ नहीं पढ़ता।
- टेलीमेट्री डेटा बाहर नहीं भेजता।

### आवश्यकताएँ

- Windows 10 या Windows 11
- Microsoft Edge
- .NET 8 Runtime या .NET 8 SDK

### त्वरित शुरुआत

1. इस रिपॉजिटरी को डाउनलोड या क्लोन करें।
2. `run.cmd` फ़ाइल को चलाएँ।
3. Edge विंडो में अपने Prime Video खाते में लॉगिन करें।
4. कोई भी फ़िल्म या एपिसोड चलाएँ।
5. बटन पर क्लिक करके सेटिंग्स बदलें।

### नियंत्रण और शॉर्टकट

| कार्य | तरीका |
|-------|-------|
| मेनू खोलें / बंद करें | फ्लोटिंग बटन पर क्लिक करें |
| बटन ले जाएँ | बटन पर बायां क्लिक दबाकर खींचें |
| गति बदलें | प्रीसेट पर क्लिक करें या `+` / `-` का उपयोग करें |
| सबटाइटल रंग बदलें | रंग पर क्लिक करें |
| सबटाइटल आकार बदलें | **Size** में मान (50–400) दर्ज करें और Enter दबाएँ |
| सबटाइटल बैकग्राउंड बदलें | **Bg** पर क्लिक करें: Shadow → Solid → None |
| सबटाइटल ऑन / ऑफ | **Subtitles: ON / OFF** पर क्लिक करें या `s` दबाएँ |
| सबटाइटल मोड (कीबोर्ड) | `Alt + C` या `Shift + C` |
| गति +0.1x बढ़ाएँ | `]` या `+` या `↑` |
| गति −0.1x घटाएँ | `[` या `-` या `↓` |
| गति 1x रीसेट करें | `\` |
| इंट्रो / अगला एपिसोड स्किप करें | `n` |
| मेनू बंद करें | `Escape` |

### सोर्स से बिल्ड करें और सिंगल-फ़ाइल रिलीज़

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### स्वचालित सिंगल-फ़ाइल `.exe` निर्माण
`publish.cmd` चलाकर `publish/` फ़ोल्डर में दो रिलीज़ फ़ाइलें बनाएँ:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**

### गोपनीयता और सुरक्षा

इंजेक्ट की गई स्क्रिप्ट केवल `localStorage` में प्राथमिकताओं को सहेजती है। हेक्साडेसिमल जांच XSS से सुरक्षा प्रदान करती है।

### लाइसेंस

MIT लाइसेंस के अंतर्गत जारी। [LICENSE](LICENSE) देखें।

---

<a id="arabic"></a>
## العربية

### نظرة عامة

Prime Video Speed & Subtitle Controller هو أداة مفتوحة المصدر وخفيفة لنظام Windows تفتح Prime Video داخل نافذة Microsoft Edge مخصصة، وتضيف زر عائم أنيق عند اكتشاف مشغل الفيديو. يحفظ الزر العائم سرعة التشغيل ولون وحجم وخلفية الترجمة محلياً.

بالإضافة إلى تخصيص الترجمة المتقدم، يشتمل هذا المشروع على **درع إعلانات مخفي من 5 طبقات (Zero-Visibility Ad Shield)** يقوم بحجب خوادم الإعلانات شبكياً (`Network.setBlockedURLs`) وعبر CDP Fetch ويكتم صوت الإعلانات الإجبارية ويسرعها إلى `16x`.

### الميزات

- يفتح Prime Video في نافذة Microsoft Edge مخصصة (`--remote-debugging-address=127.0.0.1`).
- يظهر زر التحكم (`1.2x ●` / `1.2x ⚡`) فقط أثناء تشغيل الفيديو.
- **درع إعلانات مخفي من 5 طبقات (`🛡️ Reklam Kalkanı`):**
  - **الطبقة 1 (اعتراض CDP Fetch):** يحجب الإعلانات في مرحلة الطلب قبل التحميل.
  - **الطبقة 2 (حجب الخوادم شبكياً):** يحجب خوادم إعلانات Amazon والتتبع.
  - **الطبقة 3 (إزالة اللافتات):** يخفي لافتات الإعلانات والعد التنازلي.
  - **الطبقة 4 (الغطاء والكتم التلقائي):** يكتم الصوت ويغطي الفيديو. صمام أمان 45 ثانية يمنع الحجب الخاطئ.
  - **الطبقة 5 (التخطي والتسريع 16x):** ينقر على "تخطي الإعلان" أو يسرع الإعلان إلى 16x.
- **الإخفاء التلقائي الذكي:** يختفي الزر بعد ثانيتين من التشغيل أو توقف الماوس.
- **تخصيص الترجمة المتقدم:**
  - **اللون:** 5 ألوان جاهزة — الأصفر (`#FFCC00`)، الذهبي (`#FFD700`)، الأبيض (`#FFFFFF`)، الأخضر (`#00FF66`)، الأزرق (`#00FFFF`).
  - **الحجم:** أدخل أي نسبة من **50% إلى 400%** في مربع **Size** واضغط Enter.
  - **الخلفية:** انقر على **Bg** للتبديل: **Shadow** (خلفية داكنة شبه شفافة)، **Solid** (خلفية سوداء معتمة)، **None** (شفافة).
- يتضمن إعدادات سرعة مسبقة (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) وأزرار ضبط دقيقة (`+` / `-`).
- يحفظ التفضيلات محلياً في `localStorage`.

### ما لا يفعله البرنامج

- لا يعدل تطبيق Prime Video الرسمي.
- لا يتجاوز DRM ولا ينزل الفيديوهات.
- لا يقرأ أو يرسل بيانات شخصية.
- لا يرسل بيانات تتبع.

### متطلبات النظام

- Windows 10 أو Windows 11
- متصفح Microsoft Edge
- .NET 8 Runtime أو .NET 8 SDK

### البدء السريع

1. قم بتنزيل أو نسخ المستودع.
2. شغل ملف `run.cmd`.
3. سجل الدخول إلى Prime Video في النافذة المخصصة.
4. ابدأ تشغيل أي فيلم أو حلقة.
5. اضبط إعداداتك من الزر العائم.

### أدوات التحكم والاختصارات

| الإجراء | الطريقة |
|---------|---------|
| فتح / إغلاق القائمة | انقر على الزر العائم |
| تحريك الزر | اضغط بالنقر الأيسر واسحب |
| تغيير السرعة | انقر على سرعة مسبقة أو استخدم `+` / `-` |
| تغيير لون الترجمة | انقر على أي عينة لون |
| تغيير حجم الترجمة | أدخل قيمة (50–400) في **Size** واضغط Enter |
| تغيير خلفية الترجمة | انقر على **Bg**: Shadow → Solid → None |
| تفعيل / إيقاف الترجمة | انقر على **Subtitles: ON / OFF** أو اضغط `s` |
| وضع الترجمة (لوحة المفاتيح) | `Alt + C` أو `Shift + C` |
| زيادة السرعة +0.1x | `]` أو `+` أو `↑` |
| تقليل السرعة −0.1x | `[` أو `-` أو `↓` |
| إعادة تعيين السرعة إلى 1x | `\` |
| تخطي المقدمة / الحلقة التالية | `n` |
| إغلاق القائمة | `Escape` |

### البناء من المصدر وإصدارات الملف الواحد (.exe)

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### إنشاء ملفات تشغيل وحيدة ومستقلة
قم بتشغيل `publish.cmd` لإنشاء الملفات في مجلد `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**

### الخصوصية والأمان

يحفظ السكربت فقط التفضيلات محلياً في `localStorage`.

### الترخيص

نُشر بموجب ترخيص MIT. راجع ملف [LICENSE](LICENSE).

---

<a id="russian"></a>
## Русский

### Обзор

Prime Video Speed & Subtitle Controller — это открытая легкая утилита для Windows, которая запускает Prime Video в отдельном окне Microsoft Edge и добавляет плавающую кнопку управления. Кнопка не перекрывает контент, легко перетаскивается и сохраняет ваши настройки скорости, цвета, размера и фона субтитров локально.

В дополнение к расширенной настройке субтитров, проект включает **5-уровневый рекламный щит нулевой видимости (Zero-Visibility Ad Shield)**, который блокирует рекламу на сетевом уровне (`Network.setBlockedURLs`) и через CDP Fetch, выключает звук и ускоряет рекламу до `16x`.

### Возможности

- Открывает Prime Video в отдельном окне Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Показывает кнопку управления (`1.2x ●` / `1.2x ⚡`) исключительно во время воспроизведения видео.
- **5-уровневый рекламный щит нулевой видимости (`🛡️ Reklam Kalkanı`):**
  - **Уровень 1 (Перехват CDP Fetch):** Блокирует рекламные запросы до загрузки.
  - **Уровень 2 (Сетевая блокировка):** Блокирует серверы рекламы Amazon и трекеры (`Network.setBlockedURLs`).
  - **Уровень 3 (Удаление баннеров):** Скрывает рекламные надписи и таймеры (`opacity: 0 !important`).
  - **Уровень 4 (Оверлей и авто-мьют):** Выключает звук и скрывает видео за непрозрачным оверлеем. Предохранительный клапан на 45 сек. предотвращает ошибки.
  - **Уровень 5 (Авто-клик и скорость 16x):** Нажимает "Пропустить рекламу" или ускоряет рекламу до `16x`.
- **Умное автоскрытие:** Через 2 секунды после начала видео или остановки мыши кнопка плавно исчезает.
- **Компактный индикатор статуса:** Отображает скорость и статус субтитров.
- **Расширенный настройщик субтитров:**
  - **Цвет:** 5 пресетов — Желтый (`#FFCC00`), Золотой (`#FFD700`), Белый (`#FFFFFF`), Зеленый (`#00FF66`), Голубой (`#00FFFF`).
  - **Размер:** Введите любое значение от **50% до 400%** в поле **Size** и нажмите Enter.
  - **Фон:** Нажмите **Bg** для переключения: **Shadow** (полупрозрачный), **Solid** (непрозрачный черный), **None** (прозрачный). Инъекция инлайн-стилей гарантирует перекрытие стилей Prime Video.
- Пресеты скорости (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) и точная настройка (`+` / `-`).
- Сохраняет настройки в `localStorage`.

### Чего утилита НЕ делает

- Не изменяет приложение Prime Video.
- Не обходит DRM и не скачивает видео.
- Не читает и не передает личные данные.
- Не отправляет телеметрию.

### Системные требования

- Windows 10 или Windows 11
- Microsoft Edge
- .NET 8 Runtime или .NET 8 SDK

### Быстрый старт

1. Скачайте или клонируйте репозиторий.
2. Запустите `run.cmd`.
3. Войдите в аккаунт Prime Video в отдельном окне.
4. Включите видео и настройте параметры через кнопку.

### Управление и горячие клавиши

| Действие | Способ |
|----------|--------|
| Открыть / закрыть меню | Нажать на плавающую кнопку |
| Переместить кнопку | Зажать ЛКМ и перетащить |
| Изменить скорость | Нажать пресет или использовать `+` / `-` |
| Изменить цвет субтитров | Нажать на цветную метку |
| Изменить размер субтитров | Ввести значение (50–400) в **Size** и нажать Enter |
| Изменить фон субтитров | Нажать **Bg**: Shadow → Solid → None |
| Вкл/выкл субтитры | Нажать **Subtitles: ON / OFF** или клавишу `s` |
| Режим субтитров (клавиатура) | `Alt + C` или `Shift + C` |
| Увеличить скорость +0.1x | `]` или `+` или `↑` |
| Уменьшить скорость −0.1x | `[` или `-` или `↓` |
| Сбросить скорость на 1x | `\` |
| Пропустить интро / след. серия | `n` |
| Закрыть меню | `Escape` |

### Сборка из исходного кода и single-file релизы

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Автоматическая генерация исполняемых файлов (.exe)
Запустите `publish.cmd` для создания файлов в папке `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**

### Конфиденциальность и безопасность

Скрипт сохраняет настройки исключительно в `localStorage`. Проверка Hex-regex защищает от XSS.

### Лицензия

Распространяется под лицензией MIT. См. [LICENSE](LICENSE).

---

<a id="japanese"></a>
## 日本語

### 概要

Prime Video Speed & Subtitle Controller は、Windows 向けのオープンソースで軽量なアシスタントツールです。Prime Video を Microsoft Edge の専用アプリウィンドウで起動し、動画プレイヤーが検出された際に、洗練されたドラッグ可能な操作ボタンを表示します。再生速度、字幕カラー、字幕サイズ、字幕背景の設定をローカルに記憶します。

高度な字幕カスタマイズ機能に加え、当プロジェクトには**5レイヤー広告遮断シールド (Zero-Visibility Ad Shield)**が搭載されています。CDP Fetch およびネットワーク層での広告遮断 (`Network.setBlockedURLs`)、強制広告の自動消音＆`16倍速`高速消化を実行します。

### 機能

- 専用の Microsoft Edge アプリウィンドウ (`--remote-debugging-address=127.0.0.1`) で Prime Video を開きます。
- 再生速度および字幕ボタン (`1.2x ●` / `1.2x ⚡`) を動画再生中のみ表示します。
- **5レイヤー広告遮断シールド (`🛡️ Reklam Kalkanı`):**
  - **レイヤー 1 (CDP Fetch):** リクエスト段階で広告を遮断します。
  - **レイヤー 2 (ネットワーク遮断):** Amazon 広告サーバーや追跡ドメインをブロックします。
  - **レイヤー 3 (CSS バナー削除):** カウントダウンやオーバーレイを消去します (`opacity: 0 !important`)。
  - **レイヤー 4 (カバー＆自動ミュート):** 音声を消音し映像をカバー。45秒セーフティバルブで誤作動を防ぎます。
  - **レイヤー 5 (自動スキップ＆16倍速):** スキップボタンの自動クリックまたは 16倍速処理。
- **再生中のスマート自動非表示:** 再生開始またはマウス停止の 2秒後にボタンがフェードアウトします。
- **高度な字幕カスタマイザー:**
  - **カラー:** 5つのプリセット — イエロー (`#FFCC00`)、ゴールド (`#FFD700`)、ホワイト (`#FFFFFF`)、グリーン (`#00FF66`)、シアン (`#00FFFF`)。
  - **サイズ:** **Size** 入力欄に **50% 〜 400%** の数値を入力し Enter を押します。
  - **背景:** **Bg** をクリックして切替: **Shadow** (半透明暗色)、**Solid** (不透明黒)、**None** (透明)。インラインスタイル注入により公式スタイルを確実に上書き。
- 速度プリセット (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) および微調整 (`+` / `-`)。
- 設定を `localStorage` に保持します。

### 行わないこと

- 公式 Prime Video アプリを変更しません。
- DRM 回避や動画ダウンロードは行いません。
- 個人データや Cookie を収集・送信しません。
- テレメトリーデータは送信しません。

### システム要件

- Windows 10 または Windows 11
- Microsoft Edge
- .NET 8 Runtime または .NET 8 SDK

### クイックスタート

1. 本リポジトリをダウンロードまたはクローンします。
2. `run.cmd` を実行します。
3. 専用 Edge ウィンドウで Prime Video にサインインします。
4. 動画を再生し、ボタンから設定を行います。

### 操作とショートカット

| 操作 | 方法 |
|------|------|
| メニュー開閉 | フローティングボタンをクリック |
| ボタン移動 | 左クリックでドラッグ |
| 速度変更 | プリセットクリックまたは `+` / `-` |
| 字幕カラー変更 | カラーチップをクリック |
| 字幕サイズ変更 | **Size** に数値 (50–400) を入力し Enter |
| 字幕背景変更 | **Bg** をクリック: Shadow → Solid → None |
| 字幕ON/OFF | **Subtitles: ON / OFF** をクリックまたは `s` キー |
| 字幕モード (キーボード) | `Alt + C` または `Shift + C` |
| 速度 +0.1x | `]` または `+` または `↑` |
| 速度 −0.1x | `[` または `-` または `↓` |
| 速度 1x リセット | `\` |
| イントロ / 次話スキップ | `n` |
| メニューを閉じる | `Escape` |

### ソースからのビルドと単一ファイルリリース

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### 単一ファイル実行可能(`.exe`)の自動生成
`publish.cmd` を実行し `publish/` ディレクトリに生成します：
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**

### プライバシーとセキュリティ

スクリプトは設定のみを `localStorage` に保存します。厳格な Hex regex 検証により XSS を防止します。

### ライセンス

MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) をご確認ください。

---

<a id="indonesian"></a>
## Bahasa Indonesia

### Ringkasan

Prime Video Speed & Subtitle Controller adalah alat bantu open-source yang ringan untuk Windows. Alat ini membuka Prime Video di jendela aplikasi Microsoft Edge khusus dan menampilkan tombol kontrol saat pemutar video terdeteksi. Tombol kontrol menyimpan preferensi kecepatan pemutaran, warna, ukuran, dan latar belakang subtitle secara lokal.

Selain kustomisasi subtitle tingkat lanjut, proyek ini dilengkapi **Perisai Iklan 5 Lapis Tanpa Tampilan (Zero-Visibility Ad Shield)**. Perisai ini memblokir iklan di tingkat jaringan (`Network.setBlockedURLs`) dan CDP Fetch, membisukan iklan wajib, serta mempercepat pemutaran iklan hingga `16x`.

### Fitur

- Membuka Prime Video di jendela aplikasi Microsoft Edge khusus (`--remote-debugging-address=127.0.0.1`).
- Menampilkan tombol kecepatan & subtitle (`1.2x ●` / `1.2x ⚡`) hanya saat video diputar.
- **Perisai Iklan 5 Lapis Tanpa Tampilan (`🛡️ Reklam Kalkanı`):**
  - **Lapis 1 (CDP Fetch):** Memblokir iklan pada tahap permintaan sebelum dimuat.
  - **Lapis 2 (Pemblokir Jaringan):** Memblokir server iklan Amazon dan pelacak.
  - **Lapis 3 (Penghancur Spanduk):** Menyembunyikan spanduk hitung mundur dan penutup (`opacity: 0 !important`).
  - **Lapis 4 (Penutup & Bisu Otomatis):** Membisukan suara dan menutup video. Katup pengaman 45 detik mencegah kesalahan penutupan.
  - **Lapis 5 (Klik Otomatis & Kecepatan 16x):** Mengklik "Lewati Iklan" atau mempercepat iklan ke `16x`.
- **Sembunyi Otomatis (Auto-Hide):** Tombol menghilang perlahan 2 detik setelah pemutaran dimulai atau mouse berhenti.
- **Kustomisasi Subtitle Tingkat Lanjut:**
  - **Warna:** 5 prasetel — Kuning (`#FFCC00`), Emas (`#FFD700`), Putih (`#FFFFFF`), Hijau (`#00FF66`), Cyan (`#00FFFF`).
  - **Ukuran:** Masukkan nilai **50% hingga 400%** di kotak **Size** lalu tekan Enter.
  - **Latar Belakang:** Klik **Bg** untuk beralih: **Shadow** (latar gelap transparan), **Solid** (latar hitam pekat), **None** (transparan). Penginjeksian gaya inline memastikan gaya bawaan Prime Video tertimpa.
- Prasetel kecepatan (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) dan tombol penyesuaian presisi (`+` / `-`).
- Menyimpan preferensi di `localStorage`.

### Apa Yang Tidak Dilakukan

- Tidak memodifikasi aplikasi resmi Prime Video.
- Tidak menembus DRM dan tidak mengunduh video.
- Tidak membaca atau mengirimkan data pribadi.
- Tidak mengirimkan data telemetri.

### Persyaratan Sistem

- Windows 10 atau Windows 11
- Peramban Microsoft Edge
- .NET 8 Runtime atau .NET 8 SDK

### Mulai Cepat

1. Unduh atau klon repositori ini.
2. Jalankan `run.cmd`.
3. Masuk ke Prime Video pada jendela Edge yang terbuka.
4. Mulai putar video dan atur preferensi melalui tombol.

### Kontrol & Pintasan

| Tindakan | Metode |
|----------|--------|
| Buka / Tutup Menu | Klik tombol fluktuatif |
| Pindahkan Tombol | Tahan klik kiri dan geser |
| Ubah Kecepatan Pemutaran | Klik prasetel atau gunakan `+` / `-` |
| Ubah Warna Subtitle | Klik pilihan warna |
| Ubah Ukuran Subtitle | Masukkan nilai (50–400) di kotak **Size** lalu tekan Enter |
| Ubah Latar Subtitle | Klik **Bg**: Shadow → Solid → None |
| Subtitle Aktif / Nonaktif | Klik **Subtitles: ON / OFF** atau tekan `s` |
| Mode Subtitle (Papan Ketik) | `Alt + C` atau `Shift + C` |
| Kecepatan +0.1x | `]` atau `+` atau `↑` |
| Kecepatan −0.1x | `[` atau `-` atau `↓` |
| Kecepatan Reset ke 1x | `\` |
| Lewati Intro / Episode Berikutnya | `n` |
| Tutup Menu | `Escape` |

### Membangun dari Kode Sumber dan Rilis Satu Berkas (.exe)

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Pembuatan Otomatis Berkas Executable Tunggal
Jalankan `publish.cmd` untuk mengompilasi berkas rilis ke folder `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**

### Privasi dan Keamanan

Skrip hanya menyimpan preferensi di `localStorage`. Validasi regex Heksadesimal mencegah kerentanan XSS.

### Lisensi

Diterbitkan di bawah Lisensi MIT. Lihat berkas [LICENSE](LICENSE).
