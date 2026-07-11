# Prime Video Speed & Subtitle Controller

**Prime Video Speed & Subtitle Controller** is an open-source, lightweight Windows helper that opens Prime Video in a dedicated Microsoft Edge app window, adding a clean playback speed control (`1.2x ●` / `1.2x ⚡`), an automatic subtitle color stabilizer, and an always-on **5-Layer Zero-Visibility Ad Shield** (`🛡️ Reklam Kalkanı`).

It is built for viewers who want a seamless, commercial-free streaming experience with simple speed selection and consistent yellow subtitles without modifying the official Prime Video app, bypassing DRM, downloading video, or installing a browser extension.

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
- Shows the speed & subtitle control (`1.2x ●` / `1.2x ⚡`) only when video playback is available.
- **Always-On 5-Layer Zero-Visibility Ad Shield (`🛡️ Reklam Kalkanı`):**
  - **Layer 1 (CDP Fetch Interception - uBlock Origin Style):** Uses Chromium's `Fetch.enable` and `Fetch.requestPaused` protocol domains to block ads at the request stage before any bytes are loaded. Returns custom empty VAST/VPAID response XMLs for stitched player-level ads.
  - **Layer 2 (Network-Level Blocker):** Blocks Amazon ad servers (`amazon-adsystem.com`), telemetry, and tracking networks right at the Chromium network layer (`Network.setBlockedURLs`).
  - **Layer 3 (CSS Banner & Countdown Destroyer):** Permanently removes "Ad 1 of 2", ad countdown banners, and ad overlays (`opacity: 0 !important`).
  - **Layer 4 (Opaque Ad Cover & Auto-Mute):** During unskippable stitched ad breaks, instantly mutes commercial audio (`video.muted = true`) and hides the ad stream behind an opaque cover overlay. (A `<canvas>` freeze-frame is deliberately NOT used: Prime Video's stream is DRM-protected, so capturing a frame throws a `SecurityError` in every browser.) The shield only engages when a real ad countdown (e.g. `0:27`) is visible, and a 45-second safety valve with a cooldown guarantees a stuck or false detection can never lock normal playback behind the cover.
  - **Layer 5 (Auto-Skip Clicker & 16x Hyper-Speed):** Automatically clicks "Skip Ad" the millisecond it appears, or accelerates unskippable ads at `16x` speed (`video.playbackRate = 16`) to finish them in seconds before restoring normal playback (`1.2x`).
- **Smart Auto-Hide During Playback:** Exactly 2 seconds after video playback begins (`play`/`playing`) or the mouse stops moving, the floating button smoothly fades out (`opacity: 0`) for an ultra-clean viewing experience matching native Prime Video controls. Reappears instantly on mouse movement or pause.
- **Compact Icon Indicator:** Displays your current speed and a clean indicator icon:
  - **`1.2x ●`** when custom subtitle styling is ON (the dot glows in your selected subtitle color).
  - **`1.2x ⚡`** when subtitle override is OFF (speed control only).
- **Custom Taskbar & Window Icon:** Uses Win32 COM (`SHGetPropertyStoreForWindow`) to set `AppUserModelID` (`PrimeVideoSpeedController.App`) exclusively for dedicated `msedge.exe` windows, ensuring clean taskbar grouping.
- **Multi-layer Subtitle Stabilizer:** Prevents Prime Video from resetting subtitle styles across episodes using `MutationObserver` and dynamic CSS (`!important`).
- **Two-Section Glassmorphism Menu:** Cleanly separates `⚡ Hız Kontrolü` (Speed Control) and `💬 Altyazı Rengi` (Subtitle Color).
- Includes common speed presets: `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` and fine adjustments (`+` / `-` from `0.25x` to `4.0x`).
- Includes 5 preset subtitle colors: **Yellow (`#FFCC00`)**, **Gold (`#FFD700`)**, **White (`#FFFFFF`)**, **Green (`#00FF66`)**, and **Cyan (`#00FFFF`)**.
- Lets you drag the floating button to a comfortable place on screen.
- Remembers selected speed, button position, subtitle color, and toggle state locally.
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

- Click the floating button to open or close the menu.
- Drag the floating button while holding the mouse button to move it.
- Click any speed preset or use `+` / `-` for `0.1x` speed changes (`0.25x` to `4.0x`).
- Click the subtitle toggle switch or any color swatch (`Yellow`, `Gold`, `White`, `Green`, `Cyan`).
- Press `Alt + C` or `Shift + C` to instantly toggle subtitle styling ON/OFF.
- Press `]` to increase speed by `0.1x`.
- Press `[` to decrease speed by `0.1x`.
- Press `\` to reset speed to `1x`.
- Press `Escape` to close the menu.

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
- selected subtitle color and status

No credentials, cookies, tokens, viewing history, or personal data are collected or transmitted. Strict hexadecimal regex filtering (`/^#[0-9A-Fa-f]{6}$/`) protects against any XSS attempts.

### License

Released under the MIT License. See [LICENSE](LICENSE).

---

<a id="turkce"></a>
## Türkçe

### Genel Bakış

Prime Video Speed & Subtitle Controller, Prime Video'yu Microsoft Edge üzerinde özel bir uygulama penceresinde açan ve ekranda gerçek bir video oynatıcı algılandığında zarif, kaydırılabilir bir buton gösteren açık kaynaklı ve hafif bir Windows aracıdır. Bu kontrol butonu izleme keyfinizi bölmez, ekranda dilediğiniz konuma taşınabilir ve seçtiğiniz oynatma hızı ile altyazı rengi tercihlerinizi yerel olarak hatırlar.

Çok katmanlı altyazı sabitleyicisine (varsayılan olarak **Sarı `#FFCC00`**) ek olarak, projemizde entegre bir **5 Katmanlı Sıfır Görünürlük Reklam Kalkanı (Zero-Visibility Ad Shield)** yer alır. Bu kalkan, reklam sunucu isteklerini doğrudan ağ isteği seviyesinde Chromium CDP Fetch protokolü (uBlock Origin tarzı) aracılığıyla engelleyip, diğer takip ağlarını engeller ve atlanamayan reklamları opak bir perde arkasında 16x hiper sessiz hızda eritir (`Zero-Visibility`). Kullanıcının normal izleme hızı ise serbestçe `0.25x` ile `4.0x` arasında ayarlanabilir.

### Özellikler

- Prime Video'yu özel ve yerel bir Microsoft Edge penceresinde açar (`--remote-debugging-address=127.0.0.1`).
- Hız ve altyazı butonunu (`1.2x ●` / `1.2x ⚡`) sadece video oynatımı hazır olduğunda gösterir.
- **5 Katmanlı Sıfır Görünürlük Reklam Kalkanı (`🛡️ Reklam Kalkanı`):**
  - **Katman 1 (CDP Fetch İstek Kesicisi - uBlock Origin Tarzı):** Chromium'un `Fetch.enable` ve `Fetch.requestPaused` protokol etki alanlarını kullanarak, reklam dosyalarının byte'ları daha yüklenmeye başlamadan istek aşamasında bloke eder. Oynatıcı düzeyinde gömülü reklamlar için boş VAST/VPAID XML yanıtı döner.
  - **Katman 2 (Ağ Reklam ve Takipçi Engelleyici):** Amazon reklam sunucularını (`amazon-adsystem.com`), telemetri ve takip ağlarını doğrudan Chromium ağ katmanında engeller (`Network.setBlockedURLs`).
  - **Katman 3 (CSS Banner ve Geri Sayım Yok Edici):** "Reklam 1/2" uyarılarını, reklam sayacı banner'larını ve katmanlarını tamamen görünmez yapar (`opacity: 0 !important`).
  - **Katman 4 (Opak Reklam Perdesi ve Otomatik Sessize Alma):** Atlanamayan zorunlu gömülü reklam aralarında ses otomatik kesilir (`video.muted = true`) ve reklam akışı opak bir perde ile gizlenir. (`canvas` tabanlı kare dondurma bilinçli olarak kullanılmaz: Prime Video akışı DRM korumalıdır ve kare yakalamak her tarayıcıda `SecurityError` fırlatır.) Kalkan yalnızca gerçek bir reklam geri sayımı (örn. `0:27`) görünürken devreye girer; 45 saniyelik emniyet valfi ve sonrasındaki bekleme süresi, takılı veya hatalı bir algılamanın normal izlemeyi asla perde arkasına kilitleyememesini garanti eder.
  - **Katman 5 (Otomatik Skip ve 16x Hiper Hız):** "Reklamı Atla / Skip Ad" butonu çıktığı milisaniye otomatik tıklanır. Atlanamayan reklamlarda ise video `16x` hiper hıza (`video.playbackRate = 16`) alınarak birkaç saniyede aşılır ve asıl içerik normal hızda (`1.2x`) pürüzsüzce geri gelir.
- **Akıllı Otomatik Gizleme (Auto-Hide):** Video oynatımı başladıktan tam 2 saniye sonra veya fare hareketsiz kaldığında, buton yumuşak bir animasyonla ekrandan kaybolur (`opacity: 0`) ve tertemiz sinematik bir ekran sunar. Fare hareketinde anında görünür hale gelir.
- **Kompakt Durum İkonu:** Mevcut hızı ve aktif modu simgeyle gösterir:
  - **`1.2x ●`** altyazı renklendirmesi AÇIK olduğunda (nokta seçilen altyazı renginde parlar).
  - **`1.2x ⚡`** altyazı sabitleyici KAPALI olduğunda (sadece hız kontrolü aktif).
- **Özel Görev Çubuğu ve Pencere İkonu:** Win32 COM arayüzü (`SHGetPropertyStoreForWindow`) kullanılarak sadece adanmış `msedge.exe` pencerelerine `AppUserModelID` (`PrimeVideoSpeedController.App`) atanır ve görev çubuğunda kusursuz bir gruplama sağlanır.
- **Çok Katmanlı Altyazı Sabitleyici:** `MutationObserver` ve `!important` CSS kuralları ile Prime Video'nun bölüm geçişlerinde altyazı rengini sıfırlamasını engeller.
- **İki Bölümlü Cam Efektli Menü:** `⚡ Hız Kontrolü` ve `💬 Altyazı Rengi` sekmelerini net şekilde ayırır.
- Yaygın hız önayarlarını (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) ve hassas adım butonlarını (`+` / `-` ile `0.25x` - `4.0x`) içerir.
- 5 hazır altyazı rengi sunar: **Sarı (`#FFCC00`)**, **Altın (`#FFD700`)**, **Beyaz (`#FFFFFF`)**, **Yeşil (`#00FF66`)** ve **Mavi (`#00FFFF`)**.
- Buton sürüklenerek ekranda istenilen yere taşınabilir.
- Tercihleri (hız, konum, renk, açık/kapalı durumu) yerel olarak `localStorage` üzerinde saklar.
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
5. Ekranda beliren `1.2x ●` butonuna tıklayarak hızınızı veya altyazı renginizi seçin. Reklamsız keyfin tadını çıkarın!

### Kontroller ve Kısayollar

- Menüyü açmak veya kapatmak için yüzen butona tıklayın.
- Butonu ekranda taşımak için farenin sol tuşuna basılı tutarak sürükleyin.
- Hız önayarlarına tıklayın veya `+` / `-` ile `0.1x` hassas adımlarla (`0.25x` ile `4.0x` arasında) hızı değiştirin.
- Altyazı anahtarına veya renk şeritlerine (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`) tıklayın.
- Altyazı modunu anında açıp kapatmak için `Alt + C` veya `Shift + C` tuşlarına basın.
- Hızı `0.1x` artırmak için `]` tuşuna basın.
- Hızı `0.1x` azaltmak için `[` tuşuna basın.
- Hızı `1x` (varsayılan) düzeyine sıfırlamak için `\` tuşuna basın.
- Menüyü kapatmak için `Escape` tuşuna basın.

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
- seçilen altyazı rengi ve aktiflik durumu

Hiçbir hesap bilgisi, çerez, token veya kişisel veri toplanmaz ve dışarı aktarılmaz. Katı Hex regex denetimi (`/^#[0-9A-Fa-f]{6}$/`) olası tüm XSS saldırılarını sıfır hata ile engeller.

### Lisans

MIT Lisansı altında yayınlanmıştır. Ayrıntılar için [LICENSE](LICENSE) dosyasına bakınız.

---

<a id="espanol"></a>
## Español

### Resumen

Prime Video Speed & Subtitle Controller es una herramienta ligera y de código abierto para Windows que abre Prime Video en una ventana dedicada de Microsoft Edge y añade un botón flotante cuando se detecta el reproductor de vídeo. Este control no molesta, se puede arrastrar a cualquier parte de la pantalla y recuerda localmente tu velocidad de reproducción seleccionada y tus colores de subtítulos preferidos.

Además de la estabilización multicapa de los subtítulos (por defecto en **Amarillo `#FFCC00`**), este proyecto incluye un **Escudo de Anuncios de 5 Capas (Zero-Visibility Ad Shield)** que bloquea servidores de publicidad a nivel de red (`Network.setBlockedURLs`) y silencia las pausas publicitarias obligatorias avanzando a velocidad ultrarrápida `16x` tras una cortina negra (`Zero-Visibility`). La personalización normal de la velocidad de reproducción para el usuario va fluidamente desde `0.25x` hasta `4.0x`.

### Funciones

- Abre Prime Video en una ventana dedicada de Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Muestra el control flotante (`1.2x ●` / `1.2x ⚡`) únicamente durante la reproducción de vídeo.
- **Escudo de Anuncios de 5 Capas (`🛡️ Reklam Kalkanı`):**
  - **Capa 1 (Intercepción CDP Fetch - estilo uBlock Origin):** Usa los dominios de protocolo `Fetch.enable` y `Fetch.requestPaused` de Chromium para bloquear los anuncios en la fase de solicitud, antes de que se cargue un solo byte. Devuelve respuestas XML VAST/VPAID vacías para los anuncios integrados a nivel de reproductor.
  - **Capa 2 (Bloqueo de Red y Rastreadores):** Bloquea servidores de publicidad de Amazon (`amazon-adsystem.com`), telemetría y rastreadores en la capa de red de Chromium (`Network.setBlockedURLs`).
  - **Capa 3 (Destrucción de Banners y Contadores CSS):** Oculta permanentemente avisos de "Anuncio 1 de 2", banners temporizadores y superposiciones molestas (`display: none !important`).
  - **Capa 4 (Cortina Negra y Silencio Automático):** Durante los anuncios obligatorios SSAI, silencia el audio (`video.muted = true`) y cubre el vídeo con una cortina oscura `⚡ Reklam Atlanıyor...` (`opacity: 0`). Nunca ves ni escuchas anuncios.
  - **Capa 5 (Salto Automático y Velocidad 16x):** Hace clic automáticamente en "Omitir anuncio" en el milisegundo en que aparece, o acelera los anuncios obligatorios a velocidad `16x` (`video.playbackRate = 16`) para consumirlos en segundos antes de restaurar tu velocidad normal (`1.2x`).
- **Ocultación Automática Inteligente:** Exactamente 2 segundos después de iniciar la reproducción (`play`/`playing`) o de que el ratón deje de moverse, el botón flotante se desvanece suavemente (`opacity: 0`) para dejar la pantalla completamente limpia. Vuelve a aparecer de inmediato si mueves el ratón.
- **Indicador Compacto de Estado:** Muestra tu velocidad actual junto con un icono distintivo:
  - **`1.2x ●`** cuando el color personalizado de subtítulos está ACTIVADO (el punto brilla con el color elegido).
  - **`1.2x ⚡`** cuando el estabilizador de subtítulos está DESACTIVADO (sólo control de velocidad).
- **Icono Personalizado en Barra de Tareas:** Utiliza la API Win32 COM (`SHGetPropertyStoreForWindow`) para asignar `AppUserModelID` (`PrimeVideoSpeedController.App`) en exclusiva a las ventanas dedicadas de `msedge.exe`.
- **Estabilizador de Subtítulos Multicapa:** Evita que Prime Video restablezca el color de los subtítulos entre episodios utilizando `MutationObserver` y CSS dinámico (`!important`).
- **Menú Glassmorphism de Dos Secciones:** Separa claramente `⚡ Hız Kontrolü` (Velocidad) y `💬 Altyazı Rengi` (Color de Subtítulos).
- Incluye ajustes preestablecidos de velocidad (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) y ajustes finos (`+` / `-` de `0.25x` a `4.0x`).
- Incluye 5 colores preestablecidos para subtítulos: **Amarillo (`#FFCC00`)**, **Dorado (`#FFD700`)**, **Blanco (`#FFFFFF`)**, **Verde (`#00FF66`)** y **Cian (`#00FFFF`)**.
- Permite arrastrar libremente el botón flotante en la pantalla.
- Recuerda localmente la velocidad, la posición del botón y el color de subtítulos elegido en `localStorage`.
- Vuelve a aplicar la configuración automáticamente si Prime Video reinicia el reproductor.

### Qué No Hace

- No modifica la aplicación de escritorio oficial de Prime Video.
- No elude la protección DRM, ni elimina restricciones, ni descarga vídeos.
- No lee, almacena ni transmite contraseñas, cookies, tokens, historial de visualización ni datos privados.
- No envía datos de telemetría a servidores de terceros.

### Requisitos

- Windows 10 o Windows 11
- Microsoft Edge
- .NET 8 Runtime o .NET 8 SDK

### Inicio Rápido

1. Descarga o clona este repositorio.
2. Ejecuta `run.cmd`.
3. Inicia sesión en Prime Video dentro de la ventana dedicada que se abre.
4. Reproduce cualquier película o serie.
5. Haz clic en el botón flotante (`1.2x ●`) para ajustar la velocidad o elegir el color del subtítulo. ¡Disfruta sin publicidad!

### Controles y Atajos

- Haz clic en el botón flotante para abrir o cerrar el menú.
- Arrastra el botón flotante manteniendo presionado el clic izquierdo para moverlo.
- Haz clic en cualquier velocidad preestablecida o usa `+` / `-` en pasos de `0.1x` (de `0.25x` a `4.0x`).
- Haz clic en el interruptor de subtítulos o en cualquier muestra de color (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Pulsa `Alt + C` o `Shift + C` para alternar la estabilización de subtítulos.
- Pulsa `]` para aumentar la velocidad en `0.1x`.
- Pulsa `[` para reducir la velocidad en `0.1x`.
- Pulsa `\` para restablecer la velocidad a `1x`.
- Pulsa `Escape` para cerrar el menú.

### Compilar desde el Código y Versiones de Archivo Único

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Generación automática de ejecutables de archivo único (Arquitectura Híbrida)
Gracias a nuestra **Arquitectura de Prioridad Híbrida**, los archivos `speed-control.js` y `AppIcon.ico` se leen externamente si existen (para permitir ediciones en tiempo real), O bien se cargan como recursos incrustados (`<EmbeddedResource>`) si se ejecuta como un archivo único.

Para generar ejecutables `.exe` para GitHub Releases, ejecute nuestro constructor automatizado:
```powershell
.\publish.cmd
```
Este comando compila automáticamente dos formatos en el directorio `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Versión ligera dependiente del marco (requiere .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Versión independiente (Self-Contained). ¡Incluye todo el tiempo de ejecución de .NET 8 y funciona al instante en cualquier PC con Windows de 64 bits sin instalaciones previas!

### Privacidad y Seguridad

La aplicación inicia Edge con un perfil local completamente dedicado y bloquea la depuración remota exclusivamente a `127.0.0.1:9223`. Utiliza este punto de conexión local únicamente para inyectar `speed-control.js` y aplicar `Network.setBlockedURLs` en las páginas de Prime Video.

El script inyectado sólo almacena preferencias locales en el `localStorage`:
- velocidad de reproducción seleccionada
- coordenadas del botón flotante
- color de subtítulos seleccionado y estado

No se recopilan ni transmiten credenciales, cookies, tokens o historiales de visualización. Un control estricto de expresiones regulares Hex (`/^#[0-9A-Fa-f]{6}$/`) previene por completo cualquier vulnerabilidad XSS.

### Licencia

Publicado bajo la Licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

---

<a id="deutsch"></a>
## Deutsch

### Überblick

Prime Video Speed & Subtitle Controller ist ein leichtgewichtiges Open-Source-Hilfsprogramm für Windows, das Prime Video in einem dedizierten Microsoft-Edge-Appfenster startet und einen eleganten, verschiebbaren Button einblendet, sobald ein Videoplayer erkannt wird. Die Steuerung bleibt unaufdringlich, lässt sich frei auf dem Bildschirm platzieren und speichert Ihre bevorzugte Wiedergabegeschwindigkeit sowie Untertitelfarb-Einstellungen lokal ab.

Zusätzlich zur mehrstufigen Untertitel-Stabilisierung (Standardmäßig **Gelb `#FFCC00`**) bietet dieses Projekt einen **5-stufigen Werbeschutz (Zero-Visibility Ad Shield)**, der Werbe-Tracker auf Netzwerkebene (`Network.setBlockedURLs`) blockiert und nicht überspringbare Werbeunterbrechungen mit `16x` Hyper-Geschwindigkeit und automatischer Stummschaltung hinter einem schwarzen Vorhang (`Zero-Visibility`) verschwinden lässt. Die reguläre Wiedergabegeschwindigkeit für den Benutzer lässt sich flexibel zwischen `0.25x` und `4.0x` anpassen.

### Funktionen

- Öffnet Prime Video in einem dedizierten Microsoft-Edge-Fenster (`--remote-debugging-address=127.0.0.1`).
- Zeigt den Geschwindigkeits- & Untertitelbutton (`1.2x ●` / `1.2x ⚡`) nur während der Videowiedergabe an.
- **5-stufiger Werbeschutz (`🛡️ Reklam Kalkanı`):**
  - **Stufe 1 (CDP-Fetch-Abfangen – uBlock-Origin-Stil):** Nutzt die Chromium-Protokolldomänen `Fetch.enable` und `Fetch.requestPaused`, um Werbung bereits in der Anfragephase zu blockieren, bevor ein einziges Byte geladen wird. Liefert leere VAST/VPAID-XML-Antworten für eingebettete Player-Werbung.
  - **Stufe 2 (Netzwerk- & Tracker-Blocker):** Blockiert Amazon-Werbeserver (`amazon-adsystem.com`), Telemetrie und Tracking-Netzwerke direkt auf der Chromium-Netzwerkebene (`Network.setBlockedURLs`).
  - **Stufe 3 (CSS-Banner & Countdown-Zerstörer):** Entfernt „Werbung 1 von 2“-Hinweise, Countdown-Banner und Werbe-Overlays dauerhaft (`display: none !important`).
  - **Stufe 4 (Blackout Vorhang & Auto-Stummschaltung):** Schaltet den Ton während unüberspringbarer SSAI-Werbeunterbrechungen sofort stumm (`video.muted = true`) und verbirgt das Bild hinter einem dunklen `⚡ Reklam Atlanıyor...` Vorhang (`opacity: 0`). Sie sehen oder hören keinerlei Werbung.
  - **Stufe 5 (Auto-Skip Klicker & 16x Hyper-Geschwindigkeit):** Klickt in der ersten Millisekunde automatisch auf „Werbung überspringen“ oder beschleunigt unüberspringbare Werbung mit `16x` Geschwindigkeit (`video.playbackRate = 16`), um sie in Sekunden zu beenden, bevor die normale Wiedergabe (`1.2x`) reibungslos fortgesetzt wird.
- **Intelligentes Auto-Hide während der Wiedergabe:** Genau 2 Sekunden nach Start des Videos (`play`/`playing`) oder bei Stillstand des Mauszeigers blendet sich der schwebende Button sanft aus (`opacity: 0`), um eine makellose Bildfläche zu garantieren. Erscheint bei Mausbewegung sofort wieder.
- **Kompakte Statusanzeige:** Zeigt Ihre aktuelle Geschwindigkeit und ein klares Modus-Symbol:
  - **`1.2x ●`** wenn die Untertitelfarb-Anpassung AKTIV ist (der Punkt leuchtet in der gewählten Farbe).
  - **`1.2x ⚡`** wenn die Untertitel-Stabilisierung INAKTIV ist (nur Geschwindigkeitskontrolle).
- **Benutzerdefiniertes Taskleisten- & Fenster-Icon:** Nutzt Win32 COM (`SHGetPropertyStoreForWindow`), um die `AppUserModelID` (`PrimeVideoSpeedController.App`) exklusiv den dedizierten `msedge.exe`-Fenstern zuzuweisen für eine saubere Taskleisten-Gruppierung.
- **Mehrstufiger Untertitel-Stabilisator:** Verhindert das Zurücksetzen von Untertitelfarben zwischen Episoden mithilfe von `MutationObserver` und dynamischem CSS (`!important`).
- **Zweigeteiltes Glassmorphism-Menü:** Trennt übersichtlich `⚡ Hız Kontrolü` (Geschwindigkeit) und `💬 Altyazı Rengi` (Untertitelfarbe).
- Enthält gängige Geschwindigkeits-Voreinstellungen (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) sowie Feinabstimmung (`+` / `-` von `0.25x` bis `4.0x`).
- Bietet 5 voreingestellte Untertitelfarben: **Gelb (`#FFCC00`)**, **Gold (`#FFD700`)**, **Weiß (`#FFFFFF`)**, **Grün (`#00FF66`)** und **Cyan (`#00FFFF`)**.
- Der Button lässt sich per Drag & Drop frei auf dem Bildschirm positionieren.
- Speichert alle Einstellungen (Geschwindigkeit, Position, Farbe, Status) lokal in `localStorage`.
- Wendet Einstellungen sofort neu an, falls Prime Video das Videoelement oder Untertitel zurücksetzt.

### Was dieses Tool nicht tut

- Es verändert die offizielle Prime Video Desktop-Anwendung in keiner Weise.
- Es umgeht keinen DRM-Schutz, entfernt keine Beschränkungen und lädt keine Videos herunter.
- Es liest, speichert oder überträgt niemals Passwörter, Cookies, Token, Wiedergabeverläufe oder private Kontodaten.
- Es sendet keinerlei Telemetriedaten an Dritte.

### Systemanforderungen

- Windows 10 oder Windows 11
- Microsoft Edge
- .NET 8 Runtime oder .NET 8 SDK

### Schnellstart

1. Laden Sie dieses Repository herunter oder klonen Sie es.
2. Führen Sie die Datei `run.cmd` aus.
3. Melden Sie sich im sich öffnenden dedizierten Edge-Fenster bei Prime Video an.
4. Starten Sie einen Film oder eine Episode.
5. Klicken Sie auf den schwebenden Button (`1.2x ●`), um Ihre Geschwindigkeit oder Untertitelfarbe festzulegen. Genießen Sie werbefreies Streaming!

### Steuerung & Tastenkürzel

- Klicken Sie auf den schwebenden Button, um das Menü zu öffnen oder zu schließen.
- Halten Sie die linke Maustaste gedrückt, um den Button auf dem Bildschirm zu verschieben.
- Klicken Sie auf Voreinstellungen oder nutzen Sie `+` / `-` für `0.1x`-Schritte (`0.25x` bis `4.0x`).
- Klicken Sie auf den Untertitel-Schalter oder Farbfelder (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Drücken Sie `Alt + C` oder `Shift + C`, um die Untertitel-Stabilisierung sofort ein-/auszuschalten.
- Drücken Sie `]`, um die Geschwindigkeit um `0.1x` zu erhöhen.
- Drücken Sie `[`, um die Geschwindigkeit um `0.1x` zu verringern.
- Drücken Sie `\`, um die Geschwindigkeit auf `1x` (Standard) zurückzusetzen.
- Drücken Sie `Escape`, um das Menü zu schließen.

### Aus dem Quellcode erstellen & Single-File-Releases

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Automatische Erstellung von Single-File-Ausführungsdateien (Hybride Architektur)
Dank unserer **Hybriden Prioritätsarchitektur** werden `speed-control.js` und `AppIcon.ico` bei Vorhandensein extern geladen (für sofortige Hot-Reloads durch Entwickler) ODER als `<EmbeddedResource>` direkt aus der C#-Anwendung abgerufen.

Um eigenständige `.exe`-Dateien für GitHub Releases zu generieren, starten Sie das automatisierte Skript:
```powershell
.\publish.cmd
```
Dies generiert zwei veröffentlichungsfertige Versionen im Ordner `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Ultraleichte Framework-abhängige Single-File-EXE (erfordert .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Unabhängige Self-Contained Single-File-EXE. Läuft sofort auf jedem 64-Bit-Windows-PC, ohne dass .NET installiert sein muss!

### Datenschutz und Sicherheit

Die App startet Edge mit einem dedizierten lokalen Profil und schränkt das Remote-Debugging strikt auf `127.0.0.1:9223` ein. Dieser lokale Endpunkt wird ausschließlich genutzt, um `speed-control.js` einzuspeisen und `Network.setBlockedURLs` auf die von der App geöffneten Prime-Video-Seiten anzuwenden.

Das eingespeiste Skript speichert ausschließlich lokale Präferenzen in `localStorage`:
- ausgewählte Wiedergabegeschwindigkeit
- Koordinaten des schwebenden Buttons
- ausgewählte Untertitelfarbe und Status

Es werden absolut keine Anmeldedaten, Cookies, Token oder Wiedergabeverläufe gesammelt oder übertragen. Eine strenge Hex-Regex-Prüfung (`/^#[0-9A-Fa-f]{6}$/`) verhindert jede Art von XSS-Schwachstellen.

### Lizenz

Veröffentlicht unter der MIT-Lizenz. Weitere Informationen finden Sie unter [LICENSE](LICENSE).

---

<a id="francais"></a>
## Français

### Présentation

Prime Video Speed & Subtitle Controller est un utilitaire léger et open-source pour Windows qui lance Prime Video dans une fenêtre Microsoft Edge dédiée et affiche un bouton flottant élégant lorsqu'un lecteur vidéo est détecté. Ce contrôle reste discret, peut être glissé à n'importe quel endroit de l'écran et mémorise localement votre vitesse de lecture ainsi que vos couleurs de sous-titres personnalisées.

En plus d'une stabilisation multicouche des sous-titres (par défaut en **Jaune `#FFCC00`**), ce projet intègre un **Bouclier Anti-Pub à 5 Niveaux (Zero-Visibility Ad Shield)** qui bloque les serveurs publicitaires sur le réseau (`Network.setBlockedURLs`) et coupe le son des publicités obligatoires en les accélérant à vitesse `16x` derrière un rideau noir (`Zero-Visibility`). La personnalisation normale de la vitesse de lecture par l'utilisateur s'étend de `0.25x` à `4.0x`.

### Fonctionnalités

- Ouvre Prime Video dans une fenêtre Microsoft Edge dédiée (`--remote-debugging-address=127.0.0.1`).
- Affiche le bouton de contrôle (`1.2x ●` / `1.2x ⚡`) uniquement lorsque la vidéo est en cours de lecture.
- **Bouclier Anti-Pub à 5 Niveaux (`🛡️ Reklam Kalkanı`):**
  - **Niveau 1 (Interception CDP Fetch - style uBlock Origin):** Utilise les domaines de protocole `Fetch.enable` et `Fetch.requestPaused` de Chromium pour bloquer les publicités dès la phase de requête, avant le chargement du moindre octet. Renvoie des réponses XML VAST/VPAID vides pour les publicités intégrées au lecteur.
  - **Niveau 2 (Blocage Réseau et Traqueurs):** Bloque les serveurs publicitaires d'Amazon (`amazon-adsystem.com`), la télémétrie et les traqueurs directement au niveau réseau Chromium (`Network.setBlockedURLs`).
  - **Niveau 3 (Suppression des Bannières et Comptes à Rebours):** Masque définitivement les indications "Annonce 1 sur 2", les bannières de compte à rebours et les superpositions CSS (`display: none !important`).
  - **Niveau 4 (Rideau Noir et Silencieux Automatique):** Pendant les publicités obligatoires SSAI, coupe instantanément le son (`video.muted = true`) et masque la vidéo derrière un rideau noir `⚡ Reklam Atlanıyor...` (`opacity: 0`). Vous ne voyez ni n'entendez aucune publicité.
  - **Niveau 5 (Clic de Saut et Vitesse 16x):** Clique automatiquement sur "Passer l'annonce" dès sa première milliseconde, ou accélère les publicités obligatoires à vitesse `16x` (`video.playbackRate = 16`) pour les terminer en quelques secondes avant de restaurer votre vitesse normale (`1.2x`).
- **Masquage Automatique Intelligent (Auto-Hide):** Exactement 2 secondes après le début de la vidéo (`play`/`playing`) ou l'arrêt du mouvement de la souris, le bouton flottant disparaît en douceur (`opacity: 0`) pour un écran de visionnage pur et sans encombre. Réapparaît instantanément au mouvement de la souris.
- **Indicateur Compact d'État:** Affiche la vitesse actuelle et un icône de mode propre:
  - **`1.2x ●`** lorsque la personnalisation des sous-titres est ACTIVE (le point brille dans votre couleur).
  - **`1.2x ⚡`** lorsque la stabilisation des sous-titres est INACTIVE (contrôle de vitesse uniquement).
- **Icône de Barre des Tâches et de Fenêtre Personnalisée:** Utilise l'API Win32 COM (`SHGetPropertyStoreForWindow`) pour attribuer `AppUserModelID` (`PrimeVideoSpeedController.App`) exclusivement aux fenêtres de `msedge.exe`.
- **Stabilisateur de Sous-titres Multicouche:** Empêche Prime Video de réinitialiser la couleur des sous-titres entre les épisodes grâce à `MutationObserver` et du CSS dynamique (`!important`).
- **Menu Glassmorphism à Deux Sections:** Sépare proprement `⚡ Hız Kontrolü` (Vitesse) et `💬 Altyazı Rengi` (Couleur des Sous-titres).
- Comprend des préréglages de vitesse courants (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) et un réglage précis (`+` / `-` de `0.25x` à `4.0x`).
- Propose 5 couleurs de sous-titres préréglées: **Jaune (`#FFCC00`)**, **Or (`#FFD700`)**, **Blanc (`#FFFFFF`)**, **Vert (`#00FF66`)** et **Cyan (`#00FFFF`)**.
- Permet de déplacer librement le bouton flottant par glisser-déposer sur l'écran.
- Mémorise localement la vitesse, la position du bouton et la couleur dans `localStorage`.
- Réapplique automatiquement les paramètres si Prime Video réinitialise le lecteur.

### Ce qu'il ne fait pas

- Il ne modifie pas l'application de bureau officielle Prime Video.
- Il ne contourne pas les protections DRM, ne supprime pas les restrictions et ne télécharge pas de vidéos.
- Il ne lit, ne stocke et ne transmet jamais vos mots de passe, cookies, tokens, historique ou données de compte.
- Il n'envoie aucune télémétrie à des serveurs tiers.

### Prérequis

- Windows 10 ou Windows 11
- Microsoft Edge
- .NET 8 Runtime ou .NET 8 SDK

### Démarrage Rapide

1. Téléchargez ou clonez ce dépôt.
2. Exécutez le fichier `run.cmd`.
3. Connectez-vous à Prime Video dans la fenêtre dédiée qui s'ouvre.
4. Lancez un film ou une série.
5. Cliquez sur le bouton flottant (`1.2x ●`) pour régler la vitesse ou la couleur des sous-titres. Profitez d'un streaming sans publicité !

### Contrôles et Raccourcis

- Cliquez sur le bouton flottant pour ouvrir ou fermer le menu.
- Maintenez le clic gauche enfoncé sur le bouton pour le déplacer sur l'écran.
- Cliquez sur un préréglage de vitesse ou utilisez `+` / `-` par paliers de `0.1x` (de `0.25x` à `4.0x`).
- Cliquez sur l'interrupteur des sous-titres ou sur une couleur (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Appuyez sur `Alt + C` ou `Shift + C` pour activer/désactiver instantanément la couleur des sous-titres.
- Appuyez sur `]` pour augmenter la vitesse de `0.1x`.
- Appuyez sur `[` pour diminuer la vitesse de `0.1x`.
- Appuyez sur `\` pour réinitialiser la vitesse à `1x` (par défaut).
- Appuyez sur `Escape` pour fermer le menu.

### Compiler à partir des sources et versions à fichier unique

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Génération automatique d'exécutables à fichier unique (Architecture Hybride)
Grâce à notre **Architecture à Priorité Hybride**, `speed-control.js` et `AppIcon.ico` sont lus en priorité depuis le dossier externe s'ils sont présents, OU chargés depuis les ressources intégrées (`<EmbeddedResource>`) lors d'une exécution autonome.

Pour générer des exécutables pour GitHub Releases, lancez le script :
```powershell
.\publish.cmd
```
Ce script crée deux formats prêts pour la production dans le dossier `publish/` :
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)** : Version ultra-légère dépendante du framework (nécessite .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)** : Version autonome (Self-Contained). Intègre le runtime .NET 8 complet et s'exécute instantanément sur n'importe quel PC Windows 64 bits sans aucune installation !

### Confidentialité et Sécurité

L'application démarre Edge avec un profil local entièrement dédié et restreint strictement le débogage distant à `127.0.0.1:9223`. Elle utilise cet endpoint local uniquement pour injecter `speed-control.js` et appliquer `Network.setBlockedURLs` aux pages Prime Video.

Le script injecté stocke uniquement des préférences locales dans le `localStorage`:
- vitesse de lecture sélectionnée
- coordonnées d'affichage du bouton
- couleur des sous-titres sélectionnée et état

Aucun identifiant, cookie, token ou historique n'est collecté ou transmis. Une vérification stricte par expression régulière Hex (`/^#[0-9A-Fa-f]{6}$/`) empêche toute faille XSS.

### Licence

Publié sous licence MIT. Consultez le fichier [LICENSE](LICENSE) pour plus d'informations.

---

<a id="portugues"></a>
## Português

### Visão Geral

Prime Video Speed & Subtitle Controller é uma ferramenta leve e de código aberto para Windows que abre o Prime Video em uma janela dedicada do Microsoft Edge e exibe um botão flutuante elegante assim que o reprodutor de vídeo é detectado. Esse controle não atrapalha, pode ser arrastado livremente pela tela e armazena localmente suas preferências de velocidade de reprodução e cores de legendas personalizadas.

Além da estabilização multicamada das legendas (padrão **Amarelo `#FFCC00`**), este projeto possui um **Escudo de 5 Camadas de Zero Visibilidade (Zero-Visibility Ad Shield)**, que bloqueia servidores de anúncios na rede (`Network.setBlockedURLs`) e silencia intervalos comerciais obrigatórios, acelerando-os em velocidade `16x` atrás de uma cortina preta (`Zero-Visibility`). A personalização normal da velocidade para o usuário vai de `0.25x` a `4.0x`.

### Recursos

- Abre o Prime Video em uma janela dedicada do Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Exibe o controle flutuante (`1.2x ●` / `1.2x ⚡`) exclusivamente durante a reprodução de vídeo.
- **Escudo de 5 Camadas de Zero Visibilidade (`🛡️ Reklam Kalkanı`):**
  - **Camada 1 (Interceptação CDP Fetch - estilo uBlock Origin):** Usa os domínios de protocolo `Fetch.enable` e `Fetch.requestPaused` do Chromium para bloquear anúncios na fase de requisição, antes de qualquer byte ser carregado. Retorna respostas XML VAST/VPAID vazias para anúncios embutidos no player.
  - **Camada 2 (Bloqueador de Rede e Rastreadores):** Bloqueia servidores de anúncios da Amazon (`amazon-adsystem.com`), telemetria e rastreamento diretamente na camada de rede Chromium (`Network.setBlockedURLs`).
  - **Camada 3 (Destruidor de Banners e Contadores CSS):** Oculta permanentemente avisos de "Anúncio 1 de 2", banners contadores e sobreposições indesejadas (`display: none !important`).
  - **Camada 4 (Cortina Preta e Silenciamento Automático):** Durante anúncios obrigatórios SSAI, silencia o áudio instantaneamente (`video.muted = true`) e cobre o vídeo com uma cortina escura `⚡ Reklam Atlanıyor...` (`opacity: 0`). Você não vê nem ouve comerciais.
  - **Camada 5 (Pular Automático e Velocidade 16x):** Clica no botão "Pular Anúncio" no milissegundo em que aparece ou acelera anúncios obrigatórios a velocidade `16x` (`video.playbackRate = 16`) para finalizá-los em segundos, restaurando sua velocidade normal (`1.2x`).
- **Ocultação Automática Inteligente:** Exatamente 2 segundos após o início do vídeo (`play`/`playing`) ou quando o mouse para de se mover, o botão flutuante desaparece suavemente (`opacity: 0`) para uma tela limpa e cinematográfica. Reaparece na hora ao mover o mouse.
- **Indicador Compacto de Status:** Mostra a velocidade atual junto com um ícone nítido:
  - **`1.2x ●`** quando a personalização de legenda está ATIVA (o ponto brilha na cor escolhida).
  - **`1.2x ⚡`** quando o estabilizador de legenda está DESATIVADO (apenas controle de velocidade).
- **Ícone Personalizado na Barra de Tarefas:** Usa a API Win32 COM (`SHGetPropertyStoreForWindow`) para definir a `AppUserModelID` (`PrimeVideoSpeedController.App`) exclusivamente nas janelas de `msedge.exe`.
- **Estabilizador de Legendas Multicamada:** Impede que o Prime Video redefina a cor das legendas entre os episódios via `MutationObserver` e CSS dinâmico (`!important`).
- **Menu Glassmorphism em Duas Seções:** Separa de forma limpa `⚡ Hız Kontrolü` (Velocidade) e `💬 Altyazı Rengi` (Cor da Legenda).
- Inclui predefinições de velocidade (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) e ajustes finos (`+` / `-` de `0.25x` a `4.0x`).
- Oferece 5 cores predefinidas: **Amarelo (`#FFCC00`)**, **Dourado (`#FFD700`)**, **Branco (`#FFFFFF`)**, **Verde (`#00FF66`)** e **Ciano (`#00FFFF`)**.
- Permite arrastar o botão flutuante livremente na tela.
- Lembra localmente a velocidade, posição do botão e cor da legenda no `localStorage`.
- Reaplica as configurações automaticamente se o Prime Video reiniciar o reprodutor de vídeo.

### O Que Não Faz

- Não altera o aplicativo oficial de desktop do Prime Video.
- Não burla o DRM, não remove restrições de direitos autorais e não baixa vídeos.
- Não lê, armazena ou transmite senhas, cookies, tokens, histórico de exibição ou dados de conta.
- Não envia telemetria para servidores de terceiros.

### Requisitos

- Windows 10 ou Windows 11
- Microsoft Edge
- .NET 8 Runtime ou .NET 8 SDK

### Início Rápido

1. Baixe ou clone este repositório.
2. Execute o arquivo `run.cmd`.
3. Faça login no Prime Video na janela dedicada do Edge que se abrirá.
4. Inicie um filme ou episódio.
5. Clique no botão flutuante (`1.2x ●`) para ajustar a velocidade ou a cor da legenda. Aproveite sem anúncios!

### Controles e Atalhos

- Clique no botão flutuante para abrir ou fechar o menu.
- Mantenha o clique esquerdo pressionado no botão para arrastá-lo pela tela.
- Clique em predefinições ou use `+` / `-` para passos de `0.1x` (de `0.25x` a `4.0x`).
- Clique no interruptor da legenda ou nas opções de cor (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Pressione `Alt + C` ou `Shift + C` para ativar/desativar a estilização da legenda na hora.
- Pressione `]` para aumentar a velocidade em `0.1x`.
- Pressione `[` para diminuir a velocidade em `0.1x`.
- Pressione `\` para redefinir a velocidade para `1x` (padrão).
- Pressione `Escape` para fechar o menu.

### Compilar a Partir do Código e Versões de Arquivo Único

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Geração automática de executáveis de arquivo único (Arquitetura Híbrida)
Graças à nossa **Arquitetura de Prioridade Híbrida**, `speed-control.js` e `AppIcon.ico` são lidos externamente quando presentes, OU carregados a partir dos recursos embutidos (`<EmbeddedResource>`) se executados em arquivo único.

Para gerar os arquivos `.exe` para o GitHub Releases, execute:
```powershell
.\publish.cmd
```
Isso compila automaticamente dois formatos no diretório `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Versão ultraleve dependente de framework (requer .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Versão independente (Self-Contained). Inclui o runtime .NET 8 completo e funciona instantaneamente em qualquer PC Windows 64 bits sem instalações prévias!

### Privacidade e Segurança

O aplicativo inicia o Edge com um perfil local isolado e restringe a depuração remota estritamente a `127.0.0.1:9223`. Esse endpoint local é utilizado somente para injetar `speed-control.js` e aplicar `Network.setBlockedURLs` nas páginas do Prime Video.

O script injetado salva apenas preferências locais no `localStorage`:
- velocidade de reprodução selecionada
- coordenadas da tela do botão flutuante
- cor de legenda selecionada e status

Nenhuma credencial, cookie, token ou histórico de exibição é coletado ou transmitido. A verificação rigorosa por regex Hexadecimal (`/^#[0-9A-Fa-f]{6}$/`) evita completamente qualquer vulnerabilidade de XSS.

### Licença

Distribuído sob a Licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---

<a id="zh"></a>
## 中文

### 概览

Prime Video Speed & Subtitle Controller 是一个开源、轻量级的 Windows 辅助工具。它在 Microsoft Edge 的独立应用窗口中打开 Prime Video，并在检测到视频播放器时添加一个优雅的悬浮控制按钮 (`1.2x ●` / `1.2x ⚡`)。该控制按钮界面精简，可自由拖拽放置在屏幕任何位置，并能本地保存您的倍速选择与自定义字幕颜色偏好。

除了多层字幕颜色固定功能（默认锁定为**黄色 `#FFCC00`**）外，本项目还内置了**5层零可见广告屏蔽盾 (Zero-Visibility Ad Shield)**。它不仅能在底层网络拦截广告跟踪器 (`Network.setBlockedURLs`)，更能在不可跳过的广告片断播放时自动开启黑屏静音，并以 `16x` 极速快进消除广告 (`Zero-Visibility`)。用户的常规视频播放倍速调整范围为 `0.25x` 至 `4.0x`。

### 功能特性

- 在专用的 Microsoft Edge 独立窗口 (`--remote-debugging-address=127.0.0.1`) 中打开 Prime Video。
- 仅在视频播放器准备就绪时显示悬浮按钮 (`1.2x ●` / `1.2x ⚡`)。
- **5层零可见广告屏蔽盾 (`🛡️ Reklam Kalkanı`):**
  - **第 1 层 (CDP Fetch 请求拦截 - uBlock Origin 风格):** 利用 Chromium 的 `Fetch.enable` 与 `Fetch.requestPaused` 协议域，在请求阶段拦截广告，任何字节尚未加载即被阻断；并为播放器级拼接广告返回空的 VAST/VPAID XML 响应。
  - **第 2 层 (网络广告与跟踪拦截):** 在 Chromium 底层网络协议层拦截 Amazon 广告服务器 (`amazon-adsystem.com`)、遥测数据及追踪请求 (`Network.setBlockedURLs`)。
  - **第 3 层 (CSS 广告横幅消灭):** 永久隐藏“广告 1/2”倒计时提示、顶部广告横幅及干扰弹窗 (`display: none !important`)。
  - **第 4 层 (黑屏静音幕布):** 遇到不可跳过的强制广告 (SSAI) 时，瞬间将音频静音 (`video.muted = true`) 并将视频画面隐藏在漆黑的 `⚡ Reklam Atlanıyor...` 幕布之下 (`opacity: 0`)。您看不见也听不到任何商业广告。
  - **第 5 层 (秒跳与 16 倍速极速消化):** 检测到“跳过广告”按键出现的毫秒内自动点击。若遇到无法点击的广告，则将视频播放速度瞬间加速至 `16x` (`video.playbackRate = 16`)，在两秒内消化完毕，随后平滑恢复至您设定的正常倍速 (`1.2x`)。
- **播放时智能隐藏 (Auto-Hide):** 在视频开始播放 (`play`/`playing`) 或鼠标停止移动后的精确 2 秒后，悬浮按钮会以平滑动画自动隐藏 (`opacity: 0`)，带来媲美原生的纯净观影体验。鼠标移过时瞬间重新出现。
- **紧凑型状态指示器:** 清晰显示当前倍速与运作模式：
  - **`1.2x ●`**：当自定义字幕显色开启时（圆点会以您选择的字幕颜色发光）。
  - **`1.2x ⚡`**：当字幕颜色锁定关闭时（仅启用播放倍速控制）。
- **自定义任务栏与窗口图标:** 利用 Win32 COM 接口 (`SHGetPropertyStoreForWindow`) 仅对专用的 `msedge.exe` 窗口赋予 `AppUserModelID` (`PrimeVideoSpeedController.App`)，实现精准独立的任务栏图标合并。
- **多层级字幕稳定器:** 通过 `MutationObserver` 监听 DOM，配合动态 `!important` 样式，彻底阻止 Prime Video 在剧集切换时重置字幕颜色。
- **双区隔毛玻璃美学菜单:** 清晰划分 `⚡ Hız Kontrolü` (播放速度) 与 `💬 Altyazı Rengi` (字幕颜色)。
- 内置常用倍速预设 (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) 及精细步进按钮 (`+` / `-` 从 `0.25x` 调至 `4.0x`)。
- 提供 5 种预设高对比度字幕颜色：**黄色 (`#FFCC00`)**、**金色 (`#FFD700`)**、**纯白 (`#FFFFFF`)**、**荧光绿 (`#00FF66`)** 和 **青蓝色 (`#00FFFF`)**。
- 支持按住鼠标左键自由拖动悬浮按钮在画面上的坐标。
- 将所选倍速、按钮坐标、字幕颜色及开关状态本地持久化存储于 `localStorage`。
- 若网页重置了播放器 DOM，控制脚本能够瞬间感知并重新绑定倍速与字幕设置。

### 它不做什么

- 不会以任何方式修改官方 Prime Video 桌面客户端文件。
- 不会破解 DRM 版权保护、解除观看区域限制或抓取下载视频流量。
- 不会读取、记录、转储或发送任何形式的账户密码、Cookie、Token、历史记录。
- 不会向任何远端服务器发送使用统计或遥测数据。

### 系统要求

- Windows 10 或 Windows 11
- Microsoft Edge 浏览器
- .NET 8 Runtime 或 .NET 8 SDK

### 快速入门

1. 下载或克隆本代码仓库到本地。
2. 双击运行 `run.cmd`。
3. 在新打开的专用 Edge 窗口中登录您的 Prime Video 账户。
4. 点击播放任何一部影片或连续剧。
5. 点击画面上的 `1.2x ●` 悬浮按键即可调整倍速或切换字幕颜色。自动享受全程零广告极速播放！

### 控制与快捷键

- 点击悬浮按钮开启或收起主菜单。
- 按住悬浮按钮并拖拽鼠标即可在屏幕上改变其摆放坐标。
- 点击预设倍速，或使用 `+` / `-` 按钮以 `0.1x` 步进调节播放速度 (`0.25x` 至 `4.0x`)。
- 点击字幕开关拨钮，或选取色块 (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`) 以锁定字幕样式。
- 按下 `Alt + C` 或 `Shift + C` 快捷键瞬间开启/关闭字幕美化模式。
- 按下 `]` 快捷键将当前播放倍速增加 `0.1x`。
- 按下 `[` 快捷键将当前播放倍速减少 `0.1x`。
- 按下 `\` 快捷键将播放速度一键重置为默认值 `1x`。
- 按下 `Escape` 键关闭操作菜单。

### 从源码编译与单文件发布版

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### 单文件可执行程序(`.exe`)自动生成（混合优先架构）
借助我们的**混合优先架构**，当外部文件夹中存在 `speed-control.js` 和 `AppIcon.ico` 时会优先读取（支持开发者即时热修改），而在作为独立可执行文件运行且无外部资源时，将自动平滑加载 C# 内部嵌入的资源（`<EmbeddedResource>`）！

运行以下一键脚本即可为 GitHub Releases 生成两个独立的分发包：
```powershell
.\publish.cmd
```
这将在 `publish/` 目录下构建以下两个生产格式：
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**：超轻量级框架依赖版（目标电脑需提前安装 .NET 8 Desktop Runtime）。
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**：完全独立版（Self-Contained）。内置所有 .NET 8 运行时，无需安装任何环境即可在任意 64 位 Windows 电脑上双击运行！

### 隐私与安全

本工具通过独立隔离的本地用户配置文件夹启动 Microsoft Edge，并严格限制远程调试连接仅能够绑定本机回路 `127.0.0.1:9223`。该底层调试端口绝不外露，仅用于向该窗口注入 `speed-control.js` 交互脚本及执行 `Network.setBlockedURLs` 广告域名屏蔽拦截。

注入的控制脚本仅在本地浏览器应用层 (`localStorage`) 保存简单的配置信息：
- 您选定的播放倍速数字
- 悬浮按钮在画面的像素坐标
- 您选择的字幕颜色值及启用状态

全部运行过程保持完全透明与本地运作，不涉及任何凭据、会话 Cookie 或身份标识的收集。代码对于所有的颜色参数采用极严格的十六进制正则校验 (`/^#[0-9A-Fa-f]{6}$/`)，从架构层杜绝 XSS 注入风险。

### 许可证

基于 MIT 开源许可证发布。详情请见 [LICENSE](LICENSE) 文件。

---

<a id="hindi"></a>
## हिन्दी

### परिचय

Prime Video Speed & Subtitle Controller विंडोज़ के लिए एक ओपन-सोर्स और हल्का टूल है जो Prime Video को एक समर्पित Microsoft Edge ऐप विंडो में खोलता है और वीडियो प्लेबैक का पता चलने पर स्क्रीन पर एक आकर्षक, ड्रैग करने योग्य फ्लोटिंग बटन (`1.2x ●` / `1.2x ⚡`) दिखाता है। यह नियंत्रण बटन आपके देखने में कोई बाधा नहीं डालता, स्क्रीन पर कहीं भी ले जाया जा सकता है और आपके चुने हुए प्लेबैक स्पीड और सबटाइटल रंग को स्थानीय रूप से सुरक्षित रखता है।

सबटाइटल के पीले रंग (**Yellow `#FFCC00`**) को हर एपिसोड में लॉक करने के अलावा, इस प्रोजेक्ट में **5-Layer Zero-Visibility Ad Shield** शामिल है। यह नेटवर्क स्तर पर विज्ञापन ट्रैकर्स को रोकता है (`Network.setBlockedURLs`) और अनिवार्य विज्ञापनों को काली स्क्रीन (`Zero-Visibility`) के पीछे स्वतः म्यूट करके `16x` की तेज़ गति से सेकंडों में समाप्त कर देता है। सामान्य देखने की गति को आप `0.25x` से `4.0x` तक आसानी से बदल सकते हैं।

### सुविधाएँ

- Prime Video को समर्पित Microsoft Edge विंडो (`--remote-debugging-address=127.0.0.1`) में खोलता है।
- स्पीड और सबटाइटल बटन (`1.2x ●` / `1.2x ⚡`) को केवल वीडियो चलने पर दिखाता है।
- **5-Layer Zero-Visibility Ad Shield (`🛡️ Reklam Kalkanı`):**
  - **लेयर 1 (CDP Fetch इंटरसेप्शन - uBlock Origin शैली):** Chromium के `Fetch.enable` और `Fetch.requestPaused` प्रोटोकॉल डोमेन का उपयोग करके विज्ञापनों को अनुरोध चरण में ही रोक देता है, इससे पहले कि एक भी बाइट लोड हो। प्लेयर-स्तर के विज्ञापनों के लिए खाली VAST/VPAID XML उत्तर लौटाता है।
  - **लेयर 2 (नेटवर्क विज्ञापन और ट्रैकर ब्लॉकर):** Amazon के विज्ञापन सर्वर (`amazon-adsystem.com`), टेलीमेट्री और ट्रैकिंग नेटवर्क को सीधे Chromium नेटवर्क स्तर पर ब्लॉक करता है (`Network.setBlockedURLs`)।
  - **लेयर 3 (CSS बैनर और काउंटडाउन रिमूवर):** "Ad 1 of 2" संदेश, विज्ञापन समय बैनर और ओवरले को हमेशा के लिए छुपाता है (`display: none !important`)।
  - **लेयर 4 (काली स्क्रीन और ऑटो-म्यूट):** अनिवार्य SSAI विज्ञापनों के दौरान तुरंत आवाज़ बंद करता है (`video.muted = true`) और वीडियो को काली `⚡ Reklam Atlanıyor...` स्क्रीन (`opacity: 0`) के पीछे छुपा देता है। आप न तो विज्ञापन देखते हैं और न ही सुनते हैं।
  - **लेयर 5 (ऑटो-स्किप और 16x हाइपर-स्पीड):** "Skip Ad" बटन आते ही मिलीसेकंड में स्वतः क्लिक करता है। यदि विज्ञापन स्किप न हो सके, तो वीडियो को `16x` की तेज़ गति (`video.playbackRate = 16`) से चलाकर कुछ ही सेकंड में समाप्त कर देता है और फिर आपकी सामान्य गति (`1.2x`) पर वापस आ जाता है।
- **स्मार्ट ऑटो-हाइड:** वीडियो शुरू होने के 2 सेकंड बाद या माउस रुकने पर फ्लोटिंग बटन अपने आप गायब हो जाता है (`opacity: 0`) ताकि स्क्रीन बिल्कुल साफ रहे। माउस हिलाते ही यह तुरंत फिर से दिखाई देता है।
- **कॉम्पैक्ट स्टेटस इंडिकेटर:** आपकी वर्तमान गति और मोड दिखाता है:
  - **`1.2x ●`** जब सबटाइटल कलर ऑन होता है (बिंदु आपके चुने हुए रंग में चमकता है)।
  - **`1.2x ⚡`** जब सबटाइटल स्टेबलाइज़र ऑफ होता है (केवल स्पीड कंट्रोल चालू)।
- **कस्टम टास्कबार और विंडो आइकन:** Win32 COM (`SHGetPropertyStoreForWindow`) का उपयोग करके केवल समर्पित `msedge.exe` विंडो को `AppUserModelID` (`PrimeVideoSpeedController.App`) असाइन करता है।
- **मल्टी-लेयर सबटाइटल स्टेबलाइज़र:** `MutationObserver` और डायनामिक CSS (`!important`) द्वारा Prime Video को एपिसोड बदलने पर सबटाइटल रंग रीसेट करने से रोकता है।
- **दो-भाग वाला मेनू:** `⚡ Hız Kontrolü` (स्पीड कंट्रोल) और `💬 Altyazı Rengi` (सबटाइटल कलर) को साफ तौर पर अलग करता है।
- सामान्य स्पीड प्रीसेट (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) और बारीक समायोजन (`+` / `-` से `0.25x` से `4.0x` तक) शामिल हैं।
- 5 प्रीसेट सबटाइटल रंग प्रदान करता है: **पीला (`#FFCC00`)**, **सुनहरा (`#FFD700`)**, **सफ़ेद (`#FFFFFF`)**, **हरा (`#00FF66`)**, और **सियान (`#00FFFF`)**।
- बटन को स्क्रीन पर कहीं भी खींचकर (Drag) रखने की अनुमति देता है।
- आपकी स्पीड, बटन स्थिति, और रंग को स्थानीय `localStorage` में सुरक्षित रखता है।
- यदि Prime Video प्लेयर को रीफ़्रेश करता है, तो यह सेटिंग्स को स्वतः दोबारा लागू करता है।

### यह क्या नहीं करता है

- यह आधिकारिक Prime Video डेस्कटॉप ऐप में कोई बदलाव नहीं करता है।
- यह DRM सुरक्षा को नहीं तोड़ता, प्रतिबंध नहीं हटाता और वीडियो डाउनलोड नहीं करता है।
- यह आपके पासवर्ड, कुकीज़, टोकन, देखने के इतिहास या निजी डेटा को कभी नहीं पढ़ता, सहेजता या भेजता है।
- यह कोई भी टेलीमेट्री डेटा बाहर नहीं भेजता है।

### आवश्यकताएँ

- Windows 10 या Windows 11
- Microsoft Edge
- .NET 8 Runtime या .NET 8 SDK

### त्वरित शुरुआत

1. इस रिपॉजिटरी को डाउनलोड या क्लोन करें।
2. `run.cmd` फ़ाइल को चलाएँ।
3. खुलने वाली समर्पित Edge विंडो में अपने Prime Video खाते में लॉगिन करें।
4. कोई भी फ़िल्म या एपिसोड चलाना शुरू करें।
5. स्पीड और सबटाइटल रंग चुनने के लिए स्क्रीन पर मौजूद `1.2x ●` बटन पर क्लिक करें। बिना किसी विज्ञापन के आनंद लें!

### नियंत्रण और शॉर्टकट

- मेनू खोलने या बंद करने के लिए फ्लोटिंग बटन पर क्लिक करें।
- बटन को स्क्रीन पर कहीं भी ले जाने के लिए उस पर बायां क्लिक दबाकर खींचें।
- किसी भी स्पीड प्रीसेट पर क्लिक करें या `+` / `-` का उपयोग करके `0.1x` के चरणों में (`0.25x` से `4.0x` तक) गति बदलें।
- सबटाइटल टॉगल या किसी भी रंग पर (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`) क्लिक करें।
- सबटाइटल कलरिंग को तुरंत ऑन/ऑफ करने के लिए `Alt + C` या `Shift + C` दबाएँ।
- गति को `0.1x` बढ़ाने के लिए `]` दबाएँ।
- गति को `0.1x` घटाने के लिए `[` दबाएँ।
- गति को डिफ़ॉल्ट `1x` पर रीसेट करने के लिए `\` दबाएँ।
- मेनू बंद करने के लिए `Escape` दबाएँ।

### सोर्स से बिल्ड करें और सिंगल-फ़ाइल रिलीज़

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### स्वचालित सिंगल-फ़ाइल `.exe` निर्माण (हाइब्रिड आर्किटेक्चर)
हमारी **हाइब्रिड प्राथमिकता आर्किटेक्चर** के कारण, `speed-control.js` और `AppIcon.ico` बाहरी फ़ोल्डर में मौजूद होने पर पहले पढ़े जाते हैं (ताकि डेवलपर्स तुरंत बदलाव कर सकें), या सिंगल `.exe` के रूप में चलने पर C# में एम्बेडेड (`<EmbeddedResource>`) संसाधनों से लोड होते हैं!

GitHub Releases के लिए सिंगल-फ़ाइल निष्पादन योग्य फ़ाइलें बनाने के लिए हमारा स्वचालित स्क्रिप्ट चलाएँ:
```powershell
.\publish.cmd
```
यह `publish/` फ़ोल्डर में दो प्रारूप तैयार करता है:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: अल्ट्रा-लाइटवेट फ़्रेमवर्क-आश्रित संस्करण (.NET 8 Desktop Runtime आवश्यक)।
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: पूर्ण आत्मनिर्भर (Self-Contained) संस्करण। इसमें संपूर्ण .NET 8 रनटाइम शामिल है और यह किसी भी 64-बिट Windows PC पर बिना किसी इंस्टॉलेशन के तुरंत चलता है!

### गोपनीयता और सुरक्षा

यह ऐप Edge को एक समर्पित स्थानीय प्रोफ़ाइल के साथ शुरू करता है और रिमोट डिबगिंग को सख्ती से केवल `127.0.0.1:9223` तक सीमित करता है। इस स्थानीय पोर्ट का उपयोग केवल `speed-control.js` स्क्रिप्ट इंजेक्ट करने और Prime Video पृष्ठों पर `Network.setBlockedURLs` लागू करने के लिए किया जाता है।

इंजेक्ट की गई स्क्रिप्ट केवल स्थानीय `localStorage` में प्राथमिकताएँ सहेजती है:
- आपकी चुनी हुई प्लेबैक स्पीड
- फ्लोटिंग बटन के स्क्रीन निर्देशांक
- आपका चुना हुआ सबटाइटल रंग और स्थिति

कोई भी खाता क्रेडेंशियल, कुकीज़, टोकन या व्यक्तिगत डेटा एकत्र या बाहर प्रसारित नहीं किया जाता है। सख्त हेक्साडेसिमल रेगेक्स जांच (`/^#[0-9A-Fa-f]{6}$/`) किसी भी प्रकार के XSS हमले को पूरी तरह से रोकती है।

### लाइसेंस

MIT लाइसेंस के अंतर्गत जारी किया गया। अधिक जानकारी के लिए [LICENSE](LICENSE) देखें।

---

<a id="arabic"></a>
## العربية

### نظرة عامة

Prime Video Speed & Subtitle Controller هو أداة مفتوحة المصدر وخفيفة لنظام Windows تفتح Prime Video داخل نافذة Microsoft Edge مخصصة، وتضيف زر عائم أنيق (`1.2x ●` / `1.2x ⚡`) عند اكتشاف مشغل الفيديو. لا يعيق هذا التحكم مشاهدتك، ويمكن سحبه ووضعه في أي مكان على الشاشة، ويحفظ إعداداتك المفضلة لسرعة التشغيل وألوان الترجمة محلياً.

بالإضافة إلى تثبيت لون الترجمة بعدة طبقات (الافتراضي هو **الأصفر `#FFCC00`**)، يشتمل هذا المشروع على **درع إعلانات مخفي من 5 طبقات (Zero-Visibility Ad Shield)** يقوم بحجب خوادم الإعلانات على مستوى الشبكة (`Network.setBlockedURLs`) ويكتم صوت الإعلانات الإجبارية ويسرعها إلى `16x` خلف شاشة سوداء (`Zero-Visibility`). يمكن للمستخدم تخصيص سرعة المشاهدة العادية بسلاسة بين `0.25x` و `4.0x`.

### الميزات

- يفتح Prime Video في نافذة Microsoft Edge مخصصة ومحمية (`--remote-debugging-address=127.0.0.1`).
- يظهر زر التحكم (`1.2x ●` / `1.2x ⚡`) فقط أثناء تشغيل الفيديو.
- **درع إعلانات مخفي من 5 طبقات (`🛡️ Reklam Kalkanı`):**
  - **الطبقة 1 (اعتراض CDP Fetch - بأسلوب uBlock Origin):** يستخدم نطاقات بروتوكول Chromium `Fetch.enable` و`Fetch.requestPaused` لحجب الإعلانات في مرحلة الطلب قبل تحميل أي بايت، ويعيد استجابات XML فارغة بصيغة VAST/VPAID للإعلانات المدمجة في المشغل.
  - **الطبقة 2 (حجب خوادم الإعلانات والتتبع شبكياً):** يحجب خوادم إعلانات Amazon (`amazon-adsystem.com`) وشبكات القياس عن بعد مباشرة في طبقة شبكة Chromium (`Network.setBlockedURLs`).
  - **الطبقة 3 (إزالة لافتات الإعلانات والعد التنازلي):** يخفي نهائياً رسائل "Ad 1 of 2" ولافتات العد التنازلي للإعلانات (`display: none !important`).
  - **الطبقة 4 (الستارة السوداء والكتم التلقائي للصوت):** أثناء فواصل الإعلانات الإجبارية SSAI، يكتم الصوت تلقائياً (`video.muted = true`) ويغطي الفيديو بستارة سوداء `⚡ Reklam Atlanıyor...` (`opacity: 0`). لا ترى أو تسمع أي محتوى إعلاني.
  - **الطبقة 5 (النقر التلقائي لزر التخطي والتسريع 16x):** ينقر تلقائياً على زر "تخطي الإعلان" فور ظهوره. وإذا كان الإعلان غير قابل للتخطي، يسرع الفيديو إلى `16x` (`video.playbackRate = 16`) لإنهائه في ثوانٍ ثم يعيد السرعة الطبيعية (`1.2x`).
- **الإخفاء التلقائي الذكي أثناء المشاهدة:** بعد ثانيتين بالتمام من بدء الفيديو (`play`/`playing`) أو توقف حركة الماوس، يختفي الزر العائم بسلاسة (`opacity: 0`) للحصول على شاشة نظيفة وسينمائية. يعود للظهور فور تحريك الماوس.
- **مؤشر حالة مدمج:** يعرض السرعة الحالية وأيقونة الوضع:
  - **`1.2x ●`** عندما يكون تخصيص الترجمة مفعّلاً (تضيء النقطة بلون الترجمة المختار).
  - **`1.2x ⚡`** عندما يكون مثبت الترجمة غير مفعّل (التحكم بالسرعة فقط).
- **أيقونة مخصصة لشريط المهام والنافذة:** يستخدم واجهة Win32 COM (`SHGetPropertyStoreForWindow`) لتعيين `AppUserModelID` (`PrimeVideoSpeedController.App`) حصرياً لنوافذ `msedge.exe` المخصصة.
- **مثبت الترجمة متعدد الطبقات:** يمنع Prime Video من إعادة تعيين ألوان الترجمة عند الانتقال بين الحلقات باستخدام `MutationObserver` وأنماط CSS الإجبارية (`!important`).
- **قائمة Glassmorphism من قسمين:** تفصل بشكل أنيق بين `⚡ Hız Kontrolü` (السرعة) و `💬 Altyazı Rengi` (لون الترجمة).
- يتضمن إعدادات سرعة مسبقة (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) وأزرار ضبط دقيقة (`+` / `-` بين `0.25x` و `4.0x`).
- يوفر 5 ألوان جاهزة للترجمة: **الأصفر (`#FFCC00`)**، **الذهبي (`#FFD700`)**، **الأبيض (`#FFFFFF`)**، **الأخضر (`#00FF66`)**، و **الأزرق (`#00FFFF`)**.
- يسمح بسحب الزر العائم ووضعه في أي مكان على الشاشة بحرية.
- يحفظ السرعة وموضع الزر ولون الترجمة محلياً في `localStorage`.
- يعيد تطبيق الإعدادات تلقائياً في حال تحديث المشغل أو الترجمة.

### ما لا يفعله البرنامج

- لا يقوم بتعديل تطبيق سطح المكتب الرسمي لـ Prime Video.
- لا يتجاوز حماية DRM أو يزيل القيود أو يقوم بتنزيل الفيديوهات.
- لا يقرأ أو يخزن أو يرسل كلمات المرور أو ملفات تعريف الارتباط أو الرموز أو سجل المشاهدة أو البيانات الشخصية.
- لا يرسل أي بيانات قياس عن بعد إلى خوادم خارجية.

### متطلبات النظام

- Windows 10 أو Windows 11
- متصفح Microsoft Edge
- .NET 8 Runtime أو .NET 8 SDK

### البدء السريع

1. قم بتنزيل أو نسخ هذا المستودع.
2. شغل ملف `run.cmd`.
3. سجل الدخول إلى حسابك في Prime Video داخل نافذة Edge المخصصة التي تفتح.
4. ابدأ تشغيل أي فيلم أو حلقة.
5. انقر على الزر العائم (`1.2x ●`) لضبط السرعة أو اختيار لون الترجمة. استمتع بمشاهدة خالية تماماً من الإعلانات!

### أدوات التحكم والاختصارات

- انقر على الزر العائم لفتح أو إغلاق القائمة.
- اضغط مع الاستمرار بالنقر الأيسر على الزر لسحبه وتحريكه على الشاشة.
- انقر على أي سرعة مسبقة أو استخدم `+` / `-` لتغيير السرعة بخطوات `0.1x` (من `0.25x` إلى `4.0x`).
- انقر على مفتاح تبديل الترجمة أو على أي عينة لون (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- اضغط على `Alt + C` أو `Shift + C` لتبديل تفعيل ألوان الترجمة فوراً.
- اضغط على `]` لزيادة السرعة بمقدار `0.1x`.
- اضغط على `[` لتقليل السرعة بمقدار `0.1x`.
- اضغط على `\` لإعادة تعيين السرعة إلى `1x` (الافتراضي).
- اضغط على `Escape` لإغلاق القائمة.

### البناء من المصدر وإصدارات الملف الواحد (.exe)

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### إنشاء ملفات تشغيل وحيدة ومستقلة (البنية الهجينة الذكية)
بفضل **البنية الهجينة ذات الأولوية** لدينا، يتم قراءة ملفات `speed-control.js` و `AppIcon.ico` خارجيًا عند وجودها (للسماح بالتعديل المباشر للمطورين)، أو يتم تحميلها تلقائيًا من الموارد المدمجة (`<EmbeddedResource>`) داخل التطبيق عند تشغيله كملف مستقل `.exe` وبدون أي ملفات إضافية!

لإنشاء ملفات التشغيل المخصصة لـ GitHub Releases، قم بتشغيل السكربت الآلي:
```powershell
.\publish.cmd
```
سيقوم هذا السكربت بتجهيز نسختين في مجلد `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: نسختنا الخفيفة للغاية المعتمدة على إطار العمل (تتطلب وجود .NET 8 Desktop Runtime في حاسوب المستخدم).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: النسخة المستقلة بالكامل (Self-Contained). تتضمن بيئة تشغيل .NET 8 كاملة وتعمل فورًا على أي حاسوب يعمل بنظام Windows 64-bit دون الحاجة لأي تثبيت أو متطلبات مسبقة!

### الخصوصية والأمان

يقوم التطبيق بتشغيل Edge مع ملف تعريف محلي مخصص بالكامل ويقيد التصحيح عن بعد حصرياً على المنفذ `127.0.0.1:9223`. يستخدم هذا المنفذ المحلي فقط لحقن نص `speed-control.js` وتطبيق `Network.setBlockedURLs` على صفحات Prime Video.

يحفظ النص المحقون فقط التفضيلات المحلية في `localStorage`:
- سرعة التشغيل المحددة
- إحداثيات موقع الزر العائم
- لون الترجمة المختار وحالة التفعيل

لا يتم جمع أو نقل أي بيانات اعتماد أو ملفات تعريف ارتباط أو سجلات مشاهدة أو معلومات شخصية. تمنع عمليات التحقق الصارمة بتعبيرات Hex العادية (`/^#[0-9A-Fa-f]{6}$/`) أي ثغرات حقن برمجيات خبيثة (XSS).

### الترخيص

نُشر بموجب ترخيص MIT. راجع ملف [LICENSE](LICENSE) لمزيد من التفاصيل.

---

<a id="russian"></a>
## Русский

### Обзор

Prime Video Speed & Subtitle Controller — это открытая легкая утилита для Windows, которая запускает Prime Video в отдельном приложении-окне Microsoft Edge и добавляет стильную плавающую кнопку управления (`1.2x ●` / `1.2x ⚡`), когда обнаруживается видеоплеер. Кнопка не перекрывает контент, легко перетаскивается в любое удобное место экрана и сохраняет ваши предпочтения скорости воспроизведения и цвета субтитров локально.

В дополнение к многослойному стабилизатору субтитров (по умолчанию **Желтый `#FFCC00`**), проект включает **5-уровневый рекламный щит нулевой видимости (Zero-Visibility Ad Shield)**. Он блокирует рекламные трекеры на сетевом уровне (`Network.setBlockedURLs`), а при показе обязательной рекламы автоматически отключает звук и ускоряет ее в `16x` раз за черным экраном (`Zero-Visibility`). Пользовательская скорость просмотра видео плавно настраивается от `0.25x` до `4.0x`.

### Возможности

- Открывает Prime Video в отдельном окне Microsoft Edge (`--remote-debugging-address=127.0.0.1`).
- Показывает кнопку управления (`1.2x ●` / `1.2x ⚡`) исключительно во время воспроизведения видео.
- **5-уровневый рекламный щит нулевой видимости (`🛡️ Reklam Kalkanı`):**
  - **Уровень 1 (Перехват CDP Fetch — в стиле uBlock Origin):** Использует протокольные домены Chromium `Fetch.enable` и `Fetch.requestPaused`, чтобы блокировать рекламу на стадии запроса, до загрузки первого байта. Возвращает пустые XML-ответы VAST/VPAID для встроенной рекламы на уровне плеера.
  - **Уровень 2 (Сетевая блокировка рекламы и трекеров):** Блокирует рекламные серверы Amazon (`amazon-adsystem.com`), телеметрию и трекинговые домены прямо на сетевом уровне Chromium (`Network.setBlockedURLs`).
  - **Уровень 3 (Удаление баннеров и счетчиков):** Полностью скрывает надписи "Реклама 1 из 2", таймеры и рекламные оверлеи (`display: none !important`).
  - **Уровень 4 (Черный экран и авто-мьют):** Во время неотключаемых вставок SSAI мгновенно выключает звук (`video.muted = true`) и закрывает видео темным экраном `⚡ Reklam Atlanıyor...` (`opacity: 0`). Вы не видите и не слышите рекламу.
  - **Уровень 5 (Авто-клик Skip и скорость 16x):** Автоматически нажимает "Пропустить рекламу" в первую миллисекунду появления. Если реклама не пропускается, ускоряет видео до `16x` (`video.playbackRate = 16`), завершая вставку за пару секунд перед возвратом к обычной скорости (`1.2x`).
- **Умное автоскрытие при просмотре:** Ровно через 2 секунды после начала видео (`play`/`playing`) или остановки мыши плавающая кнопка плавно исчезает (`opacity: 0`) для чистого кинематографического обзора. Мгновенно появляется при движении мыши.
- **Компактный индикатор статуса:** Отображает текущую скорость и иконку режима:
  - **`1.2x ●`**, если цвет субтитров ВКЛЮЧЕН (точка светится выбранным вами цветом).
  - **`1.2x ⚡`**, если стабилизатор субтитров ВЫКЛЮЧЕН (только контроль скорости).
- **Собственная иконка окна и панели задач:** Использует Win32 COM API (`SHGetPropertyStoreForWindow`) для назначения `AppUserModelID` (`PrimeVideoSpeedController.App`) исключительно окнам `msedge.exe`, обеспечивая идеальную группировку в панели задач.
- **Многоуровневый стабилизатор субтитров:** Предотвращает сброс цвета субтитров между сериями с помощью `MutationObserver` и принудительных стилей CSS (`!important`).
- **Двухсекционное меню Glassmorphism:** Четко разделяет `⚡ Hız Kontrolü` (Скорость) и `💬 Altyazı Rengi` (Цвет субтитров).
- Содержит пресеты скорости (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) и кнопки точной настройки (`+` / `-` от `0.25x` до `4.0x`).
- Предлагает 5 готовых контрастных цветов: **Желтый (`#FFCC00`)**, **Золотой (`#FFD700`)**, **Белый (`#FFFFFF`)**, **Зеленый (`#00FF66`)** и **Голубой (`#00FFFF`)**.
- Позволяет свободно перетаскивать плавающую кнопку левой кнопкой мыши.
- Сохраняет скорость, координаты кнопки, цвет и статус локально в `localStorage`.
- Автоматически повторно применяет настройки, если Prime Video обновляет плеер.

### Чего утилита НЕ делает

- Не изменяет официальное десктопное приложение Prime Video.
- Не обходит DRM-защиту, не снимает региональные ограничения и не скачивает видео.
- Не читает, не сохраняет и не передает пароли, файлы cookie, токены, историю просмотров или данные аккаунта.
- Не отправляет данные телеметрии на сторонние серверы.

### Системные требования

- Windows 10 или Windows 11
- Браузер Microsoft Edge
- .NET 8 Runtime или .NET 8 SDK

### Быстрый старт

1. Скачайте или клонируйте этот репозиторий.
2. Запустите файл `run.cmd`.
3. Войдите в свой аккаунт Prime Video в открывшемся отдельном окне Edge.
4. Включите любой фильм или сериал.
5. Нажмите на плавающую кнопку (`1.2x ●`), чтобы настроить скорость или цвет субтитров. Наслаждайтесь просмотром без рекламы!

### Управление и горячие клавиши

- Нажмите на плавающую кнопку, чтобы открыть или закрыть меню.
- Удерживайте левую кнопку мыши на плавающей кнопке, чтобы перетащить ее по экрану.
- Выберите пресет скорости или используйте `+` / `-` для шага `0.1x` (от `0.25x` до `4.0x`).
- Нажмите на переключатель субтитров или на любой цвет (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Нажмите `Alt + C` или `Shift + C` для мгновенного включения/выключения цвета субтитров.
- Нажмите `]`, чтобы увеличить скорость на `0.1x`.
- Нажмите `[`, чтобы уменьшить скорость на `0.1x`.
- Нажмите `\`, чтобы сбросить скорость до `1x` (по умолчанию).
- Нажмите `Escape`, чтобы закрыть меню.

### Сборка из исходного кода и создание единого исполняемого файла

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Автоматическая генерация единых исполняемых файлов (.exe) — Гибридная архитектура
Благодаря нашей **гибридной архитектуре приоритетов**, файлы `speed-control.js` и `AppIcon.ico` в первую очередь считываются из внешней папки (позволяя разработчикам редактировать код на лету). Если внешние файлы отсутствуют (например, при загрузке единого `.exe`), приложение автоматически использует встроенные в сборку ресурсы (`<EmbeddedResource>`).

Чтобы создать релизные файлы для GitHub Releases, запустите скрипт:
```powershell
.\publish.cmd
```
Этот скрипт скомпилирует в папку `publish/` два готовых формата:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Ультралегкая версия, зависящая от фреймворка (требует установленного .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Полностью автономная версия (Self-Contained). Включает всю среду выполнения .NET 8 и мгновенно запускается на любом 64-битном ПК с Windows без установки дополнительных компонентов!

### Конфиденциальность и безопасность

Приложение запускает Edge с изолированным локальным профилем и жестко ограничивает удаленную отладку только портом `127.0.0.1:9223`. Этот локальный интерфейс используется исключительно для внедрения скрипта `speed-control.js` и применения сетевых блокировок `Network.setBlockedURLs` на страницах Prime Video.

Внедренный скрипт сохраняет в `localStorage` только локальные настройки:
- выбранную скорость воспроизведения
- экранные координаты плавающей кнопки
- выбранный цвет субтитров и статус включения

Никакие учетные данные, файлы cookie, токены или история просмотров не собираются и не передаются. Строгая проверка формата Hex (`/^#[0-9A-Fa-f]{6}$/`) исключает любые уязвимости XSS.

### Лицензия

Распространяется под лицензией MIT. Подробности см. в файле [LICENSE](LICENSE).

---

<a id="japanese"></a>
## 日本語

### 概要

Prime Video Speed & Subtitle Controller は、Windows 向けのオープンソースで軽量なアシスタントツールです。Prime Video を Microsoft Edge の専用アプリウィンドウで起動し、動画プレイヤーが検出された際に、洗練されたドラッグ可能な操作ボタン (`1.2x ●` / `1.2x ⚡`) を画面に表示します。このボタンは視聴の妨げにならず、画面内の好きな場所に移動でき、選択した再生速度や字幕カラーの設定をローカルに記憶します。

字幕を黄色 (**Yellow `#FFCC00`**) に保つ多層安定化機能に加えて、当プロジェクトには**5レイヤー広告遮断シールド (Zero-Visibility Ad Shield)**が搭載されています。ネットワーク層での広告サーバーブロック (`Network.setBlockedURLs`) に加え、スキップ不可の広告枠では自動消音とブラックアウト画面の裏で `16倍速` 高速消化 (`Zero-Visibility`) を実行します。通常の動画再生におけるユーザーの速度調整範囲は `0.25x` から `4.0x` です。

### 機能

- 専用の Microsoft Edge アプリウィンドウ (`--remote-debugging-address=127.0.0.1`) で Prime Video を開きます。
- 再生速度および字幕ボタン (`1.2x ●` / `1.2x ⚡`) を動画再生中のみ表示します。
- **5レイヤー広告遮断シールド (`🛡️ Reklam Kalkanı`):**
  - **レイヤー 1 (CDP Fetch インターセプト - uBlock Origin 方式):** Chromium の `Fetch.enable` / `Fetch.requestPaused` プロトコルドメインを使い、1バイトも読み込まれる前のリクエスト段階で広告をブロックします。プレイヤー統合型広告には空の VAST/VPAID XML を返します。
  - **レイヤー 2 (ネットワーク広告＆トラッカー遮断):** Chromium ネットワーク層にて Amazon 広告サーバー (`amazon-adsystem.com`)、テレメトリー、追跡ドメインを直接ブロックします (`Network.setBlockedURLs`)。
  - **レイヤー 3 (CSS バナー＆カウントダウン削除):** 「広告 1/2」表示、カウントダウンバナー、画面オーバーレイを完全に非表示化します (`display: none !important`)。
  - **レイヤー 4 (ブラックアウトカーテン＆自動ミュート):** スキップ不可の強制広告 (SSAI) 時に音声を即座に消音 (`video.muted = true`) し、映像を暗転した `⚡ Reklam Atlanıyor...` カーテン (`opacity: 0`) で覆い隠します。広告の音声も映像も一切流れません。
  - **レイヤー 5 (自動スキップ＆16倍速超高速消化):** 「広告をスキップ」ボタンが出現した瞬間に自動クリック。スキップ不可広告の場合は再生速度を `16倍速` (`video.playbackRate = 16`) に引き上げ数秒で終了させ、本来の動画速度 (`1.2x`) へスムーズに復帰します。
- **再生中のスマート自動非表示:** 動画再生開始 (`play`/`playing`) またはマウス停止のちょうど2秒後にボタンがスムーズにフェードアウト (`opacity: 0`) し、公式プレイヤーと調和した美しい画面を提供します。マウスを動かすと即座に再表示されます。
- **コンパクトステータスインジケーター:** 現在の速度とモードアイコンを表示します：
  - **`1.2x ●`**：字幕カラー変更が有効な場合（点が選択カラーで発光します）。
  - **`1.2x ⚡`**：字幕固定が無効な場合（再生速度コントロールのみ）。
- **カスタムタスクバー＆ウィンドウアイコン:** Win32 COM (`SHGetPropertyStoreForWindow`) を利用し、専用の `msedge.exe` ウィンドウに対して独自 `AppUserModelID` (`PrimeVideoSpeedController.App`) を割り当て、タスクバー上で綺麗に独立結合します。
- **多層字幕スタビライザー:** `MutationObserver` と動的 CSS (`!important`) により、エピソード切り替え時に Prime Video が字幕色をリセットする現象を完全に防ぎます。
- **2セクション Glassmorphism メニュー:** `⚡ Hız Kontrolü` (再生速度) と `💬 Altyazı Rengi` (字幕カラー) を美しく分割します。
- 代表的な速度プリセット (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) や微調整 (`+` / `-` で `0.25x` から `4.0x` まで) を完備しています。
- 5種類の高コントラスト字幕カラーを提供：**イエロー (`#FFCC00`)**、**ゴールド (`#FFD700`)**、**ホワイト (`#FFFFFF`)**、**グリーン (`#00FF66`)**、**シアン (`#00FFFF`)**。
- 左クリックのドラッグ＆ドロップでフローティングボタンを画面上の任意の位置へ配置できます。
- 速度、ボタン座標、字幕カラー、有効状態をローカルの `localStorage` に保持します。
- Prime Video がプレイヤーや字幕要素を再構築しても自動で設定を再適用します。

### 行わないこと

- 公式の Prime Video デスクトップアプリを変更・改変することはありません。
- DRM 保護の回避、権利制限の解除、または動画のダウンロードは一切行いません。
- パスワード、Cookie、トークン、視聴履歴、個人データを収集、保存、送信することは一切ありません。
- 外部のサーバーへテレメトリーデータを送信することはありません。

### システム要件

- Windows 10 または Windows 11
- Microsoft Edge ブラウザ
- .NET 8 Runtime または .NET 8 SDK

### クイックスタート

1. 本リポジトリをダウンロードまたはクローンします。
2. `run.cmd` を実行します。
3. 開いた専用 Edge ウィンドウで Prime Video アカウントにサインインします。
4. お好きな映画やドラマを再生します。
5. 画面に表示されるフローティングボタン (`1.2x ●`) をクリックして速度や字幕カラーを設定します。広告ゼロの快適視聴をお楽しみください！

### 操作とショートカット

- フローティングボタンをクリックしてメニューを開閉します。
- フローティングボタンを左クリックしたままドラッグして画面上を移動します。
- 速度プリセットをクリックするか、`+` / `-` を使って `0.1x` 単位 (`0.25x` 〜 `4.0x`) で速度を調整します。
- 字幕スイッチまたは色チップ (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`) をクリックします。
- `Alt + C` または `Shift + C` を押して字幕カスタマイズを瞬時に切り替えます。
- `]` を押して速度を `0.1x` 上げます。
- `[` を押して速度を `0.1x` 下げます。
- `\` を押して速度を標準 (`1x`) にリセットします。
- `Escape` を押してメニューを閉じます。

### ソースからのビルドと単一ファイルリリース

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### 単一ファイル実行可能(`.exe`)の自動生成（ハイブリッド優先アーキテクチャ）
**ハイブリッド優先アーキテクチャ**により、`speed-control.js`と`AppIcon.ico`は外部ファイルが存在する場合は優先して読み込まれ（開発者の即時カスタマイズが可能）、単一の`.exe`として実行される場合はC#アセンブリに埋め込まれたリソース（`<EmbeddedResource>`）から自動的に読み込まれます。

GitHub Releases用の単一ファイル実行可能ファイルを生成するには、以下の自動化スクリプトを実行します：
```powershell
.\publish.cmd
```
これにより、`publish/`ディレクトリに2つの本番用フォーマットが生成されます：
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**：超軽量フレームワーク依存版（実行環境に .NET 8 Desktop Runtime が必要）。
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**：完全自己完結型（Self-Contained）版。.NET 8ランタイムを内蔵しており、事前のインストールなしで64ビットWindows上で即座に動作します！

### プライバシーとセキュリティ

当アプリは完全隔離されたローカルプロファイルで Edge を起動し、リモートデバッグを `127.0.0.1:9223` のみに厳格制限します。このローカルエンドポイントは `speed-control.js` の注入と `Network.setBlockedURLs` による広告遮断適用以外には一切使用されません。

注入されるスクリプトが `localStorage` に保存するのはローカル設定のみです：
- 選択された再生速度
- フローティングボタンの画面座標
- 選択された字幕カラーおよび有効状態

アカウント情報、Cookie、トークン、視聴履歴などの個人情報は一切収集または送信されません。厳格な16進数正規表現 (`/^#[0-9A-Fa-f]{6}$/`) 検証により、あらゆる XSS 脆弱性を根絶しています。

### ライセンス

MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) ファイルをご確認ください。

---

<a id="indonesian"></a>
## Bahasa Indonesia

### Ringkasan

Prime Video Speed & Subtitle Controller adalah alat bantu open-source yang ringan untuk Windows, yang membuka Prime Video di jendela aplikasi Microsoft Edge khusus serta menampilkan tombol kontrol elegan dan dapat digeser (`1.2x ●` / `1.2x ⚡`) saat pemutar video terdeteksi. Kontrol ini tidak mengganggu tampilan, dapat dipindahkan ke posisi mana pun di layar, dan mengingat preferensi kecepatan pemutaran serta warna subtitle Anda secara lokal.

Selain penstabil warna subtitle multilapis (default warna **Kuning `#FFCC00`**), proyek ini dilengkapi **Perisai Iklan 5 Lapis Tanpa Tampilan (Zero-Visibility Ad Shield)**. Perisai ini memblokir pelacak iklan di tingkat jaringan (`Network.setBlockedURLs`) dan membisukan iklan wajib yang tidak dapat dilewati sembari mempercepatnya hingga `16x` di balik layar hitam (`Zero-Visibility`). Pengaturan kecepatan pemutaran normal untuk pengguna berkisar dari `0.25x` hingga `4.0x`.

### Fitur

- Membuka Prime Video di jendela aplikasi Microsoft Edge khusus (`--remote-debugging-address=127.0.0.1`).
- Menampilkan tombol kecepatan & subtitle (`1.2x ●` / `1.2x ⚡`) hanya ketika video sedang diputar.
- **Perisai Iklan 5 Lapis Tanpa Tampilan (`🛡️ Reklam Kalkanı`):**
  - **Lapis 1 (Intersepsi CDP Fetch - gaya uBlock Origin):** Menggunakan domain protokol `Fetch.enable` dan `Fetch.requestPaused` Chromium untuk memblokir iklan pada tahap permintaan, sebelum satu byte pun dimuat. Mengembalikan respons XML VAST/VPAID kosong untuk iklan yang tertanam di pemutar.
  - **Lapis 2 (Pemblokir Jaringan Iklan & Pelacak):** Memblokir server iklan Amazon (`amazon-adsystem.com`), telemetri, dan pelacak langsung pada lapisan jaringan Chromium (`Network.setBlockedURLs`).
  - **Lapis 3 (Penghancur Spanduk & Penghitung Mundur CSS):** Menyembunyikan pesan "Ad 1 of 2", spanduk hitung mundur iklan, dan lapisan penutup secara permanen (`display: none !important`).
  - **Lapis 4 (Tirai Hitam & Bisu Otomatis):** Selama jeda iklan wajib SSAI, suara langsung dibisukan (`video.muted = true`) dan video ditutup oleh tirai gelap `⚡ Reklam Atlanıyor...` (`opacity: 0`). Anda tidak melihat atau mendengar komersial apa pun.
  - **Lapis 5 (Klik Lewati Otomatis & Kecepatan 16x):** Mengklik tombol "Lewati Iklan" secara otomatis pada milidetik pertama munculnya. Jika iklan tidak dapat dilewati, video dipercepat ke kecepatan `16x` (`video.playbackRate = 16`) untuk menyelesaikannya dalam hitungan detik sebelum kembali ke kecepatan normal (`1.2x`).
- **Sembunyi Otomatis (Auto-Hide) Saat Pemutaran:** Tepat 2 detik setelah pemutaran video dimulai (`play`/`playing`) atau mouse berhenti bergerak, tombol fluktuatif menghilang perlahan (`opacity: 0`) untuk pengalaman menonton yang bersih. Muncul kembali seketika saat mouse digerakkan.
- **Indikator Status Kompak:** Menampilkan kecepatan saat ini serta ikon mode:
  - **`1.2x ●`** saat kustomisasi subtitle AKTIF (titik bersinar dengan warna pilihan Anda).
  - **`1.2x ⚡`** saat penstabil subtitle NONAKTIF (hanya kontrol kecepatan pemutaran).
- **Ikon Taskbar & Jendela Kustom:** Menggunakan API Win32 COM (`SHGetPropertyStoreForWindow`) untuk menetapkan `AppUserModelID` (`PrimeVideoSpeedController.App`) khusus pada jendela `msedge.exe`, memastikan pengelompokan taskbar yang rapi dan terpisah.
- **Penstabil Subtitle Multilapis:** Mencegah Prime Video mereset warna subtitle saat berganti episode menggunakan `MutationObserver` dan CSS dinamis (`!important`).
- **Menu Glassmorphism Dua Bagian:** Memisahkan dengan jelas antara `⚡ Hız Kontrolü` (Kecepatan Pemutaran) dan `💬 Altyazı Rengi` (Warna Subtitle).
- Menyediakan prasetel kecepatan umum (`0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`) serta tombol penyesuaian presisi (`+` / `-` dari `0.25x` hingga `4.0x`).
- Menyediakan 5 warna subtitle kontras tinggi: **Kuning (`#FFCC00`)**, **Emas (`#FFD700`)**, **Putih (`#FFFFFF`)**, **Hijau (`#00FF66`)**, dan **Cyan (`#00FFFF`)**.
- Memungkinkan Anda menggeser dan menempatkan tombol fluktuatif di mana saja pada layar.
- Menyimpan kecepatan, posisi tombol, dan warna subtitle secara lokal pada `localStorage`.
- Menerapkan kembali pengaturan secara otomatis jika Prime Video memperbarui elemen pemutar video.

### Apa Yang Tidak Dilakukan

- Tidak memodifikasi aplikasi desktop resmi Prime Video sama sekali.
- Tidak menembus perlindungan DRM, tidak menghapus batasan hak cipta, dan tidak mengunduh video.
- Tidak membaca, menyimpan, atau mengirimkan kata sandi, cookie, token, riwayat tontonan, atau data akun pribadi.
- Tidak mengirimkan data telemetri ke server pihak ketiga mana pun.

### Persyaratan Sistem

- Windows 10 atau Windows 11
- Peramban Microsoft Edge
- .NET 8 Runtime atau .NET 8 SDK

### Mulai Cepat

1. Unduh atau klon repositori ini.
2. Jalankan berkas `run.cmd`.
3. Masuk ke akun Prime Video Anda pada jendela khusus Edge yang terbuka.
4. Mulai putar film atau episode drama apa pun.
5. Klik tombol fluktuatif (`1.2x ●`) di layar untuk mengatur kecepatan atau memilih warna subtitle. Nikmati tontonan bebas iklan!

### Kontrol & Pintasan

- Klik tombol fluktuatif untuk membuka atau menutup menu.
- Tahan klik kiri pada tombol fluktuatif sembari menggesernya untuk memindahkannya di layar.
- Klik prasetel kecepatan atau gunakan `+` / `-` untuk mengubah kecepatan dalam langkah `0.1x` (`0.25x` hingga `4.0x`).
- Klik sakelar subtitle atau pilihan warna (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Tekan `Alt + C` atau `Shift + C` untuk mengaktifkan/menonaktifkan pewarnaan subtitle secara instan.
- Tekan `]` untuk menambah kecepatan pemutaran sebesar `0.1x`.
- Tekan `[` untuk mengurangi kecepatan pemutaran sebesar `0.1x`.
- Tekan `\` untuk mengatur ulang kecepatan pemutaran ke `1x` (bawaan).
- Tekan `Escape` untuk menutup menu.

### Membangun dari Kode Sumber dan Rilis Satu Berkas (.exe)

```powershell
dotnet build -c Release
dotnet run -c Release
```

#### Pembuatan Otomatis Berkas Executable Tunggal (Arsitektur Hibrida)
Berkat **Arsitektur Prioritas Hibrida** kami, `speed-control.js` dan `AppIcon.ico` dibaca secara eksternal jika tersedia (memungkinkan modifikasi langsung oleh pengembang), ATAU dimuat secara otomatis dari sumber daya tersemat (`<EmbeddedResource>`) saat dijalankan sebagai satu berkas `.exe` mandiri.

Untuk membuat berkas rilis `.exe` untuk GitHub Releases, jalankan skrip otomatis kami:
```powershell
.\publish.cmd
```
Skrip ini mengompilasi dua format siap pakai ke dalam folder `publish/`:
- **`publish/Light/PrimeVideoSpeedApp.exe` (~213 KB)**: Versi sangat ringan yang bergantung pada kerangka kerja (memerlukan .NET 8 Desktop Runtime).
- **`publish/Standalone/PrimeVideoSpeedApp.exe` (~64 MB)**: Versi sepenuhnya mandiri (Self-Contained). Sudah menyertakan seluruh runtime .NET 8 dan dapat langsung dijalankan di PC Windows 64-bit mana pun tanpa instalasi tambahan!

### Privasi dan Keamanan

Aplikasi ini menjalankan Edge dengan profil lokal yang sepenuhnya terisolasi dan membatasi debugging jarak jauh secara ketat hanya pada port `127.0.0.1:9223`. Endpoint lokal ini hanya digunakan untuk menyuntikkan `speed-control.js` serta menerapkan pemblokiran domain iklan `Network.setBlockedURLs` pada halaman Prime Video.

Skrip yang disuntikkan hanya menyimpan preferensi lokal di `localStorage`:
- kecepatan pemutaran yang dipilih
- koordinat posisi tombol fluktuatif
- warna subtitle yang dipilih dan status aktifnya

Tidak ada kredensial, cookie, token, atau riwayat tontonan yang dikumpulkan atau dikirimkan. Validasi regex Heksadesimal yang ketat (`/^#[0-9A-Fa-f]{6}$/`) mencegah seluruh risiko kerentanan XSS.

### Lisensi

Diterbitkan di bawah Lisensi MIT. Silakan lihat berkas [LICENSE](LICENSE) untuk detail lebih lanjut.
