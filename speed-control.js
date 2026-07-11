(() => {
  const ROOT_ID = "pvsc-root";
  const STYLE_ID = "pvsc-style";
  const SUBTITLE_STYLE_ID = "pvsc-subtitle-style";
  const AD_SHIELD_STYLE_ID = "pvsc-ad-shield-style";
  const AD_COVER_ID = "pvsc-ad-freeze-canvas";
  const STORAGE_KEY = "primeVideoSpeedControl.speed";
  const POSITION_KEY = "primeVideoSpeedControl.position";
  const SUBTITLE_STORAGE_KEY = "primeVideoSpeedControl.subtitleColor";
  const SUBTITLE_ENABLED_KEY = "primeVideoSpeedControl.subtitleEnabled";
  
  const MIN_SPEED = 0.25;
  const MAX_SPEED = 4;
  const STEP = 0.1;
  const DEFAULT_SPEED = 1;
  const PRESET_SPEEDS = [0.5, 1, 1.25, 1.5, 1.75, 2];

  const DEFAULT_SUBTITLE_COLOR = "#FFCC00";
  const PRESET_COLORS = [
    { name: "Sarı", hex: "#FFCC00" },
    { name: "Altın", hex: "#FFD700" },
    { name: "Beyaz", hex: "#FFFFFF" },
    { name: "Yeşil", hex: "#00FF66" },
    { name: "Mavi", hex: "#00FFFF" },
  ];

  // How many consecutive "no ad detected" ticks are required before ad-mode is
  // exited. A single missed detection (e.g. Prime Video briefly re-rendering the
  // indicator element during an ad-to-content transition) would otherwise unmute/
  // un-freeze the video prematurely and immediately re-trigger ad-mode on the next
  // tick, producing an audible/visible flicker right at ad boundaries.
  const AD_END_CONFIRM_TICKS = 2;

  // Safety valve: real Amazon Prime Video ad breaks don't run this long. If the
  // shield has been continuously engaged (muted, 16x, video hidden) for longer
  // than this, it's almost certainly a stuck or false detection rather than a
  // real ad, so playback is forcibly handed back to the user instead of racing
  // through the rest of the episode at 16x behind a black cover.
  const AD_MAX_DURATION_MS = 45000;

  // After the safety valve force-exits ad mode, suppress re-engaging the visual
  // shield for this long. Without a cooldown, a persistently-matching element
  // (a stuck indicator, or UI we misclassify) re-engages ad mode on the very
  // next 200ms tick after the valve fires, turning one false positive into an
  // endless loop of black-screen/16x windows. Skip-button clicking and the
  // network-level blocker stay active during the cooldown, so real ads are
  // still handled — only the mute/hide/16x shield is suppressed.
  const AD_COOLDOWN_AFTER_VALVE_MS = 120000;

  const AD_SKIP_BUTTON_SELECTOR =
    ".atvwebplayersdk-ad-skip-button, [class*='adSkipButton' i], [class*='ad-skip-button' i], [aria-label*='skip ad' i], [aria-label*='reklamı atla' i], [aria-label*='reklamı geç' i], button[title*='skip' i], button[title*='atla' i], [data-testid*='skip' i], div[class*='ad-skip' i]";

  // Note: .atvwebplayersdk-ad-resume-message is intentionally excluded here. It
  // appears once the ad break has ENDED and real content is resuming, so treating
  // it as an "ad still active" signal (as before) kept the video muted/16x-speed/
  // frozen for the first few seconds of real content.
  const AD_INDICATOR_SELECTOR = [
    ".atvwebplayersdk-ad-timer-countdown",
    ".atvwebplayersdk-ad-timer",
    ".atvwebplayersdk-ad-timer-text",
    ".atvwebplayersdk-ad-timer-remaining-time",
    ".atvwebplayersdk-ad-indicator",
    ".atvwebplayersdk-adbreak-indicator",
    "[class*='adIndicator' i]",
    "[class*='adBreak' i]",
    "[class*='adTimer' i]",
    "[class*='adCountdown' i]",
    "[class*='ad-timer' i]",
    "[class*='ad-break' i]",
    "[class*='ad-countdown' i]",
    "[data-testid*='ad-indicator' i]",
    "[data-testid*='ad-break' i]",
    "[data-testid*='ad-timer' i]",
    "[data-testid*='ad-countdown' i]",
    ".ad-timer",
    ".ad-countdown",
    ".ad-break-container"
  ].join(", ");

  if (window.__primeVideoSpeedControl?.installed && window.__primeVideoSpeedControl?.version === "3.1.0") {
    window.__primeVideoSpeedControl.refresh();
    window.__primeVideoSpeedControl.applySpeed();
    window.__primeVideoSpeedControl.applySubtitleStyles();
    window.__primeVideoSpeedControl.checkAndHandleAds();
    return "already-installed";
  }

  let speed = Number(window.localStorage.getItem(STORAGE_KEY));
  if (!Number.isFinite(speed) || speed < MIN_SPEED || speed > MAX_SPEED) {
    speed = DEFAULT_SPEED;
  }

  let subtitleColor = window.localStorage.getItem(SUBTITLE_STORAGE_KEY) || DEFAULT_SUBTITLE_COLOR;
  if (!/^#[0-9A-Fa-f]{6}$/.test(subtitleColor)) {
    subtitleColor = DEFAULT_SUBTITLE_COLOR;
  }

  let subtitleEnabled = window.localStorage.getItem(SUBTITLE_ENABLED_KEY) !== "false";

  let hideTimer = 0;
  let isMenuOpen = false;
  let isDragging = false;
  let dragStarted = false;
  let dragOffsetX = 0;
  let dragOffsetY = 0;
  let lastPointerX = 0;
  let lastPointerY = 0;

  let currentObservedContainer = null;
  let subtitleObserver = null;
  let attachedVideo = null;
  let mutationThrottleTimer = 0;

  let isAdCurrentlyActive = false;
  let wasMutedBeforeAd = false;
  let noAdStreak = 0;
  let adModeStartedAt = 0;
  let adCooldownUntil = 0;
  let adHiddenVideo = null;

  function clamp(value) {
    return Math.min(MAX_SPEED, Math.max(MIN_SPEED, value));
  }

  function format(value) {
    return `${value.toFixed(2).replace(/\.?0+$/, "")}x`;
  }

  function updateButtonDisplay() {
    if (!speedButton) return;
    const formattedSpeed = format(speed);
    if (subtitleEnabled) {
      speedButton.innerHTML = `${formattedSpeed} <span style="color: ${subtitleColor}; margin-left: 6px; font-size: 13px; text-shadow: 0 0 5px rgba(0,0,0,0.85);">●</span>`;
    } else {
      speedButton.innerHTML = `${formattedSpeed} <span style="color: rgba(255,255,255,0.48); margin-left: 6px; font-size: 12px;">⚡</span>`;
    }
  }

  function findVideo() {
    const videos = Array.from(document.querySelectorAll("video"));
    return (
      videos.find((video) => video.readyState > 0) ||
      videos.find((video) => video.currentSrc || video.src) ||
      videos[0] ||
      null
    );
  }

  function handleVideoPlaybackState() {
    if (!attachedVideo) return;
    if (isAdCurrentlyActive) {
      if (attachedVideo.playbackRate !== 16) attachedVideo.playbackRate = 16;
      if (attachedVideo.defaultPlaybackRate !== 16) attachedVideo.defaultPlaybackRate = 16;
      if (attachedVideo.muted !== true) attachedVideo.muted = true;
    } else {
      if (attachedVideo.playbackRate !== speed) attachedVideo.playbackRate = speed;
      if (attachedVideo.defaultPlaybackRate !== speed) attachedVideo.defaultPlaybackRate = speed;
    }
  }

  function attachVideoListeners(video) {
    if (!video || video === attachedVideo) {
      return;
    }
    if (attachedVideo) {
      attachedVideo.removeEventListener("play", showControls);
      attachedVideo.removeEventListener("playing", showControls);
      attachedVideo.removeEventListener("pause", showControls);
      attachedVideo.removeEventListener("seeked", showControls);
      attachedVideo.removeEventListener("ratechange", handleVideoPlaybackState);
      attachedVideo.removeEventListener("play", handleVideoPlaybackState);
      attachedVideo.removeEventListener("playing", handleVideoPlaybackState);
      attachedVideo.removeEventListener("timeupdate", handleVideoPlaybackState);
    }
    attachedVideo = video;
    attachedVideo.addEventListener("play", showControls, { passive: true });
    attachedVideo.addEventListener("playing", showControls, { passive: true });
    attachedVideo.addEventListener("pause", showControls, { passive: true });
    attachedVideo.addEventListener("seeked", showControls, { passive: true });
    attachedVideo.addEventListener("ratechange", handleVideoPlaybackState, { passive: true });
    attachedVideo.addEventListener("play", handleVideoPlaybackState, { passive: true });
    attachedVideo.addEventListener("playing", handleVideoPlaybackState, { passive: true });
    attachedVideo.addEventListener("timeupdate", handleVideoPlaybackState, { passive: true });
    showControls();
  }

  function applySpeed() {
    const video = findVideo();
    if (!video) {
      return;
    }
    attachVideoListeners(video);

    if (isAdCurrentlyActive) {
      if (video.playbackRate !== 16) video.playbackRate = 16;
      if (video.defaultPlaybackRate !== 16) video.defaultPlaybackRate = 16;
      if (video.muted !== true) video.muted = true;
      return;
    }

    if (video.playbackRate !== speed) {
      video.playbackRate = speed;
    }

    if (video.defaultPlaybackRate !== speed) {
      video.defaultPlaybackRate = speed;
    }
  }

  function ensureAdShieldStyle() {
    if (document.getElementById(AD_SHIELD_STYLE_ID)) {
      return;
    }
    const style = document.createElement("style");
    style.id = AD_SHIELD_STYLE_ID;
    style.textContent = `
      .atvwebplayersdk-ad-indicator,
      .atvwebplayersdk-adbreak-indicator,
      .atvwebplayersdk-ad-timer,
      .atvwebplayersdk-ad-timer-countdown,
      .atvwebplayersdk-ad-timer-text,
      .atvwebplayersdk-ad-timer-ad-text,
      .atvwebplayersdk-ad-timer-remaining-time,
      .atvwebplayersdk-ad-resume-message,
      [class*="adIndicator" i],
      [class*="adBreak" i],
      [class*="adTimer" i],
      [class*="adCountdown" i],
      [class*="ad-timer" i],
      [class*="ad-break" i],
      [class*="ad-countdown" i],
      [data-testid*="ad-indicator" i],
      [data-testid*="ad-break" i],
      [data-testid*="ad-timer" i],
      [data-testid*="ad-countdown" i],
      .ad-timer,
      .ad-countdown,
      .ad-break-container,
      .dv-signup-button,
      [class*="dv-signup-button" i] {
        opacity: 0 !important;
        pointer-events: none !important;
      }
      #${AD_COVER_ID} {
        position: absolute !important;
        top: 0 !important;
        left: 0 !important;
        width: 100% !important;
        height: 100% !important;
        z-index: 2147483640 !important;
        pointer-events: none !important;
        background: #000 !important;
      }
    `;
    document.documentElement.appendChild(style);
  }

  // Prime Video's <video> element is always DRM/EME-protected (Widevine), and
  // drawing a protected video frame onto a <canvas> (ctx.drawImage) throws a
  // SecurityError from a tainted-canvas check — every browser blocks exactly this
  // to prevent DRM bypass via screen capture. The previous implementation tried to
  // capture a real freeze-frame via canvas, which therefore failed silently on
  // every real ad (caught by an empty catch), leaving the video hidden
  // (opacity: 0) with nothing drawn over it — a black screen while the video kept
  // playing at 16x underneath. A plain opaque cover element can't fail this way:
  // it doesn't touch the video's pixels at all, so it always renders.
  function showAdCover(video) {
    if (document.getElementById(AD_COVER_ID)) return; // Already showing
    const cover = document.createElement("div");
    cover.id = AD_COVER_ID;
    const container = video.closest(".webPlayerSDKContainer, .atvwebplayersdk-player-container, #player, .player") || video.parentElement || document.body;
    container.appendChild(cover);
  }

  function removeAdCover() {
    const cover = document.getElementById(AD_COVER_ID);
    if (cover) cover.remove();
  }

  // "adbreak"/"adtimer" etc. cover tokens like Amazon's own
  // ".atvwebplayersdk-adbreak-indicator", where "ad" and "break" are fused with no
  // hyphen or camelCase boundary between them, so splitting alone wouldn't isolate
  // a standalone "ad" token for that one.
  const AD_WORD_TOKENS = new Set(["ad", "adbreak", "adtimer", "adindicator", "adcountdown"]);

  // A real Prime Video ad indicator always displays a live countdown — either a
  // clock ("0:27", "1:05") or a "27 s"/"27 sec"/"27 saniye" remaining-time label.
  // Requiring countdown-shaped text is what keeps a persistently-mounted shell
  // element (e.g. an "ad-break-container" that stays in the DOM between breaks
  // holding a static label or episode text) from engaging the shield on normal
  // content, which showed up to the user as a black screen with the episode
  // racing at 16x underneath.
  const COUNTDOWN_TEXT_RE = /(\d{1,2}:\d{2})|(\b\d{1,3}\s*(s|sn|sec|second|seconds|saniye)\b)/i;
  const COUNTDOWN_ZERO_RE = /^0{1,2}:00$|^0\s*(s|sn|sec|second|seconds|saniye)$/i;

  // Splits a class/testid string into words on hyphens/underscores/whitespace and
  // camelCase boundaries, so "ad" can be matched as a whole word. This is what
  // makes the check below reject "loadTimer"/"threadIndicator"/"broadBreak"-style
  // unrelated UI (a plain substring match like [class*="adTimer" i] happily
  // matches those, since "ad" is just letters 3-4 of "load"/"thread"/"broad")
  // while still accepting "adTimer", "atvwebplayersdk-ad-timer", "myAdBreak", etc.
  function containsAdWord(str) {
    if (!str) return false;
    const tokens = str
      .split(/[-_\s]+|(?<=[a-z0-9])(?=[A-Z])/)
      .map((token) => token.toLowerCase());
    return tokens.some((token) => AD_WORD_TOKENS.has(token));
  }

  function isAdIndicatorActive(ind) {
    if (!ind || !document.body.contains(ind)) return false;
    const className = typeof ind.className === "string" ? ind.className : "";
    const testId = (ind.getAttribute && ind.getAttribute("data-testid")) || "";
    // The selector list that produces candidates for this function relies partly
    // on broad `[class*="ad..." i]`/`[data-testid*="ad-..." i]` substring
    // selectors (kept broad to catch Amazon renaming their specific classes).
    // Require "ad" to actually appear as a standalone word so unrelated
    // elements (a "loadTimer"/buffering spinner, a "threadIndicator", etc.)
    // that merely contain the letters "ad" don't get treated as real ad UI —
    // this was causing normal buffering/loading UI to trigger the ad shield
    // (mute + 16x speed + hidden video) on ordinary episode playback.
    if (!containsAdWord(className) && !containsAdWord(testId)) {
      return false;
    }
    // Use getBoundingClientRect + getComputedStyle rather than offsetParent/clientWidth:
    // offsetParent is null for position:fixed elements even when they're genuinely
    // visible, which previously caused false "hidden" results for such elements.
    // Note: intentionally does NOT check computed opacity — ensureAdShieldStyle()
    // cosmetically sets opacity:0 on these same selectors, which would otherwise make
    // every real ad indicator look "inactive" and disable ad detection entirely.
    const rect = ind.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) {
      return false;
    }
    const computed = window.getComputedStyle(ind);
    if (computed.display === "none" || computed.visibility === "hidden") {
      return false;
    }
    if (className.includes("atvwebplayersdk-element-off") || className.includes("atvwebplayersdk-visibility-hidden")) {
      return false;
    }
    const text = (ind.textContent || "").trim();
    if (!COUNTDOWN_TEXT_RE.test(text) || COUNTDOWN_ZERO_RE.test(text)) {
      return false;
    }
    return true;
  }

  function hideVideoForAd(video) {
    if (adHiddenVideo && adHiddenVideo !== video) {
      restoreHiddenVideos();
    }
    adHiddenVideo = video;
    video.dataset.pvscHidden = "1";
    video.style.opacity = "0";
  }

  // Restores every video element the shield ever hid, not just the one
  // findVideo() returns right now: Prime Video re-creates the <video> element on
  // some ad/content transitions, and restoring only the current element used to
  // leave the original one stuck at opacity:0 — a permanent black screen the
  // safety valve couldn't undo.
  function restoreHiddenVideos() {
    for (const el of document.querySelectorAll("video[data-pvsc-hidden]")) {
      el.style.opacity = "";
      delete el.dataset.pvscHidden;
    }
    if (adHiddenVideo) {
      adHiddenVideo.style.opacity = "";
      delete adHiddenVideo.dataset.pvscHidden;
      adHiddenVideo = null;
    }
  }

  function exitAdMode(video) {
    isAdCurrentlyActive = false;
    noAdStreak = 0;
    adModeStartedAt = 0;
    video.muted = wasMutedBeforeAd;
    restoreHiddenVideos();
    removeAdCover();
    video.playbackRate = speed;
    video.defaultPlaybackRate = speed;
    applySpeed();
  }

  function checkAndHandleAds() {
    ensureAdShieldStyle();
    const video = findVideo();
    if (!video) return;

    const skipButtons = document.querySelectorAll(AD_SKIP_BUTTON_SELECTOR);
    for (const btn of skipButtons) {
      if (document.body.contains(btn) && (btn.offsetParent !== null || btn.clientWidth > 0 || btn.clientHeight > 0 || btn.style.display !== "none")) {
        try { btn.click(); } catch {}
      }
    }

    const adIndicators = document.querySelectorAll(AD_INDICATOR_SELECTOR);
    let adDetected = false;
    for (const ind of adIndicators) {
      if (isAdIndicatorActive(ind)) {
        adDetected = true;
        break;
      }
    }

    if (adDetected) {
      noAdStreak = 0;
    }

    if (adDetected && !isAdCurrentlyActive && Date.now() >= adCooldownUntil) {
      isAdCurrentlyActive = true;
      adModeStartedAt = Date.now();
      wasMutedBeforeAd = video.muted;
      showAdCover(video);
      video.muted = true;
      hideVideoForAd(video);
      if (video.playbackRate !== 16) video.playbackRate = 16;
      if (video.defaultPlaybackRate !== 16) video.defaultPlaybackRate = 16;
      if (video.paused) {
        try { video.play(); } catch {}
      }
    }

    if (isAdCurrentlyActive && Date.now() - adModeStartedAt > AD_MAX_DURATION_MS) {
      adCooldownUntil = Date.now() + AD_COOLDOWN_AFTER_VALVE_MS;
      console.warn("[pvsc] Ad shield engaged for over " + AD_MAX_DURATION_MS / 1000 + "s — treating as stuck/false detection, restoring playback and suppressing the shield for " + AD_COOLDOWN_AFTER_VALVE_MS / 1000 + "s.");
      exitAdMode(video);
      return;
    }

    if (isAdCurrentlyActive && adDetected) {
      if (video.playbackRate !== 16) video.playbackRate = 16;
      if (video.defaultPlaybackRate !== 16) video.defaultPlaybackRate = 16;
      if (video.muted !== true) video.muted = true;
      if (video !== adHiddenVideo || video.style.opacity !== "0") hideVideoForAd(video);
      showAdCover(video);
      if (video.paused) {
        try { video.play(); } catch {}
      }
    } else if (isAdCurrentlyActive && !adDetected) {
      // Require a couple of consecutive negative ticks before declaring the ad
      // over, to avoid flicker if an indicator element briefly disappears during
      // Prime Video's own re-render at the ad/content boundary.
      noAdStreak += 1;
      if (noAdStreak >= AD_END_CONFIRM_TICKS) {
        exitAdMode(video);
      }
    }
  }

  function ensureSubtitleStyle() {
    let style = document.getElementById(SUBTITLE_STYLE_ID);
    if (!style) {
      style = document.createElement("style");
      style.id = SUBTITLE_STYLE_ID;
      document.documentElement.appendChild(style);
    }

    if (!subtitleEnabled) {
      style.textContent = "";
      return;
    }

    style.textContent = `
      video::cue {
        color: ${subtitleColor} !important;
        background-color: rgba(0, 0, 0, 0.78) !important;
        text-shadow: 0 2px 4px rgba(0, 0, 0, 0.95) !important;
      }
      .atvwebplayersdk-subtitle-text span,
      .atvwebplayersdk-captions-text span,
      [class*="subtitle" i] span,
      [class*="captions" i] span,
      .timedText span,
      [data-testid*="subtitle" i] span,
      [class*="Subtitle" i] span,
      .atvwebplayersdk-subtitle-container span,
      .atvwebplayersdk-captions-container span {
        color: ${subtitleColor} !important;
        text-shadow: 0 2px 5px rgba(0, 0, 0, 0.98), 0 0 2px rgba(0, 0, 0, 1) !important;
      }
    `;
  }

  function applySubtitleStyles() {
    if (!subtitleEnabled) {
      return;
    }
    ensureSubtitleStyle();

    const subtitleContainers = document.querySelectorAll(
      ".atvwebplayersdk-subtitle-text, .atvwebplayersdk-captions-text, .timedText, [class*='subtitle' i], [class*='captions' i], [data-testid*='subtitle' i]"
    );

    for (const container of subtitleContainers) {
      if (root.contains(container) || container.id === ROOT_ID) {
        continue;
      }
      const spans = container.querySelectorAll("span, div, p");
      for (const el of spans) {
        if (root.contains(el)) continue;
        const inlineColor = el.style.color;
        if (inlineColor && inlineColor !== subtitleColor) {
          el.style.setProperty("color", subtitleColor, "important");
          el.style.setProperty("text-shadow", "0 2px 5px rgba(0, 0, 0, 0.98), 0 0 2px rgba(0, 0, 0, 1)", "important");
        }
      }
      if (container instanceof HTMLElement) {
        const inlineColor = container.style.color;
        if (inlineColor && inlineColor !== subtitleColor) {
          container.style.setProperty("color", subtitleColor, "important");
        }
      }
    }
  }

  function updateSubtitleObserver() {
    // This observer drives both ad detection and subtitle restyling reactions to
    // DOM mutations, so it must stay connected regardless of subtitleEnabled —
    // otherwise disabling subtitles would silently degrade ad-detection latency
    // from "near-instant on mutation" to "up to 200ms" (the setInterval fallback),
    // an unintended coupling between two unrelated features. Only the subtitle
    // restyling *action* inside the callback is gated on subtitleEnabled below.
    const video = findVideo();
    const targetContainer = video
      ? (video.closest(".webPlayerSDKContainer, .atvwebplayersdk-player-container, [id*='player' i], #player, .player") || video.parentElement || document.body)
      : document.body;

    if (currentObservedContainer === targetContainer && subtitleObserver) {
      return;
    }

    if (subtitleObserver) {
      subtitleObserver.disconnect();
    } else {
      subtitleObserver = new MutationObserver((mutations) => {
        if (mutationThrottleTimer) return;
        let shouldApplySubtitles = false;
        let shouldCheckAds = false;
        for (const mutation of mutations) {
          if (mutation.type === "attributes" && (mutation.attributeName === "style" || mutation.attributeName === "class")) {
            const target = mutation.target;
            if (!root.contains(target) && target instanceof HTMLElement) {
              shouldApplySubtitles = true;
              shouldCheckAds = true;
              break;
            }
          } else if (mutation.type === "childList" && mutation.addedNodes.length > 0) {
            for (const node of mutation.addedNodes) {
              if (node instanceof HTMLElement && !root.contains(node)) {
                shouldApplySubtitles = true;
                shouldCheckAds = true;
                break;
              }
            }
            if (shouldApplySubtitles) break;
          }
        }
        if (shouldCheckAds || shouldApplySubtitles) {
          mutationThrottleTimer = window.setTimeout(() => {
            mutationThrottleTimer = 0;
            if (shouldCheckAds) checkAndHandleAds();
            if (shouldApplySubtitles && subtitleEnabled) applySubtitleStyles();
          }, 150);
        }
      });
    }

    currentObservedContainer = targetContainer;
    subtitleObserver.observe(targetContainer, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["style", "class"]
    });
  }

  function setSpeed(nextSpeed) {
    speed = Number(clamp(nextSpeed).toFixed(2));
    window.localStorage.setItem(STORAGE_KEY, String(speed));
    updateButtonDisplay();
    updateActivePreset();
    applySpeed();
  }

  function setSubtitleColor(nextColor) {
    subtitleColor = nextColor;
    window.localStorage.setItem(SUBTITLE_STORAGE_KEY, subtitleColor);
    if (!subtitleEnabled) {
      setSubtitleEnabled(true);
      return;
    }
    updateButtonDisplay();
    updateActivePreset();
    ensureSubtitleStyle();
    applySubtitleStyles();
  }

  function setSubtitleEnabled(enabled) {
    subtitleEnabled = Boolean(enabled);
    window.localStorage.setItem(SUBTITLE_ENABLED_KEY, String(subtitleEnabled));
    updateButtonDisplay();
    updateActivePreset();
    ensureSubtitleStyle();
    applySubtitleStyles();
    updateSubtitleObserver();
  }

  function ensureStyle() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }

    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = `
      #${ROOT_ID} {
        position: fixed;
        top: 76px;
        right: 18px;
        z-index: 2147483647;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        color: #f7f7f8;
        font: 650 13px/1.2 Arial, sans-serif;
        pointer-events: auto;
        transition: opacity 220ms ease;
      }
      #${ROOT_ID}.pvsc-hidden {
        opacity: 0;
        pointer-events: none;
      }
      #${ROOT_ID}.pvsc-no-video {
        display: none;
      }
      .pvsc-wrap {
        position: relative;
        display: inline-flex;
        align-items: center;
      }
      .pvsc-speed-button {
        min-width: 54px;
        height: 38px;
        padding: 0 12px;
        color: #f7f7f8;
        font: inherit;
        cursor: grab;
        background: rgba(28, 31, 38, 0.86);
        border: 1px solid rgba(255, 255, 255, 0.24);
        border-radius: 10px;
        box-shadow: 0 6px 18px rgba(0, 0, 0, 0.32);
        user-select: none;
        backdrop-filter: blur(12px);
        display: inline-flex;
        align-items: center;
        justify-content: center;
      }
      .pvsc-speed-button:active {
        cursor: grabbing;
      }
      .pvsc-menu {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        display: none;
        flex-direction: column;
        gap: 10px;
        width: 196px;
        padding: 12px;
        background: rgba(20, 22, 28, 0.95);
        border: 1px solid rgba(255, 255, 255, 0.18);
        border-radius: 14px;
        box-shadow: 0 16px 36px rgba(0, 0, 0, 0.5);
        backdrop-filter: blur(14px);
      }
      #${ROOT_ID}.pvsc-menu-open .pvsc-menu {
        display: flex;
      }
      .pvsc-section-title {
        font-size: 11px;
        font-weight: 700;
        letter-spacing: 0.6px;
        text-transform: uppercase;
        color: rgba(255, 255, 255, 0.58);
        margin-bottom: 2px;
      }
      .pvsc-speed-grid {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: 6px;
      }
      .pvsc-menu button {
        width: 100%;
        height: 34px;
        padding: 0 8px;
        color: #f7f7f8;
        font: inherit;
        cursor: pointer;
        background: rgba(255, 255, 255, 0.08);
        border: 1px solid rgba(255, 255, 255, 0.14);
        border-radius: 8px;
        user-select: none;
        transition: background 120ms ease, border-color 120ms ease;
      }
      .pvsc-speed-button:hover,
      .pvsc-speed-button:focus,
      .pvsc-menu button:hover,
      .pvsc-menu button:focus {
        background: rgba(255, 255, 255, 0.18);
        outline: none;
      }
      .pvsc-menu .pvsc-step {
        font-size: 17px;
        font-weight: 700;
      }
      .pvsc-menu .pvsc-active {
        color: #ffffff;
        border-color: rgba(99, 179, 237, 0.72);
        background: rgba(46, 118, 211, 0.62);
      }
      .pvsc-divider {
        height: 1px;
        background: rgba(255, 255, 255, 0.12);
        margin: 2px 0;
      }
      .pvsc-subtitle-toggle {
        display: flex;
        align-items: center;
        justify-content: space-between;
        width: 100%;
        height: 34px;
        padding: 0 10px;
        color: #f7f7f8;
        font: inherit;
        font-size: 12px;
        cursor: pointer;
        background: rgba(255, 255, 255, 0.08);
        border: 1px solid rgba(255, 255, 255, 0.14);
        border-radius: 8px;
        user-select: none;
      }
      .pvsc-subtitle-toggle.pvsc-toggle-on {
        background: rgba(46, 184, 114, 0.28);
        border-color: rgba(46, 184, 114, 0.65);
        color: #ffffff;
      }
      .pvsc-color-grid {
        display: grid;
        grid-template-columns: repeat(5, 1fr);
        gap: 8px;
        padding-top: 2px;
      }
      .pvsc-color-swatch {
        width: 28px !important;
        height: 28px !important;
        padding: 0 !important;
        border-radius: 50% !important;
        cursor: pointer;
        border: 2px solid rgba(255, 255, 255, 0.28) !important;
        position: relative;
      }
      .pvsc-color-swatch:hover {
        transform: scale(1.08);
      }
      .pvsc-color-swatch.pvsc-swatch-active {
        border-color: #ffffff !important;
        box-shadow: 0 0 0 2px #3182ce, 0 0 8px rgba(255, 255, 255, 0.6);
      }
    `;
    document.documentElement.appendChild(style);
  }

  function makeMenuButton(text, className, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = className;
    button.textContent = text;
    button.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      onClick();
      showControls();
    });
    return button;
  }

  function isVisible(element) {
    const rect = element.getBoundingClientRect();
    const style = window.getComputedStyle(element);
    return rect.width > 12 && rect.height > 12 && style.visibility !== "hidden" && style.display !== "none";
  }

  function findCloseButton() {
    const candidates = Array.from(document.querySelectorAll("button, [role='button'], [aria-label], [title]"));
    let fallback = null;

    for (const element of candidates) {
      if (element === root || root.contains(element) || !isVisible(element)) {
        continue;
      }

      const rect = element.getBoundingClientRect();
      if (rect.top < 32 || rect.top > 150 || rect.left < window.innerWidth * 0.55) {
        continue;
      }

      const label = [
        element.getAttribute("aria-label"),
        element.getAttribute("title"),
        element.textContent,
      ].filter(Boolean).join(" ").toLowerCase();

      if (/(close|exit|dismiss|kapat|çık|cikis|çikiş|x\b)/i.test(label)) {
        return element;
      }

      if (!fallback || rect.right > fallback.getBoundingClientRect().right) {
        fallback = element;
      }
    }

    return fallback;
  }

  function readSavedPosition() {
    try {
      const position = JSON.parse(window.localStorage.getItem(POSITION_KEY) || "null");
      if (position && Number.isFinite(position.left) && Number.isFinite(position.top)) {
        return position;
      }
    } catch {
      return null;
    }

    return null;
  }

  function clampPosition(left, top) {
    const rect = root.getBoundingClientRect();
    const width = rect.width || 54;
    const height = rect.height || 38;

    return {
      left: Math.min(Math.max(8, left), window.innerWidth - width - 8),
      top: Math.min(Math.max(8, top), window.innerHeight - height - 8),
    };
  }

  function setPosition(left, top, save) {
    const position = clampPosition(left, top);
    root.style.left = `${position.left}px`;
    root.style.top = `${position.top}px`;
    root.style.right = "auto";

    if (save) {
      window.localStorage.setItem(POSITION_KEY, JSON.stringify(position));
    }
  }

  function placeNearCloseButton(closeButton) {
    if (readSavedPosition()) {
      return;
    }

    const closeRect = closeButton.getBoundingClientRect();
    const rootRect = root.getBoundingClientRect();
    const width = rootRect.width || 54;
    const height = rootRect.height || 38;
    let left = closeRect.right + 10;

    if (left + width > window.innerWidth - 8) {
      left = closeRect.left - width - 10;
    }

    setPosition(left, closeRect.top + (closeRect.height - height) / 2, false);
  }

  function refresh() {
    ensureAdShieldStyle();
    checkAndHandleAds();

    const video = findVideo();
    if (!video) {
      root.classList.add("pvsc-no-video");
      setMenuOpen(false);
      return;
    }

    root.classList.remove("pvsc-no-video");
    attachVideoListeners(video);

    const closeButton = findCloseButton();
    if (closeButton) {
      placeNearCloseButton(closeButton);
    } else if (!readSavedPosition()) {
      setPosition(window.innerWidth - 76, 78, false);
    }

    updateSubtitleObserver();
    applySubtitleStyles();
  }

  function updateActivePreset() {
    for (const button of menu.querySelectorAll("[data-speed]")) {
      const value = Number(button.getAttribute("data-speed"));
      button.classList.toggle("pvsc-active", Math.abs(value - speed) < 0.001);
    }

    if (subtitleToggleBtn) {
      subtitleToggleBtn.classList.toggle("pvsc-toggle-on", subtitleEnabled);
      subtitleToggleBtn.textContent = subtitleEnabled ? "Altyazı: Açık ✓" : "Altyazı: Kapalı";
    }

    for (const swatch of menu.querySelectorAll("[data-color]")) {
      const colorVal = swatch.getAttribute("data-color");
      swatch.classList.toggle("pvsc-swatch-active", subtitleEnabled && colorVal.toLowerCase() === subtitleColor.toLowerCase());
    }
  }

  function showControls() {
    const video = findVideo();
    if (!video) {
      root.classList.add("pvsc-no-video");
      return;
    }

    attachVideoListeners(video);
    root.classList.remove("pvsc-no-video");
    root.classList.remove("pvsc-hidden");
    window.clearTimeout(hideTimer);

    if (!video.paused && !isMenuOpen && !isDragging) {
      hideTimer = window.setTimeout(() => {
        const currentVideo = findVideo();
        if (currentVideo && !currentVideo.paused && !isMenuOpen && !isDragging) {
          root.classList.add("pvsc-hidden");
        }
      }, 2000);
    }
  }

  function setMenuOpen(open) {
    isMenuOpen = open;
    root.classList.toggle("pvsc-menu-open", open);

    if (open) {
      root.classList.remove("pvsc-hidden");
      window.clearTimeout(hideTimer);
    } else {
      showControls();
    }
  }

  ensureStyle();
  ensureSubtitleStyle();
  ensureAdShieldStyle();

  const existingRoot = document.getElementById(ROOT_ID);
  if (existingRoot) {
    existingRoot.remove();
  }

  const root = document.createElement("div");
  root.id = ROOT_ID;
  root.className = "pvsc-no-video";
  root.setAttribute("aria-label", "Prime Video speed & subtitle control");

  const wrap = document.createElement("div");
  wrap.className = "pvsc-wrap";

  const speedButton = document.createElement("button");
  speedButton.type = "button";
  speedButton.className = "pvsc-speed-button";
  speedButton.title = "Playback speed & subtitle control";

  const menu = document.createElement("div");
  menu.className = "pvsc-menu";

  const speedTitle = document.createElement("div");
  speedTitle.className = "pvsc-section-title";
  speedTitle.textContent = "⚡ Hız Kontrolü";
  menu.appendChild(speedTitle);

  const speedGrid = document.createElement("div");
  speedGrid.className = "pvsc-speed-grid";
  speedGrid.appendChild(makeMenuButton("-", "pvsc-step", () => setSpeed(speed - STEP)));
  speedGrid.appendChild(makeMenuButton("+", "pvsc-step", () => setSpeed(speed + STEP)));

  for (const preset of PRESET_SPEEDS) {
    const presetButton = makeMenuButton(format(preset), "", () => {
      setSpeed(preset);
      setMenuOpen(false);
    });
    presetButton.setAttribute("data-speed", String(preset));
    speedGrid.appendChild(presetButton);
  }
  menu.appendChild(speedGrid);

  const divider = document.createElement("div");
  divider.className = "pvsc-divider";
  menu.appendChild(divider);

  const subtitleTitle = document.createElement("div");
  subtitleTitle.className = "pvsc-section-title";
  subtitleTitle.textContent = "💬 Altyazı Rengi";
  menu.appendChild(subtitleTitle);

  const subtitleToggleBtn = document.createElement("button");
  subtitleToggleBtn.type = "button";
  subtitleToggleBtn.className = "pvsc-subtitle-toggle";
  subtitleToggleBtn.textContent = subtitleEnabled ? "Altyazı: Açık ✓" : "Altyazı: Kapalı";
  subtitleToggleBtn.addEventListener("click", (event) => {
    event.preventDefault();
    event.stopPropagation();
    setSubtitleEnabled(!subtitleEnabled);
    showControls();
  });
  menu.appendChild(subtitleToggleBtn);

  const colorGrid = document.createElement("div");
  colorGrid.className = "pvsc-color-grid";
  for (const presetColor of PRESET_COLORS) {
    const swatch = document.createElement("button");
    swatch.type = "button";
    swatch.className = "pvsc-color-swatch";
    swatch.title = presetColor.name;
    swatch.style.backgroundColor = presetColor.hex;
    swatch.setAttribute("data-color", presetColor.hex);
    swatch.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      setSubtitleColor(presetColor.hex);
      showControls();
    });
    colorGrid.appendChild(swatch);
  }
  menu.appendChild(colorGrid);

  wrap.append(speedButton, menu);
  root.appendChild(wrap);
  document.documentElement.appendChild(root);

  const savedPosition = readSavedPosition();
  if (savedPosition) {
    setPosition(savedPosition.left, savedPosition.top, false);
  }

  updateButtonDisplay();

  speedButton.addEventListener("pointerdown", (event) => {
    if (event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    const rect = root.getBoundingClientRect();
    isDragging = true;
    dragStarted = false;
    dragOffsetX = event.clientX - rect.left;
    dragOffsetY = event.clientY - rect.top;
    lastPointerX = event.clientX;
    lastPointerY = event.clientY;
    speedButton.setPointerCapture(event.pointerId);
  });

  speedButton.addEventListener("pointermove", (event) => {
    if (!isDragging) {
      return;
    }

    if (Math.abs(event.clientX - lastPointerX) > 3 || Math.abs(event.clientY - lastPointerY) > 3) {
      dragStarted = true;
    }

    if (dragStarted) {
      event.preventDefault();
      setPosition(event.clientX - dragOffsetX, event.clientY - dragOffsetY, false);
    }
  });

  speedButton.addEventListener("pointerup", (event) => {
    if (!isDragging) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    isDragging = false;
    speedButton.releasePointerCapture(event.pointerId);

    if (dragStarted) {
      const rect = root.getBoundingClientRect();
      setPosition(rect.left, rect.top, true);
      dragStarted = false;
      return;
    }

    setMenuOpen(!isMenuOpen);
  });

  root.addEventListener("click", (event) => event.stopPropagation());
  document.addEventListener("pointerdown", (event) => {
    if (!root.contains(event.target)) {
      setMenuOpen(false);
    }
  });
  document.addEventListener("mousemove", showControls, { passive: true });
  document.addEventListener("touchstart", showControls, { passive: true });

  document.addEventListener("keydown", (event) => {
    const target = event.target;
    const tagName = target instanceof HTMLElement ? target.tagName.toLowerCase() : "";
    const isTyping = target instanceof HTMLElement
      && (target.isContentEditable || tagName === "input" || tagName === "textarea" || tagName === "select");
    if (event.defaultPrevented || isTyping) {
      return;
    }

    if ((event.altKey || event.shiftKey) && (event.key === "c" || event.key === "C" || event.key === "ç" || event.key === "Ç")) {
      event.preventDefault();
      setSubtitleEnabled(!subtitleEnabled);
      showControls();
      return;
    }

    if (event.key === "]") {
      setSpeed(speed + STEP);
      showControls();
    } else if (event.key === "[") {
      setSpeed(speed - STEP);
      showControls();
    } else if (event.key === "\\") {
      setSpeed(DEFAULT_SPEED);
      showControls();
    } else if (event.key === "Escape") {
      setMenuOpen(false);
    }
  }, true);

  window.addEventListener("resize", () => {
    const rect = root.getBoundingClientRect();
    setPosition(rect.left, rect.top, Boolean(readSavedPosition()));
    refresh();
  });
  window.setInterval(applySpeed, 200);
  window.setInterval(checkAndHandleAds, 200);
  window.setInterval(refresh, 500);

  updateActivePreset();
  applySpeed();
  refresh();

  window.__primeVideoSpeedControl = {
    installed: true,
    version: "3.1.0",
    applySpeed,
    refresh,
    applySubtitleStyles,
    checkAndHandleAds,
  };

  return "installed";
})();
