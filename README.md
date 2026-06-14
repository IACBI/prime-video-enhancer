# Prime Video Speed Controller

**Prime Video Speed Controller** is a lightweight Windows helper that opens Prime Video in a dedicated Microsoft Edge app window and adds a clean playback speed control to the video player.

It is built for viewers who want a simple speed selector without modifying the official Prime Video app, bypassing DRM, downloading video, or installing a browser extension.

**Author:** **𝓐.𝓒.𝓑**  
**License:** MIT  
**Platform:** Windows, Microsoft Edge, .NET 8

## Languages

[English](#english) · [Türkçe](#turkce) · [Español](#espanol) · [Deutsch](#deutsch) · [Français](#francais) · [Português](#portugues) · [中文](#zh) · [हिन्दी](#hindi) · [العربية](#arabic) · [Русский](#russian) · [日本語](#japanese) · [Bahasa Indonesia](#indonesian)

---

<a id="english"></a>
## English

### Overview

Prime Video Speed Controller launches Prime Video in an Edge app-style window and adds a small speed button when a real video player is detected. The control stays out of the way, can be dragged to a preferred position, and remembers the selected playback speed locally.

This project is intentionally narrow in scope: it improves playback comfort while leaving Prime Video, DRM, account data, and streaming restrictions untouched.

### Features

- Opens Prime Video in a dedicated Microsoft Edge app window.
- Shows the speed control only when video playback is available.
- Includes common presets: `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Supports fine adjustment with `+` and `-` in `0.1x` steps.
- Lets you drag the speed button to a comfortable place on screen.
- Remembers the selected speed and button position locally.
- Reapplies the selected speed if Prime Video resets the video element.

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
5. Click the speed button and choose your preferred speed.

If a published executable exists, `run.cmd` starts it. Otherwise, it runs the project with `dotnet run`.

### Controls

- Click the speed button to open or close the speed menu.
- Drag the speed button while holding the mouse button to move it.
- Click a preset to apply it immediately.
- Use `+` or `-` for `0.1x` speed changes.
- Press `]` to increase speed by `0.1x`.
- Press `[` to decrease speed by `0.1x`.
- Press `\` to reset speed to `1x`.

### Build From Source

```powershell
dotnet build
dotnet run
```

To create a local publish output:

```powershell
dotnet publish -c Release -o publish
```

### Privacy And Security

The app starts Edge with a dedicated local profile and a local debugging endpoint on `127.0.0.1`. It uses that local endpoint only to inject the speed-control script into Prime Video pages opened by the Edge instance it launched.

The injected script stores only two local preferences in the browser profile:

- selected playback speed
- speed button position

No account credentials, cookies, tokens, viewing history, or personal data are collected by this project.

### Limitations

Prime Video can change its player implementation at any time. If the player DOM changes significantly, the speed button may require a small update.

Taskbar icon behavior is partly controlled by Windows and Edge when using Edge app mode. The project prioritizes playback reliability over replacing Edge with a custom browser shell.

### License

Released under the MIT License. See [LICENSE](LICENSE).

---

<a id="turkce"></a>
## Türkçe

### Genel Bakış

**Prime Video Speed Controller**, Prime Video'yu Microsoft Edge'in uygulama tarzı penceresinde açan ve gerçek bir video oynatıcı algılandığında sade bir hız butonu ekleyen küçük bir Windows yardımcı aracıdır. Buton sürüklenebilir, seçtiğiniz konumu hatırlar ve hız tercihinizi yerel olarak saklar.

Projenin amacı yalnızca izleme konforunu artırmaktır. Resmi Prime Video uygulamasını değiştirmez, DRM'i aşmaz, video indirmez ve hesap verilerinize erişmez.

### Özellikler

- Prime Video'yu ayrı bir Microsoft Edge uygulama penceresinde açar.
- Hız kontrolünü yalnızca video oynatıcı bulunduğunda gösterir.
- `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` hazır hızlarını destekler.
- `+` ve `-` ile `0.1x` adımlarla ince ayar yapılabilir.
- Hız butonu fareyle sürüklenerek istenen yere taşınabilir.
- Seçilen hız ve buton konumu yerel olarak hatırlanır.
- Prime Video hızı sıfırlarsa seçilen hızı tekrar uygular.

### Kullanım

1. Bu depoyu indirin veya klonlayın.
2. `run.cmd` dosyasını çalıştırın.
3. Açılan Prime Video penceresinde oturum açın.
4. Bir film veya bölüm başlatın.
5. Hız butonuna tıklayıp istediğiniz hızı seçin.

### Gizlilik

Proje şifre, çerez, token, izleme geçmişi veya özel hesap verisi toplamaz. Yalnızca seçilen hız ve buton konumu yerel tarayıcı profilinde saklanır.

---

<a id="espanol"></a>
## Español

### Resumen

**Prime Video Speed Controller** es una herramienta ligera para Windows que abre Prime Video en una ventana de aplicación de Microsoft Edge y agrega un botón de velocidad cuando detecta un reproductor de video real. El botón se puede mover, recuerda su posición y guarda la velocidad elegida de forma local.

El proyecto está pensado para mejorar la comodidad de reproducción sin modificar la aplicación oficial, evitar DRM ni descargar contenido.

### Funciones

- Abre Prime Video en una ventana dedicada de Microsoft Edge.
- Muestra el control de velocidad solo cuando hay un video disponible.
- Incluye velocidades: `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Permite ajustes finos de `0.1x` con `+` y `-`.
- Permite arrastrar el botón a una posición cómoda.
- Guarda localmente la velocidad y la posición del botón.

### Uso

1. Descarga o clona el repositorio.
2. Ejecuta `run.cmd`.
3. Inicia sesión en Prime Video.
4. Reproduce una película o episodio.
5. Haz clic en el botón de velocidad y elige una opción.

### Privacidad

No recopila contraseñas, cookies, tokens, historial de reproducción ni datos privados de la cuenta. Solo guarda preferencias locales de velocidad y posición.

---

<a id="deutsch"></a>
## Deutsch

### Überblick

**Prime Video Speed Controller** ist ein leichtes Windows-Hilfsprogramm, das Prime Video in einem Microsoft-Edge-Appfenster öffnet und bei erkanntem Videoplayer einen kompakten Geschwindigkeitsbutton einblendet. Der Button kann verschoben werden und merkt sich Geschwindigkeit und Position lokal.

Das Projekt verbessert nur den Bedienkomfort. Es verändert die offizielle Prime-Video-App nicht, umgeht kein DRM und lädt keine Videos herunter.

### Funktionen

- Öffnet Prime Video in einem dedizierten Microsoft-Edge-Appfenster.
- Zeigt die Steuerung nur an, wenn ein Videoplayer vorhanden ist.
- Unterstützt `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Feinanpassung in `0.1x`-Schritten mit `+` und `-`.
- Der Button kann an eine bevorzugte Position gezogen werden.
- Geschwindigkeit und Position werden lokal gespeichert.

### Verwendung

1. Repository herunterladen oder klonen.
2. `run.cmd` starten.
3. Bei Prime Video anmelden.
4. Einen Film oder eine Episode starten.
5. Den Geschwindigkeitsbutton anklicken und eine Option wählen.

### Datenschutz

Das Projekt sammelt keine Passwörter, Cookies, Tokens, Wiedergabeverläufe oder privaten Kontodaten. Es speichert nur lokale Einstellungen für Geschwindigkeit und Buttonposition.

---

<a id="francais"></a>
## Français

### Présentation

**Prime Video Speed Controller** est un utilitaire Windows léger qui ouvre Prime Video dans une fenêtre d'application Microsoft Edge et ajoute un bouton de vitesse lorsqu'un vrai lecteur vidéo est détecté. Le bouton peut être déplacé et mémorise localement la vitesse et sa position.

Le projet améliore uniquement le confort de lecture. Il ne modifie pas l'application officielle, ne contourne pas les DRM et ne télécharge aucun contenu.

### Fonctions

- Ouvre Prime Video dans une fenêtre dédiée Microsoft Edge.
- Affiche le contrôle uniquement lorsqu'une vidéo est disponible.
- Propose `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Permet un réglage fin par pas de `0.1x` avec `+` et `-`.
- Permet de déplacer le bouton à l'endroit souhaité.
- Enregistre localement la vitesse et la position du bouton.

### Utilisation

1. Téléchargez ou clonez ce dépôt.
2. Lancez `run.cmd`.
3. Connectez-vous à Prime Video.
4. Lancez un film ou un épisode.
5. Cliquez sur le bouton de vitesse et choisissez une valeur.

### Confidentialité

Le projet ne collecte pas les mots de passe, cookies, jetons, historiques de visionnage ou données privées du compte. Il conserve seulement les préférences locales de vitesse et de position.

---

<a id="portugues"></a>
## Português

### Visão Geral

**Prime Video Speed Controller** é uma ferramenta leve para Windows que abre o Prime Video em uma janela de aplicativo do Microsoft Edge e adiciona um botão de velocidade quando detecta um player de vídeo real. O botão pode ser movido e lembra a velocidade e a posição localmente.

O projeto foi feito para melhorar o conforto de reprodução sem modificar o aplicativo oficial, contornar DRM ou baixar vídeos.

### Recursos

- Abre o Prime Video em uma janela dedicada do Microsoft Edge.
- Mostra o controle somente quando há vídeo disponível.
- Inclui `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Permite ajuste fino em passos de `0.1x` com `+` e `-`.
- Permite arrastar o botão para uma posição confortável.
- Salva localmente a velocidade e a posição do botão.

### Uso

1. Baixe ou clone este repositório.
2. Execute `run.cmd`.
3. Entre na sua conta do Prime Video.
4. Reproduza um filme ou episódio.
5. Clique no botão de velocidade e escolha uma opção.

### Privacidade

O projeto não coleta senhas, cookies, tokens, histórico de reprodução ou dados privados da conta. Ele salva apenas preferências locais de velocidade e posição.

---

<a id="zh"></a>
## 中文

### 概览

**Prime Video Speed Controller** 是一个轻量级 Windows 辅助工具。它会在独立的 Microsoft Edge 应用窗口中打开 Prime Video，并在检测到真实视频播放器时添加一个简洁的播放速度按钮。按钮可以拖动，并会在本地记住所选速度和位置。

本项目只用于提升观看体验。它不会修改官方 Prime Video 应用，不会绕过 DRM，也不会下载视频内容。

### 功能

- 在独立的 Microsoft Edge 应用窗口中打开 Prime Video。
- 仅在检测到视频播放器时显示速度控制。
- 支持 `0.5x`、`1x`、`1.25x`、`1.5x`、`1.75x`、`2x`。
- 可使用 `+` 和 `-` 以 `0.1x` 为步长微调速度。
- 可将速度按钮拖动到合适的位置。
- 在本地保存播放速度和按钮位置。

### 使用方法

1. 下载或克隆本仓库。
2. 运行 `run.cmd`。
3. 在打开的窗口中登录 Prime Video。
4. 播放电影或剧集。
5. 点击速度按钮并选择所需速度。

### 隐私

本项目不会收集密码、Cookie、令牌、观看记录或账号隐私数据。它只在本地保存速度和按钮位置偏好。

---

<a id="hindi"></a>
## हिन्दी

### परिचय

**Prime Video Speed Controller** Windows के लिए एक हल्का सहायक टूल है। यह Prime Video को Microsoft Edge की अलग ऐप विंडो में खोलता है और असली वीडियो प्लेयर मिलने पर एक साफ playback speed बटन जोड़ता है। बटन को खींचकर मनचाही जगह रखा जा सकता है और चुनी हुई speed स्थानीय रूप से याद रहती है।

यह प्रोजेक्ट केवल देखने का अनुभव बेहतर करने के लिए है। यह आधिकारिक Prime Video ऐप को बदलता नहीं है, DRM को bypass नहीं करता और वीडियो डाउनलोड नहीं करता।

### सुविधाएँ

- Prime Video को अलग Microsoft Edge app window में खोलता है।
- Speed control केवल वीडियो उपलब्ध होने पर दिखता है।
- `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x` presets उपलब्ध हैं।
- `+` और `-` से `0.1x` steps में fine adjustment किया जा सकता है।
- Speed button को drag करके जगह बदली जा सकती है।
- Speed और button position स्थानीय रूप से सेव होती है।

### उपयोग

1. Repository डाउनलोड या clone करें।
2. `run.cmd` चलाएँ।
3. खुली हुई Prime Video window में sign in करें।
4. कोई movie या episode चलाएँ।
5. Speed button पर click करके अपनी speed चुनें।

### गोपनीयता

यह प्रोजेक्ट passwords, cookies, tokens, watch history या private account data collect नहीं करता। यह केवल स्थानीय speed और button position preferences सेव करता है।

---

<a id="arabic"></a>
## العربية

### نظرة عامة

**Prime Video Speed Controller** هو أداة خفيفة لنظام Windows تفتح Prime Video داخل نافذة تطبيق مخصصة من Microsoft Edge، وتضيف زر سرعة بسيطًا عند اكتشاف مشغل فيديو فعلي. يمكن سحب الزر إلى المكان المناسب، ويتم حفظ السرعة والموقع محليًا.

الغرض من المشروع هو تحسين تجربة المشاهدة فقط. لا يغير تطبيق Prime Video الرسمي، ولا يتجاوز DRM، ولا يقوم بتنزيل الفيديوهات.

### الميزات

- يفتح Prime Video داخل نافذة Microsoft Edge مخصصة.
- يعرض زر السرعة فقط عند توفر مشغل فيديو.
- يدعم السرعات `0.5x` و `1x` و `1.25x` و `1.5x` و `1.75x` و `2x`.
- يسمح بضبط دقيق بمقدار `0.1x` عبر `+` و `-`.
- يمكن سحب زر السرعة إلى مكان مريح.
- يحفظ السرعة المختارة وموقع الزر محليًا.

### الاستخدام

1. قم بتنزيل المستودع أو نسخه.
2. شغّل `run.cmd`.
3. سجّل الدخول إلى Prime Video في النافذة التي تفتح.
4. شغّل فيلمًا أو حلقة.
5. اضغط زر السرعة واختر القيمة المناسبة.

### الخصوصية

لا يجمع المشروع كلمات مرور أو ملفات تعريف ارتباط أو رموز وصول أو سجل مشاهدة أو بيانات حساب خاصة. يتم حفظ تفضيلات السرعة وموقع الزر محليًا فقط.

---

<a id="russian"></a>
## Русский

### Обзор

**Prime Video Speed Controller** — легкая утилита для Windows, которая открывает Prime Video в отдельном окне приложения Microsoft Edge и добавляет аккуратную кнопку скорости при обнаружении реального видеоплеера. Кнопку можно перетащить в удобное место, а выбранная скорость и позиция сохраняются локально.

Проект предназначен только для удобства просмотра. Он не изменяет официальное приложение Prime Video, не обходит DRM и не скачивает видео.

### Возможности

- Открывает Prime Video в отдельном окне Microsoft Edge.
- Показывает управление скоростью только при наличии видео.
- Поддерживает `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Позволяет менять скорость шагом `0.1x` через `+` и `-`.
- Позволяет перетаскивать кнопку скорости.
- Локально сохраняет скорость и положение кнопки.

### Использование

1. Скачайте или клонируйте репозиторий.
2. Запустите `run.cmd`.
3. Войдите в Prime Video в открывшемся окне.
4. Запустите фильм или эпизод.
5. Нажмите кнопку скорости и выберите нужное значение.

### Конфиденциальность

Проект не собирает пароли, cookies, токены, историю просмотра или личные данные аккаунта. Он сохраняет только локальные настройки скорости и положения кнопки.

---

<a id="japanese"></a>
## 日本語

### 概要

**Prime Video Speed Controller** は、Windows 向けの軽量ヘルパーツールです。Prime Video を Microsoft Edge の専用アプリウィンドウで開き、実際の動画プレーヤーを検出したときだけシンプルな再生速度ボタンを追加します。ボタンはドラッグでき、選択した速度と位置はローカルに保存されます。

このプロジェクトは視聴体験を改善するためのものです。公式 Prime Video アプリを変更せず、DRM を回避せず、動画をダウンロードしません。

### 機能

- Prime Video を専用の Microsoft Edge アプリウィンドウで開きます。
- 動画プレーヤーがあるときだけ速度コントロールを表示します。
- `0.5x`、`1x`、`1.25x`、`1.5x`、`1.75x`、`2x` に対応します。
- `+` と `-` で `0.1x` 単位の微調整ができます。
- 速度ボタンを好みの位置にドラッグできます。
- 速度とボタン位置をローカルに保存します。

### 使い方

1. このリポジトリをダウンロードまたは clone します。
2. `run.cmd` を実行します。
3. 開いた Prime Video ウィンドウでサインインします。
4. 映画またはエピソードを再生します。
5. 速度ボタンをクリックして速度を選択します。

### プライバシー

このプロジェクトはパスワード、Cookie、トークン、視聴履歴、個人アカウント情報を収集しません。保存するのはローカルの速度設定とボタン位置だけです。

---

<a id="indonesian"></a>
## Bahasa Indonesia

### Ringkasan

**Prime Video Speed Controller** adalah alat ringan untuk Windows yang membuka Prime Video di jendela aplikasi Microsoft Edge khusus dan menambahkan tombol kecepatan saat pemutar video terdeteksi. Tombol dapat dipindahkan, serta menyimpan kecepatan dan posisinya secara lokal.

Proyek ini dibuat untuk meningkatkan kenyamanan menonton saja. Proyek ini tidak mengubah aplikasi resmi Prime Video, tidak melewati DRM, dan tidak mengunduh video.

### Fitur

- Membuka Prime Video di jendela Microsoft Edge khusus.
- Menampilkan kontrol kecepatan hanya saat video tersedia.
- Mendukung `0.5x`, `1x`, `1.25x`, `1.5x`, `1.75x`, `2x`.
- Mendukung penyesuaian `0.1x` dengan `+` dan `-`.
- Tombol kecepatan dapat diseret ke posisi yang nyaman.
- Kecepatan dan posisi tombol disimpan secara lokal.

### Cara Pakai

1. Unduh atau clone repositori ini.
2. Jalankan `run.cmd`.
3. Masuk ke Prime Video di jendela yang terbuka.
4. Putar film atau episode.
5. Klik tombol kecepatan dan pilih opsi yang diinginkan.

### Privasi

Proyek ini tidak mengumpulkan kata sandi, cookie, token, riwayat tontonan, atau data akun pribadi. Hanya preferensi kecepatan dan posisi tombol yang disimpan secara lokal.
