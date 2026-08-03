# Prime Video Enhancer v3.5.5 — Subtitle Style Flash Elimination & Proportional Scaling

## What's New in v3.5.5

- **Zero Subtitle Flash:** Added a synchronous fast-path inside the `MutationObserver` callback. When new subtitle elements are injected by Prime Video, our custom styles are applied immediately in the microtask queue before the browser paints, completely eliminating the initial flash of Amazon's default font size and color.
- **Proportional Font Size Scaling (`computeSubtitleSizeVh`):** Subtitle font size is now calculated using a calibrated viewport-relative (`vh`) formula (`100% = 1.85vh`). This ensures consistent font proportions across all window sizes and fullscreen modes without compounding with Prime Video's container styles or overflowing the player at `%200` setting.
- **Background & Shadow Rendering Fixes:**
  - Container element backgrounds (`.atvwebplayersdk-subtitle-text`) are cleared to `transparent !important` when child text elements are styled, eliminating double-background box artifacts.
  - Added `line-height: 1.35 !important`, `padding: 0.12em 0.35em !important`, `border-radius: 4px !important`, `-webkit-box-decoration-break: clone !important`, and `box-decoration-break: clone !important` to ensure text lines never overlap and background boxes render with smooth, uniform corners across line wraps.
  - Text shadows scale dynamically with font size and render a crisp stroke outline in `None (Transparent)` background mode.
- Includes all packaging, CDP, ad shield, and subtitle isolation features from v3.5.4.

---

# Prime Video Enhancer v3.5.4 — Packaging & Release Maintenance

## What's New in v3.5.4

- Windows executable metadata now reports the same version as the GitHub release and injected controller.
- GitHub Actions dependencies were updated to their current Node.js 24-based major versions, removing deprecated Node.js 20 runtime warnings from release builds.
- Includes all subtitle isolation, CDP reliability, ad classification, and runtime performance improvements introduced in v3.5.3.

---

# Prime Video Enhancer v3.5.3 — Subtitle Isolation & Ad Shield Reliability

## What's New in v3.5.3

- Subtitle size, color, and background are now applied only to verified text in the lower subtitle region of the active video. Episode titles and player controls are excluded, and original inline styles are restored when subtitle nodes are recycled.
- Fixed CDP response matching where command ID `10` was incorrectly accepted as ID `1`, causing the script to be reinjected every polling cycle and multiplying timers/event listeners.
- Added a controller lifecycle cleanup API so future version upgrades stop old timers, observers, video listeners, and global handlers before reinjection.
- CDP WebSocket responses are now assembled across all frames and parsed as JSON before command IDs are accepted.
- Fetch interception now releases paused requests on handler errors instead of leaving playback requests hanging.
- Amazon ad/telemetry host classification now matches the configured Fetch patterns, including regional `unagi`, `fls`, `device-metrics`, `mads`, and `aan.amazon.co.*` hosts.
- Intro/outro buttons are no longer counted as ads; visible Skip Ad buttons are clicked and counted only once per DOM element.
- Added dependency-free .NET regression tests and a live CDP browser smoke test for subtitle isolation and reinjection stability.

---

# 🚀 Prime Video Enhancer v3.5.0 — 30x Hyper-Speed Ad Shield & Automatic 16x Fallback

## What's New in v3.5.0

### ⚡ 30x Hyper-Speed Ad Shield
- Upgraded ad fast-forward speed from 16x to **30x** during unskippable stitched ads.
- A 30-second ad break now finishes in just **1 second**!
- **Automatic Fallback:** Added real-time playback stall monitoring (`waiting` and `stalled` event listeners). If a browser's GPU hardware decoder slows down at 30x, the shield seamlessly falls back to 16x to guarantee smooth playback without freezing or buffering.
- **Clean Black Screen:** The ad cover overlay is 100% clean black with no text during ad skipping.

---

# 🚀 Prime Video Enhancer v3.4.0 — Custom Subtitle Size, Background Fix & Ad Shield Hardening

## What's New in v3.4.0

### 📝 Custom Subtitle Size Input
- Replaced the old cycle button (Small / Normal / Large / Huge) with a **real number input** field.
- Type any value from **50% to 400%** and press Enter (or click away) to apply instantly.
- Your setting is remembered across sessions via `localStorage`.

### 🎨 Subtitle Background Fixed
- The `Bg` toggle (Shadow → Solid → None) now works correctly in all modes.
- Root cause fixed: Prime Video was injecting inline `background-color` styles directly on subtitle span elements, overriding our CSS. The controller now **strips those inline overrides** before our `!important` CSS rule is applied, so all three Bg modes render correctly.
- The default "Shadow" mode now uses a slightly tuned `rgba(0,0,0,0.45)` semi-transparent background for improved legibility.

### 🛡️ Ad Shield Improvements
- The `containsAdWord()` word-boundary tokenizer ensures that UI elements whose class names merely *contain* "ad" as a substring (e.g. `loadTimer`, `threadIndicator`) are **never** mistakenly treated as ad indicators.
- Countdown text validation (`COUNTDOWN_TEXT_RE`) still required before engaging the shield — prevents false positives from persistent empty containers.
- Safety valve (`AD_MAX_DURATION_MS = 45s`) with a 2-minute cooldown prevents infinite 16x/black-screen loops.
- `restoreHiddenVideos()` now scans the entire DOM for `data-pvsc-hidden` markers, restoring video elements re-created during ad/content transitions.

### 🔧 Bug Fixes
- Typing in the subtitle size input no longer triggers speed hotkeys.
- Clicking the input no longer closes the menu.
- Episode title and top-bar UI no longer scale when subtitle size changes.
- `Bg: None` mode now correctly removes all background from subtitles.

---

## Download

| File | Size | Notes |
|------|------|-------|
| `PrimeVideoSpeedApp-Light.exe` | ~213 KB | Requires .NET 8 Runtime |
| `PrimeVideoSpeedApp-Standalone.exe` | ~64 MB | Self-contained, no prerequisites |

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `]` / `+` / `↑` | Speed up +0.1x |
| `[` / `-` / `↓` | Slow down −0.1x |
| `\` | Reset to 1x |
| `s` | Toggle subtitle color overlay |
| `n` | Skip intro / next episode |
| `Shift+C` | Toggle subtitle overlay |
| `Escape` | Close menu |

---

# 🚀 Prime Video Enhancer v3.0.0 — CDP Fetch Ad Interception & Freeze-Frame Video Shield

We are thrilled to release **v3.0.0** of the **Prime Video Enhancer (Speed & Subtitle Controller)**! This major release upgrades our ad blocking to the next level by introducing uBlock Origin-style network-request-level interception and canvas-based freeze-frame video overlays, resulting in a completely seamless, zero-interruption ad-free viewing experience.

### 🔥 Key Features & Highlights in v3.0.0

- **CDP Fetch Interception (uBlock Origin Level):** Replaced legacy domain blocking with Chromium's native `Fetch.enable` and `Fetch.requestPaused` CDP domains. Intercepts and blocks ad requests *before* any bytes are downloaded. Returns custom empty VAST/VPAID response XMLs for stitched player-level ads.
- **Canvas-based Freeze-Frame Overlays:** Completely removed the dark "⚡ Reklam Atlanıyor..." blackout curtain and text. Now, when a stitched SSAI ad segment is detected, the controller captures the last valid video frame using HTML5 Canvas, overlaying it statically while the ad plays silently at `16x` hyper-speed in the background. The viewer only experiences a brief, natural scene freeze without seeing or hearing commercial interruptions.
- **Robust Visibility Checks:** Added active layout tree verification (`offsetParent` & dimensions) to eliminate false ad detection on landing page billboards or hidden containers.
- **Dynamic C# Fetch Listener**: Redesigned the C# backend to run a persistent background WebSocket message loop for ongoing Fetch request pause-and-resume operations.


---

# 🚀 Prime Video Enhancer v2.0.0 — Zero-Visibility Ad Shield, 16x Hyper-Speed & Hybrid Architecture

We are thrilled to release **v2.0.0** of the **Prime Video Enhancer (Speed & Subtitle Controller)**! This release introduces state-of-the-art multi-layer ad blocking (`Reklam Kalkanı`), hyper-speed ad digestion, intelligent auto-hiding UI, and a revolutionary **Hybrid Priority Architecture** available in two single-file `.exe` formats.

---

## 🔥 Key Features & Highlights

### 🛡️ 4-Layer Zero-Visibility Ad Shield (`Reklam Kalkanı`)
- **Layer 1 (Chromium Network Block)**: Intercepts and blocks Amazon ad servers (`amazon-adsystem.com`), telemetry, and tracking networks directly at the Chromium protocol layer via `Network.setBlockedURLs`.
- **Layer 2 (CSS Banner Destroyer)**: Permanently hides "Ad 1 of 2" indicators, countdown banners, and overlays (`display: none !important`).
- **Layer 3 (Blackout Curtain & Auto-Mute)**: Instantly mutes audio (`video.muted = true`) and conceals the screen behind a dark `⚡ Reklam Atlanıyor...` curtain during mandatory Server-Side Ad Insertion (SSAI). You never hear or see commercials.
- **Layer 4 (Auto-Skip & 16x Hyper-Speed)**: Automatically clicks "Skip Ad" within milliseconds or hyper-accelerates unskippable ads at `16x` playback speed (`video.playbackRate = 16`), digesting them in seconds before smoothly restoring your custom streaming speed (`1.2x`).

### 📦 Hybrid Priority Architecture
We have completely redesigned asset and script delivery:
- **Developer Hot-Reloading**: If `speed-control.js` or `AppIcon.ico` exist alongside the executable, the app prioritizes reading them dynamically from disk.
- **Zero-Dependency Single-File Execution**: When running as a standalone `.exe`, all resources (`speed-control.js` & `AppIcon.ico`) are loaded seamlessly from embedded `<EmbeddedResource>` streams inside C#. No external folders or JS files are required to run!

### 🎬 Sleek Glassmorphism UI & Subtitle Stabilizer
- **Draggable & Auto-Hiding Button**: The floating control button (`1.2x ●`) automatically fades out exactly 2 seconds after video playback begins for a pristine viewing screen, and reappears instantly when you move the mouse.
- **Multi-Layer Subtitle Color Lock**: Choose from 5 vibrant presets (**Sarı `#FFCC00`**, **Altın `#FFD700`**, **Beyaz `#FFFFFF`**, **Yeşil `#00FF66`**, **Mavi `#00FFFF`**). Our active `MutationObserver` ensures colors persist across episodes and DOM resets.
- **Custom Edge AppUserModelID Grouping**: Dedicates `msedge.exe` windows specifically to `PrimeVideoSpeedController.App`, keeping your taskbar clean and organized.

---

## 📥 Download & Installation

Choose the distribution format that best fits your system from the **Assets** below:

### 1. `PrimeVideoSpeedApp-Standalone.exe` (~64 MB) — ⭐ Recommended for Most Users
- **Self-Contained & Portable**: Includes the complete `.NET 8 Runtime` and Windows API bindings embedded inside a single `.exe`.
- **Zero Prerequisites**: Works instantly out-of-the-box on **any 64-bit Windows 10/11 PC**. Just download and double-click!

### 2. `PrimeVideoSpeedApp-Light.exe` (~213 KB) — 🪶 Ultra-Lightweight
- **Framework-Dependent**: Minimal binary size (only ~213 KB).
- **Prerequisite**: Requires `.NET 8 Desktop Runtime` installed on your Windows machine. If not present, Windows will prompt you to download it automatically.

---

## 📚 Multilingual Documentation & Automated CI/CD
- Full setup, keyboard shortcuts, and hybrid architecture guides are available in **12 languages** in our `README.md`.
- **Automated GitHub Releases Workflow (`.github/workflows/release.yml`)**: Every time a version tag is pushed, our automated workflow builds and packages both distribution versions. Or build locally anytime using `publish.cmd`!
