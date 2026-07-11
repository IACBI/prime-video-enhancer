# Prime Video Speed & Subtitle Controller

**Prime Video Speed & Subtitle Controller** is an open-source, lightweight Windows helper that opens Prime Video in a dedicated Microsoft Edge app window, adding a clean playback speed control (`1.2x ●` / `1.2x ⚡`), an automatic subtitle color stabilizer, and an always-on **4-Layer Zero-Visibility Ad Shield** (`🛡️ Reklam Kalkanı`).

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

In addition to multi-layer subtitle stabilization (defaulting to **Yellow `#FFCC00`**), this project features an **Always-On 4-Layer Zero-Visibility Ad Shield** that blocks ad trackers at the network level (`Network.setBlockedURLs`) and blackouts unskippable stitched ad segments with `16x` hyper-speed and automatic muting, ensuring you never see or hear commercials (`Zero-Visibility`).

### Features

- Opens Prime Video in a dedicated Microsoft Edge app window (`--remote-debugging-address=127.0.0.1`).
- Shows the speed & subtitle control (`1.2x ●` / `1.2x ⚡`) only when video playback is available.
- **Always-On 4-Layer Zero-Visibility Ad Shield (`🛡️ Reklam Kalkanı`):**
  - **Layer 1 (Network Ad & Tracker Blocker):** Blocks Amazon ad servers (`amazon-adsystem.com`), telemetry, and tracking networks right at the Chromium network layer (`Network.setBlockedURLs`).
  - **Layer 2 (CSS Banner & Countdown Destroyer):** Permanently removes "Ad 1 of 2", ad countdown banners, and ad overlays (`display: none !important`).
  - **Layer 3 (Blackout Curtain & Auto-Mute):** During unskippable SSAI ad breaks, instantly mutes the commercial audio (`video.muted = true`) and hides video frames behind a dark `⚡ Reklam Atlanıyor...` blackout curtain (`opacity: 0`). You never see or hear commercial content.
  - **Layer 4 (Auto-Skip Clicker & 16x Hyper-Speed):** Automatically clicks "Skip Ad" the millisecond it appears, or accelerates unskippable ads at `16x` speed to finish them in seconds before restoring normal playback (`1.2x`).
- **Smart Auto-Hide During Playback:** Exactly 2 seconds after video playback begins (`play`/`playing`) or the mouse stops moving, the floating button smoothly fades out (`opacity: 0`) for an ultra-clean viewing experience matching native Prime Video controls. Reappears instantly on mouse movement or pause.
- **Compact Icon Indicator:** Displays your current speed and a clean indicator icon:
  - **`1.2x ●`** when custom subtitle styling is ON (the dot glows in your selected subtitle color).
  - **`1.2x ⚡`** when subtitle override is OFF (speed control only).
- **Multi-layer Subtitle Stabilizer:** Prevents Prime Video from resetting subtitle styles across episodes using `MutationObserver` and dynamic CSS (`!important`).
- **Two-Section Glassmorphism Menu:** Cleanly separates `⚡ Hız Kontrolü` (Speed Control) and `💬 Altyazı Rengi` (Subtitle Color).
- Includes common speed presets: `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` and fine adjustments (`+` / `-`).
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
- Click any speed preset or use `+` / `-` for `0.1x` speed changes.
- Click the subtitle toggle switch or any color swatch (`Sarı`, `Altın`, `Beyaz`, `Yeşil`, `Mavi`).
- Press `Alt + C` or `Shift + C` to instantly toggle subtitle styling ON/OFF.
- Press `]` to increase speed by `0.1x`.
- Press `[` to decrease speed by `0.1x`.
- Press `\` to reset speed to `1x`.
- Press `Escape` to close the menu.

### Build From Source

```powershell
dotnet build -c Release
dotnet run -c Release
```

### Privacy And Security

The app starts Edge with a dedicated local profile and restricts remote debugging strictly to `127.0.0.1:9223`. It uses that local endpoint only to inject the `speed-control.js` script and apply `Network.setBlockedURLs` into Prime Video pages opened by the Edge instance it launched.

The injected script stores only local preferences in `localStorage`:
- selected playback speed
- floating button coordinates
- selected subtitle color and status

No credentials, cookies, tokens, viewing history, or personal data are collected or transmitted.

### License

Released under the MIT License. See [LICENSE](LICENSE).

---

<a id="turkce"></a>
## Türkçe

### Genel Bakış

**Prime Video Speed & Subtitle Controller**, Prime Video'yu Microsoft Edge'in uygulama tarzı penceresinde açan, gerçek bir video oynatıcı algılandığında şık bir kayan buton (`1.2x ●` / `1.2x ⚡`) ekleyen, altyazı renklerinin her bölümde sıfırlanma sorununu çözen ve **4 Katmanlı Görünmez Reklam Kalkanı (`Zero-Visibility Ad Shield`)** ile reklamları tamamen yok eden açık kaynaklı bir Windows yardımcı aracıdır.

Amazon Prime Video web oynatıcısında hem altyazı sıfırlanma sorununu çözer (varsayılan **Sarı `#FFCC00`**), hem de reklam aralarında hiçbir reklam videosu veya sesi hissettirmeden (`Siyah Perde + Sessize Alma + 16x Hiper Hız`) kesintisiz bir izleme keyfi sunar.

### Özellikler

- Prime Video'yu yerel ağa kapalı (`127.0.0.1`) özel bir Microsoft Edge uygulama penceresinde açar.
- Hız ve altyazı kontrolünü yalnızca video oynatıcı bulunduğunda gösterir.
- **Her Zaman Aktif 4 Katmanlı Görünmez Reklam Kalkanı (`🛡️ Reklam Kalkanı`):**
  - **Katman 1 (C# Ağ / CDP Engellemesi):** Amazon reklam sunucuları (`amazon-adsystem.com`), telemetri ve takip ağları doğrudan tarayıcı ağ katmanında (`Network.setBlockedURLs`) engellenir.
  - **Katman 2 (CSS Banner Yok Edici):** "Reklam 1/2" afişleri, geri sayım sayaçları ve reklam çubukları kalıcı olarak gizlenir (`display: none !important`).
  - **Katman 3 (Görsel Siyah Perde & Sessize Alma - Blackout Curtain):** Oynatıcıda zorunlu (SSAI) reklam akışı algılandığı an video **sessize alınır (`video.muted = true`)** ve video ekranı **siyah perdeyle kaplanır (`opacity: 0`)**. Ekranda sadece **"⚡ Reklam Atlanıyor..."** yazısı görünür; kullanıcı reklamı asla izlemez ve duymaz.
  - **Katman 4 (Anlık Skip & 16x Hiper Hız):** "Reklamı Atla / Skip Ad" butonu çıktığı milisaniye otomatik tıklanır. Atlanamayan reklamlarda video `16x` hıza alınarak reklam birkaç saniyede aşılır ve asıl içerik normal hızda (`1.2x`) pürüzsüzce geri gelir.
- **Akıllı Otomatik Gizlenme (Temiz Ekran Deneyimi):** Video oynatılmaya başlandığı (`play` / `playing`) veya fare hareketsiz kaldığı andan itibaren tam **2 saniye içinde** kayan buton yumuşakça şeffaflaşıp (`opacity: 0`) ekrandan tamamen kaybolur. Fare oynatıldığında veya video duraklatıldığında anında görünür olur.
- **Kompakt Simge ve Renk Göstergesi:** Uzun yazılar yerine sadece net hız (`1.2x`) ve şık bir simge gösterir:
  - **`1.2x ●`** Altyazı rengi müdahalesi AÇIK olduğunda (nokta `●` seçtiğiniz altyazı renginde parlar!).
  - **`1.2x ⚡`** Altyazı müdahalesi KAPALI olduğunda (sadece hız kontrolü aktif).
- **Otomatik Sarı Altyazı Sabitleme:** Bölüm geçişlerinde altyazı renginin sıfırlanmasını engeller, her replikte seçilen rengi zorlar.
- **İki Bölümlü Cam Efektli (Glassmorphism) Menü:** `⚡ Hız Kontrolü` (+/-, presetler) ve `💬 Altyazı Rengi` (Aç/Kapat anahtarı ve renk paleti) bölümlerini şıkça sunar.
- `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` hazır hızlarını ve `0.1x` hassas adımları destekler.
- 5 hazır altyazı rengi sunar: **Sarı (`#FFCC00`)**, **Altın (`#FFD700`)**, **Beyaz (`#FFFFFF`)**, **Yeşil (`#00FF66`)**, **Açık Mavi (`#00FFFF`)**.
- Kayan buton fareyle sürüklenerek istenen konuma taşınabilir.
- Seçilen hız, buton konumu, altyazı rengi ve durumu yerel tarayıcı profilinde saklanır.

### Kullanım ve Kısayollar

1. Bu depoyu indirin veya klonlayın.
2. `run.cmd` dosyasını çalıştırın.
3. Açılan Prime Video penceresinde oturum açın.
4. Bir film veya bölüm başlatın.
5. `1.2x ●` butonuna tıklayıp istediğiniz hızı ve altyazı rengini seçin. Reklamlar otomatik engellenecektir!

- **`Alt + C` veya `Shift + C`:** Altyazı rengi müdahalesini anında açar / kapatır.
- **`]` / `[`:** Oynatma hızını `0.1x` artırır / azaltır.
- **`\`:** Hızı `1x` değerine sıfırlar.
- **`Escape`:** Açılır menüyü kapatır.

### Gizlilik ve Güvenlik

Proje şifre, çerez, token, izleme geçmişi veya özel hesap verisi toplamaz ve iletmez. Uzaktan hata ayıklama portu yalnızca `127.0.0.1` adresine kilitlidir. Yalnızca seçilen hız, altyazı tercihi ve buton konumu yerel tarayıcı profilinde (`localStorage`) saklanır.

---

<a id="espanol"></a>
## Español

### Resumen

**Prime Video Speed & Subtitle Controller** es una herramienta ligera para Windows que abre Prime Video en Microsoft Edge, estabiliza el color de los subtítulos en **Amarillo (`#FFCC00`)** y elimina la publicidad con un **Escudo de Anuncios de 4 Capas (Zero-Visibility Ad Shield)** que bloquea servidores de anuncios (`Network.setBlockedURLs`), silencia los comerciales y los avanza a velocidad `16x` con cortina negra (`Blackout Curtain`).

### Funciones

- Abre Prime Video en una ventana dedicada de Microsoft Edge (`127.0.0.1`).
- **Escudo de Anuncios de 4 Capas:** Bloqueo en red + Ocultación de banners + Cortina negra y silencio automático (`video.muted = true`) + Salto automático y avance hiperrápido a `16x`.
- **Ocultamiento Automático (Auto-Hide):** El botón flotante desaparece tras 2 segundos de inactividad durante el video.
- **Estabilizador de subtítulos:** Evita que Prime Video restablezca el color de los subtítulos usando `MutationObserver`.
- Menú de dos secciones: `⚡ Velocidad` y `💬 Subtítulos` (Amarillo, Oro, Blanco, Verde, Cian).

---

<a id="deutsch"></a>
## Deutsch

### Überblick

**Prime Video Speed & Subtitle Controller** ist ein leichtes Windows-Hilfsprogramm, das Prime Video in einem Microsoft-Edge-Appfenster öffnet, einen Geschwindigkeitsbutton (`1.2x ●` / `1.2x ⚡`) einblendet, Untertitelfarben fixiert (Standard: **Gelb `#FFCC00`**) und Werbung durch einen **4-stufigen Werbeschutz (Zero-Visibility Ad Shield)** völlig unsichtbar und lautlos macht (`Network.setBlockedURLs` + Blackout Curtain + 16x Fast-Forward).

### Funktionen

- Öffnet Prime Video in einem dedizierten Edge-Fenster.
- **4-stufiger Werbeschutz:** Netzwerksperre für Ad-Server + Blackout Vorhang (`opacity: 0`) mit Stummschaltung beim Abspielen von Werbung + Automatisches Klicken auf "Werbung überspringen" und `16x` Schnelldurchlauf.
- **Auto-Hide Funktion:** Der Button verschwindet nach 2 Sekunden Inaktivität beim Videoabspielen.
- **Untertitel-Stabilisator:** Verhindert das Zurücksetzen der Untertitelfarbe bei Episodenwechseln.
- Zweigeteiltes Menü: `⚡ Geschwindigkeit` und `💬 Untertitel`.

---

<a id="francais"></a>
## Français

### Présentation

**Prime Video Speed & Subtitle Controller** est un utilitaire Windows léger et open-source qui ouvre Prime Video dans Microsoft Edge, ajoute un contrôle de vitesse, maintient la couleur de vos sous-titres en **Jaune (`#FFCC00`)** et intègre un **Bouclier Anti-Pub à 4 Niveaux (Zero-Visibility Ad Shield)** qui bloque les serveurs publicitaires en réseau (`Network.setBlockedURLs`) et masque/silence les pubs insérées à vitesse `16x`.

### Fonctions

- Ouvre Prime Video dans une fenêtre dédiée Microsoft Edge.
- **Bouclier Anti-Pub à 4 Niveaux:** Bloque les domaines publicitaires + Écran noir silencieux pendant les pubs obligatoires + Clic automatique sur "Passer l'annonce" et accélération `16x`.
- **Masquage Automatique (Auto-Hide):** Le bouton disparaît après 2 secondes d'inactivity.
- **Stabilisateur de sous-titres:** Empêche la réinitialisation de la couleur des sous-titres à chaque épisode.
- Menu en deux sections: `⚡ Vitesse` et `💬 Sous-titres`.

---

<a id="portugues"></a>
## Português

### Visão Geral

**Prime Video Speed & Subtitle Controller** é uma ferramenta leve de código aberto para Windows que abre o Prime Video no Microsoft Edge, adiciona controle de velocidade (`1.2x ●` / `1.2x ⚡`), fixa a cor das legendas em **Amarelo (`#FFCC00`)** e bloqueia anúncios com um **Escudo de 4 Camadas (Zero-Visibility Ad Shield)** que silencia e avança anúncios obrigatórios a `16x`.

### Recursos

- Abre o Prime Video em janela dedicada do Microsoft Edge.
- **Escudo Anti-Anúncios de 4 Camadas:** Bloqueio de servidores de anúncios no nível de rede (`Network.setBlockedURLs`) + Cortina preta e silenciamento de áudio + Clique automático em "Pular Anúncio" e avanço de `16x`.
- **Ocultação Automática (Auto-Hide):** O botão desaparece suavemente após 2 segundos de inatividade do mouse durante o vídeo.
- **Estabilizador de Legendas:** Mantém a cor das legendas fixa através de `MutationObserver`.
- Menu elegante com duas seções: `⚡ Velocidade` e `💬 Legendas`.

---

<a id="zh"></a>
## 中文

### 概览

**Prime Video Speed & Subtitle Controller** 是一个开源轻量级 Windows 辅助工具。它在 Microsoft Edge 独立窗口中打开 Prime Video，提供精简控制按钮（`1.2x ●` / `1.2x ⚡`），能稳定锁定字幕颜色为 **黄色 (`#FFCC00`)**，并内置 **4层零可见广告屏蔽盾 (Zero-Visibility Ad Shield)**，彻底拦截网络广告服务器并通过黑屏静音及 `16x` 倍速秒过不可跳过广告。

### 功能

- 在独立的 Microsoft Edge 窗口 (`127.0.0.1`) 中打开 Prime Video。
- **4层零可见广告屏蔽盾:** 在 Chromium 网络层 (`Network.setBlockedURLs`) 屏蔽广告服务器 + 广告期间黑屏静音幕布 (`Blackout Curtain`) + 瞬间自动点击“跳过广告”并开启 `16x` 极速秒过。
- **播放时自动隐藏:** 视频播放开始或鼠标停顿 2 秒后，悬浮按钮平滑隐藏，保持纯净画面。
- **多层字幕锁定:** 利用 `MutationObserver` 强制锁定字幕颜色，跨集不断。
- 精美菜单：`⚡ 播放速度` 与 `💬 字幕颜色`。

---

<a id="hindi"></a>
## हिन्दी

### परिचय

**Prime Video Speed & Subtitle Controller** Windows के लिए एक ओपन-सोर्स और हल्का टूल है जो Prime Video को अलग Microsoft Edge विंडो में खोलता है, हर एपिसोड में subtitle color को **पीला (`#FFCC00`)** बनाए रखता है और **4-Layer Zero-Visibility Ad Shield** के साथ विज्ञापनों को पूरी तरह अदृश्य और शांत कर देता है (`Network.setBlockedURLs` + Blackout Curtain + 16x Fast-Forward)।

### सुविधाएँ

- Prime Video को समर्पित Microsoft Edge window में खोलता है।
- **4-Layer Ad Shield:** नेटवर्क स्तर पर विज्ञापन सर्वर ब्लॉक करना + विज्ञापन के समय काली स्क्रीन और स्वतः म्यूट (`muted = true`) + स्वतः "Skip Ad" क्लिक और `16x` तेज़ गति।
- **Auto-Hide:** वीडियो चलने के 2 सेकंड बाद बटन स्वतः छिप जाता है ताकि स्क्रीन बिल्कुल साफ रहे।
- **Subtitle Stabilizer:** हर एपिसोड में subtitle color को reset होने से रोकता है।
- दो भागों वाला मेनू: `⚡ Speed` और `💬 Subtitle`।

---

<a id="arabic"></a>
## العربية

### نظرة عامة

**Prime Video Speed & Subtitle Controller** هو أداة مفتوحة المصدر وخفيفة لنظام Windows تفتح Prime Video داخل نافذة Microsoft Edge، وتحافظ على لون الترجمة **أصفر (`#FFCC00`)** دائمًا، وتضم **درع إعلانات مخفي من 4 طبقات (Zero-Visibility Ad Shield)** يمنع خوادم الإعلانات شبكيًا ويكتم صوت الإعلانات المدمجة ويسرعها إلى `16x` خلف ستارة سوداء.

### الميزات

- يفتح Prime Video في نافذة Edge مخصصة ومحمية محليًا.
- **درع إعلانات من 4 طبقات:** حجب خوادم الإعلانات والتتبع (`Network.setBlockedURLs`) + شاشة سوداء وكتم الصوت التلقائي أثناء الإعلانات + نقر تلقائي لزر تخطي الإعلان وتسريع الفيديو إلى `16x`.
- **الإخفاء التلقائي أثناء المشاهدة:** يختفي الزر العائم بعد ثانيتين من بدء الفيديو للحصول على شاشة نظيفة.
- **مثبت الترجمة:** يمنع إعادة تعيين لون الترجمة بين الحلقات.
- قائمة حديثة بقسمين: `⚡ السرعة` و `💬 الترجمة`.

---

<a id="russian"></a>
## Русский

### Обзор

**Prime Video Speed & Subtitle Controller** — открытая легкая утилита для Windows, которая открывает Prime Video в отдельном окне Microsoft Edge, фиксирует цвет субтитров на **Желтом (`#FFCC00`)** и полностью устраняет рекламу с помощью **4-уровневого щита нулевой видимости (Zero-Visibility Ad Shield)**, блокирующего рекламные домены на сетевом уровне (`Network.setBlockedURLs`), а также скрывающего рекламные вставки за черным экраном при скорости `16x`.

### Возможности

- Открывает Prime Video в отдельном окне Microsoft Edge.
- **4-уровневый рекламный щит:** Сетевая блокировка серверов рекламы + Черный экран (`Blackout Curtain`) и автоматическое отключение звука при показе рекламы + Мгновенный клик по кнопке "Пропустить рекламу" и ускорение до `16x`.
- **Автоматическое скрытие (Auto-Hide):** Кнопка плавно исчезает через 2 секунды после начала видео.
- **Стабилизатор субтитров:** Исключает сброс цвета субтитров между сериями.
- Двухсекционное меню: `⚡ Скорость` и `💬 Субтитры`.

---

<a id="japanese"></a>
## 日本語

### 概要

**Prime Video Speed & Subtitle Controller** は、Windows 向けのオープンソース軽量ツールです。Prime Video を Microsoft Edge の専用アプリウィンドウで開き、再生速度ボタン (`1.2x ●` / `1.2x ⚡`) を提供し、字幕の色を**黄色 (`#FFCC00`)**に固定するとともに、**4レイヤー広告遮断シールド (Zero-Visibility Ad Shield)** により、広告サーバーのネットワークブロックおよび広告再生時のブラックアウト・消音・`16倍速`自動スキップを実現します。

### 機能

- 専用の Microsoft Edge アプリウィンドウで開きます。
- **4レイヤー広告遮断シールド:** Chromium ネットワーク層での広告サーバーブロック (`Network.setBlockedURLs`) + 広告再生中の画面暗転・ミュート機能 + スキップボタンの自動クリック及び不可避広告の `16倍速` 高速消化。
- **自動非表示機能:** 再生開始から2秒後にボタンがスムーズに消え、クリーンな画面で視聴できます。
- **字幕カラー固定機能:** `MutationObserver` により、次の話へ進んでも字幕色を黄色に保ちます。
- 2セクション構成メニュー: `⚡ 再生速度` と `💬 字幕カラー`。

---

<a id="indonesian"></a>
## Bahasa Indonesia

### Ringkasan

**Prime Video Speed & Subtitle Controller** adalah alat ringan open-source untuk Windows yang membuka Prime Video di Microsoft Edge khusus, mengunci warna subtitle tetap **Kuning (`#FFCC00`)**, dan dilengkapi **Perisai Iklan 4 Lapis Tanpa Tampilan (Zero-Visibility Ad Shield)** yang memblokir server iklan di tingkat jaringan (`Network.setBlockedURLs`) serta membisukan dan mempercepat iklan hingga `16x` di balik layar hitam.

### Fitur

- Membuka Prime Video di jendela Microsoft Edge khusus (`127.0.0.1`).
- **Perisai Iklan 4 Lapis:** Pemblokiran jaringan server iklan + Tirai hitam dan bisu otomatis (`video.muted = true`) saat iklan tayang + Klik otomatis tombol "Lewati Iklan" dan percepatan `16x`.
- **Sembunyi Otomatis (Auto-Hide):** Tombol otomatis menghilang setelah 2 detik video berjalan agar layar bersih.
- **Penstabil Subtitle:** Mencegah Prime Video mereset warna subtitle antar episode.
- Menu dua bagian: `⚡ Kecepatan` dan `💬 Subtitle`.
