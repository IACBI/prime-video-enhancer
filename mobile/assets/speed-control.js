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
  const SUBTITLE_SIZE_KEY = "primeVideoSpeedControl.subtitleSize";
  const SUBTITLE_BG_KEY = "primeVideoSpeedControl.subtitleBg";
  const ADS_BLOCKED_KEY = "primeVideoSpeedControl.adsBlockedCount";
  const ADS_SAVED_SEC_KEY = "primeVideoSpeedControl.adsTimeSavedSecs";
  const SUBTITLE_TEXT_SELECTOR = [
    ".atvwebplayersdk-subtitle-text",
    ".atvwebplayersdk-captions-text",
    ".timedText"
  ].join(", ");
  
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
  // shield has been continuously engaged (muted, hyper-speed, video hidden) for longer
  // than this, it's almost certainly a stuck or false detection rather than a
  // real ad, so playback is forcibly handed back to the user instead of racing
  // through the rest of the episode at ad speed behind a black cover.
  const AD_MAX_DURATION_MS = 45000;

  // After the safety valve force-exits ad mode, suppress re-engaging the visual
  // shield for this long. Without a cooldown, a persistently-matching element
  // (a stuck indicator, or UI we misclassify) re-engages ad mode on the very
  // next 200ms tick after the valve fires, turning one false positive into an
  // endless loop of black-screen/hyper-speed windows. Skip-button clicking and the
  // network-level blocker stay active during the cooldown, so real ads are
  // still handled — only the mute/hide/hyper-speed shield is suppressed.
  const AD_COOLDOWN_AFTER_VALVE_MS = 120000;

  const AUTO_SKIP_SELECTOR = [
    ".atvwebplayersdk-skip-button",
    ".atvwebplayersdk-next-episode",
    "[class*='skip-intro' i]",
    "[class*='skipIntro' i]",
    "[class*='next-episode' i]",
    "[class*='nextEpisode' i]",
    "[class*='skip-recap' i]",
    "[class*='skipRecap' i]",
    "[data-testid*='skip-intro' i]",
    "[data-testid*='next-episode' i]"
  ].join(", ");

  const AD_SKIP_BUTTON_SELECTOR =
    ".atvwebplayersdk-ad-skip-button, [class*='adSkipButton' i], [class*='ad-skip-button' i], [aria-label*='skip ad' i], [aria-label*='reklamı atla' i], [aria-label*='reklamı geç' i], button[title*='skip' i], button[title*='atla' i], [data-testid*='skip' i], div[class*='ad-skip' i]";

  // Note: .atvwebplayersdk-ad-resume-message is intentionally excluded here. It
  // appears once the ad break has ENDED and real content is resuming, so treating
  // it as an "ad still active" signal (as before) kept the video muted/ad-speed/
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

  const previousControl = window.__primeVideoSpeedControl;
  if (previousControl?.installed) {
    if (previousControl.version === "3.6.1") {
      previousControl.refresh();
      previousControl.applySpeed();
      previousControl.applySubtitleStyles();
      previousControl.checkAndHandleAds();
      return "already-installed";
    }
    previousControl.destroy?.();
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
  let subtitleSize = window.localStorage.getItem(SUBTITLE_SIZE_KEY) || "150%";
  let subtitleBg = window.localStorage.getItem(SUBTITLE_BG_KEY) || "shadow"; // transparent, shadow, solid
  
  let adsBlockedCount = parseInt(window.localStorage.getItem(ADS_BLOCKED_KEY) || "0", 10);
  let adsTimeSavedSecs = parseInt(window.localStorage.getItem(ADS_SAVED_SEC_KEY) || "0", 10);

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
  let maintenanceTimer = 0;
  let refreshTimer = 0;
  const lifecycleController = new AbortController();
  const lifecycleSignal = lifecycleController.signal;
  const styledSubtitleElements = new Map();

  const TARGET_AD_SPEED = 30;
  const FALLBACK_AD_SPEED = 16;
  let currentAdSpeed = TARGET_AD_SPEED;

  let isAdCurrentlyActive = false;
  let wasMutedBeforeAd = false;
  let noAdStreak = 0;
  let adModeStartedAt = 0;
  let adCooldownUntil = 0;
  let adHiddenVideo = null;
  const handledAutoSkipButtons = new WeakSet();
  const handledAdSkipButtons = new WeakSet();

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

  function updateStatsDisplay() {
    const statsEl = document.getElementById("pvsc-stats-text");
    if (statsEl) {
      statsEl.textContent = `🛡️ ${adsBlockedCount} ads blocked (~${Math.floor(adsTimeSavedSecs / 60)}m saved)`;
    }
  }

  function incrementAdStats(count, secs) {
    adsBlockedCount += count;
    adsTimeSavedSecs += secs;
    window.localStorage.setItem(ADS_BLOCKED_KEY, String(adsBlockedCount));
    window.localStorage.setItem(ADS_SAVED_SEC_KEY, String(adsTimeSavedSecs));
    updateStatsDisplay();
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

  function handleAdStall() {
    if (isAdCurrentlyActive && currentAdSpeed > FALLBACK_AD_SPEED) {
      console.warn("[pvsc] Ad playback stalled at 30x, falling back to 16x");
      currentAdSpeed = FALLBACK_AD_SPEED;
      handleVideoPlaybackState();
    }
  }

  function handleVideoPlaybackState() {
    if (!attachedVideo) return;
    if (isAdCurrentlyActive) {
      if (attachedVideo.playbackRate !== currentAdSpeed) attachedVideo.playbackRate = currentAdSpeed;
      if (attachedVideo.defaultPlaybackRate !== currentAdSpeed) attachedVideo.defaultPlaybackRate = currentAdSpeed;
      if (attachedVideo.muted !== true) attachedVideo.muted = true;
    } else {
      if (attachedVideo.playbackRate !== speed) attachedVideo.playbackRate = speed;
      if (attachedVideo.defaultPlaybackRate !== speed) attachedVideo.defaultPlaybackRate = speed;
    }
  }

  function detachVideoListeners(video) {
    if (!video) return;
    video.removeEventListener("play", showControls);
    video.removeEventListener("playing", showControls);
    video.removeEventListener("pause", showControls);
    video.removeEventListener("seeked", showControls);
    video.removeEventListener("ratechange", handleVideoPlaybackState);
    video.removeEventListener("play", handleVideoPlaybackState);
    video.removeEventListener("playing", handleVideoPlaybackState);
    video.removeEventListener("timeupdate", handleVideoPlaybackState);
    video.removeEventListener("waiting", handleAdStall);
    video.removeEventListener("stalled", handleAdStall);
  }

  function attachVideoListeners(video) {
    if (!video || video === attachedVideo) {
      return;
    }
    if (attachedVideo) {
      detachVideoListeners(attachedVideo);
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
    attachedVideo.addEventListener("waiting", handleAdStall, { passive: true });
    attachedVideo.addEventListener("stalled", handleAdStall, { passive: true });
    showControls();
  }

  function applySpeed(video = findVideo()) {
    if (!video) {
      return;
    }
    attachVideoListeners(video);

    if (isAdCurrentlyActive) {
      if (video.playbackRate !== currentAdSpeed) video.playbackRate = currentAdSpeed;
      if (video.defaultPlaybackRate !== currentAdSpeed) video.defaultPlaybackRate = currentAdSpeed;
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
  // playing at hyper-speed underneath. A plain opaque cover element can't fail this way:
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
  // racing at ad speed underneath.
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
    // (mute + ad speed + hidden video) on ordinary episode playback.
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
    if (isAdCurrentlyActive && adModeStartedAt > 0) {
      const timeSpentAtAdSpeed = Date.now() - adModeStartedAt;
      const effectiveMultiplier = Math.max(1, currentAdSpeed - 1);
      const savedSecs = Math.max(1, Math.floor((timeSpentAtAdSpeed * effectiveMultiplier) / 1000));
      incrementAdStats(1, savedSecs);
    }
    isAdCurrentlyActive = false;
    currentAdSpeed = TARGET_AD_SPEED;
    noAdStreak = 0;
    adModeStartedAt = 0;
    video.muted = wasMutedBeforeAd;
    restoreHiddenVideos();
    removeAdCover();
    video.playbackRate = speed;
    video.defaultPlaybackRate = speed;
    applySpeed();
  }

  function checkAndHandleAds(video = findVideo()) {
    ensureAdShieldStyle();
    if (!video) return;

    // Auto-Skip feature
    const autoSkipButtons = document.querySelectorAll(AUTO_SKIP_SELECTOR);
    for (const btn of autoSkipButtons) {
      if (!handledAutoSkipButtons.has(btn) && document.body.contains(btn) && isVisible(btn)) {
        try {
          btn.click();
          handledAutoSkipButtons.add(btn);
          console.log("[pvsc] Auto-skipped intro/outro!");
        } catch {}
      }
    }

    const skipButtons = document.querySelectorAll(AD_SKIP_BUTTON_SELECTOR);
    for (const btn of skipButtons) {
      if (btn.matches(AUTO_SKIP_SELECTOR)) continue;
      if (!handledAdSkipButtons.has(btn) && document.body.contains(btn) && isVisible(btn)) {
        try {
          btn.click();
          handledAdSkipButtons.add(btn);
          incrementAdStats(1, 15);
        } catch {}
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
      currentAdSpeed = TARGET_AD_SPEED;
      adModeStartedAt = Date.now();
      wasMutedBeforeAd = video.muted;
      showAdCover(video);
      video.muted = true;
      hideVideoForAd(video);
      if (video.playbackRate !== currentAdSpeed) video.playbackRate = currentAdSpeed;
      if (video.defaultPlaybackRate !== currentAdSpeed) video.defaultPlaybackRate = currentAdSpeed;
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
      if (video.playbackRate !== currentAdSpeed) video.playbackRate = currentAdSpeed;
      if (video.defaultPlaybackRate !== currentAdSpeed) video.defaultPlaybackRate = currentAdSpeed;
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
    
    let bgCss = "";
    if (subtitleBg === "transparent") {
      bgCss = "background-color: transparent !important;";
    } else if (subtitleBg === "solid") {
      bgCss = "background-color: rgba(0, 0, 0, 0.9) !important;";
    } else {
      // shadow (default)
      bgCss = "background-color: rgba(0, 0, 0, 0.45) !important;";
    }

    // Use vh-based size for ::cue so it doesn't compound with Amazon's own
    // container font-size scaling.  Shadow offsets scale proportionally.
    const cueSizeVh = computeSubtitleSizeVh();
    const cueShadow = computeScaledShadow();

    style.textContent = `
      video::cue {
        color: ${subtitleColor} !important;
        font-size: ${cueSizeVh} !important;
        font-weight: 700 !important;
        line-height: 1.35 !important;
        ${bgCss}
        text-shadow: ${cueShadow} !important;
      }
    `;
  }

  const SUBTITLE_STYLE_PROPERTIES = [
    "color",
    "font-size",
    "font-weight",
    "line-height",
    "background-color",
    "text-shadow",
    "padding",
    "border-radius",
    "-webkit-box-decoration-break",
    "box-decoration-break"
  ];

  function rememberSubtitleStyles(element) {
    if (styledSubtitleElements.has(element)) return;
    const originalStyles = new Map();
    for (const property of SUBTITLE_STYLE_PROPERTIES) {
      originalStyles.set(property, {
        value: element.style.getPropertyValue(property),
        priority: element.style.getPropertyPriority(property)
      });
    }
    styledSubtitleElements.set(element, originalStyles);
  }

  function restoreSubtitleElement(element) {
    const originalStyles = styledSubtitleElements.get(element);
    if (!originalStyles) return;
    for (const [property, original] of originalStyles) {
      if (original.value) {
        element.style.setProperty(property, original.value, original.priority);
      } else {
        element.style.removeProperty(property);
      }
    }
    styledSubtitleElements.delete(element);
  }

  function restoreInactiveSubtitleElements(activeElements = new Set()) {
    for (const element of [...styledSubtitleElements.keys()]) {
      if (!element.isConnected || !activeElements.has(element)) {
        restoreSubtitleElement(element);
      }
    }
  }

  function findSubtitleTextElements(video) {
    if (!video) return new Set();
    const videoRect = video.getBoundingClientRect();
    if (videoRect.width <= 0 || videoRect.height <= 0) return new Set();

    const minimumSubtitleTop = videoRect.top + videoRect.height * 0.45;
    const maximumSubtitleHeight = videoRect.height * 0.3;
    const elements = new Set();
    const candidateRoots = document.querySelectorAll(SUBTITLE_TEXT_SELECTOR);

    for (const candidateRoot of candidateRoots) {
      if (!(candidateRoot instanceof HTMLElement) || root.contains(candidateRoot)) continue;
      const candidates = [candidateRoot, ...candidateRoot.querySelectorAll("span, div, p")];
      for (const candidate of candidates) {
        if (!(candidate instanceof HTMLElement)) continue;
        const text = candidate.textContent?.trim();
        if (!text) continue;

        const hasTextChild = [...candidate.children].some(child => child.textContent?.trim());
        if (hasTextChild) continue;

        const rect = candidate.getBoundingClientRect();
        const isInsideSubtitleRegion =
          rect.width > 0 &&
          rect.height > 0 &&
          rect.height <= maximumSubtitleHeight &&
          rect.top >= minimumSubtitleTop &&
          rect.bottom <= videoRect.bottom + 4 &&
          rect.right >= videoRect.left &&
          rect.left <= videoRect.right;
        if (isInsideSubtitleRegion) {
          elements.add(candidate);
        }
      }
    }

    return elements;
  }

  function clearLegacySubtitleOverrides() {
    const candidateRoots = document.querySelectorAll(SUBTITLE_TEXT_SELECTOR);
    for (const rootEl of candidateRoots) {
      if (!(rootEl instanceof HTMLElement) || root.contains(rootEl)) continue;
      // Strip background-color on parent container when it has styled text children,
      // preventing nested/double background artifacts.
      if (rootEl.firstElementChild) {
        rootEl.style.setProperty("background-color", "transparent", "important");
      }
    }
  }

  // Converts the user's percentage setting into a vh-based CSS value.
  // Baseline: 100% setting = 1.85vh ≈ 20px on a 1080p screen.
  // 150% = 2.78vh ≈ 30px, 200% = 3.70vh ≈ 40px.
  // Clamped to [1.0vh, 4.2vh] so subtitles are always readable and never block the screen.
  const SUBTITLE_VH_BASE = 1.85;  // vh per 100% setting
  const SUBTITLE_VH_MIN  = 1.0;
  const SUBTITLE_VH_MAX  = 7.5;

  function computeSubtitleSizeVh() {
    const pct = parseInt(subtitleSize, 10);
    if (!Number.isFinite(pct) || pct <= 0) return SUBTITLE_VH_BASE + "vh";
    const raw = (SUBTITLE_VH_BASE * pct) / 100;
    const clamped = Math.min(SUBTITLE_VH_MAX, Math.max(SUBTITLE_VH_MIN, raw));
    return clamped.toFixed(2) + "vh";
  }

  // Returns a text-shadow CSS value whose offsets/blur scale proportionally
  // with the computed font-size so the shadow looks clean and sharp at any scale.
  function computeScaledShadow() {
    const pct = parseInt(subtitleSize, 10) || 100;
    const scale = Math.max(0.5, pct / 100);
    if (subtitleBg === "transparent") {
      const y = (2.5 * scale).toFixed(1);
      const blur = (4 * scale).toFixed(1);
      return `-1px -1px 0 #000, 1px -1px 0 #000, -1px 1px 0 #000, 1px 1px 0 #000, 0 ${y}px ${blur}px rgba(0,0,0,0.9)`;
    }
    const y = (1.5 * scale).toFixed(1);
    const blur = (3 * scale).toFixed(1);
    return `0 ${y}px ${blur}px rgba(0, 0, 0, 0.8)`;
  }

  function applySubtitleStyles(video = findVideo()) {
    ensureSubtitleStyle();
    if (!subtitleEnabled) {
      restoreInactiveSubtitleElements();
      return;
    }

    clearLegacySubtitleOverrides();

    // Resolve the bg value we want to set inline.
    // Inline styles always beat CSS rules regardless of specificity, so this is
    // the only reliable way to override Prime Video's own bg injections.
    let bgValue;
    if (subtitleBg === "solid") {
      bgValue = "rgba(0, 0, 0, 0.92)";
    } else if (subtitleBg === "transparent") {
      bgValue = "transparent";
    } else {
      // shadow (default)
      bgValue = "rgba(0, 0, 0, 0.48)";
    }

    const subtitleElements = findSubtitleTextElements(video);
    restoreInactiveSubtitleElements(subtitleElements);

    const shadowValue = computeScaledShadow();
    const inlineFontSize = computeSubtitleSizeVh();

    for (const element of subtitleElements) {
      rememberSubtitleStyles(element);
      element.style.setProperty("color", subtitleColor, "important");
      element.style.setProperty("font-size", inlineFontSize, "important");
      element.style.setProperty("font-weight", "700", "important");
      element.style.setProperty("line-height", "1.35", "important");
      element.style.setProperty("background-color", bgValue, "important");
      element.style.setProperty("text-shadow", shadowValue, "important");
      if (subtitleBg !== "transparent") {
        element.style.setProperty("padding", "0.12em 0.35em", "important");
        element.style.setProperty("border-radius", "4px", "important");
        element.style.setProperty("-webkit-box-decoration-break", "clone", "important");
        element.style.setProperty("box-decoration-break", "clone", "important");
      } else {
        element.style.removeProperty("padding");
        element.style.removeProperty("border-radius");
        element.style.removeProperty("-webkit-box-decoration-break");
        element.style.removeProperty("box-decoration-break");
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
        let hasNewSubtitleElement = false;
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
                // Fast-path: detect subtitle element additions immediately to avoid flash.
                // When Prime Video injects a new subtitle span, apply our styles
                // synchronously before the browser paints — no 50 ms debounce delay.
                if (subtitleEnabled && (
                  node.matches(SUBTITLE_TEXT_SELECTOR) ||
                  node.querySelector(SUBTITLE_TEXT_SELECTOR) !== null
                )) {
                  hasNewSubtitleElement = true;
                }
                shouldApplySubtitles = true;
                shouldCheckAds = true;
                break;
              }
            }
            if (shouldApplySubtitles) break;
          }
        }
        // Apply subtitle styles immediately when a new subtitle element appears.
        // This runs synchronously in the MutationObserver microtask, before the
        // next paint, so Amazon's default color/size is never visible to the user.
        if (hasNewSubtitleElement) {
          applySubtitleStyles();
        }
        if (shouldCheckAds || shouldApplySubtitles) {
          mutationThrottleTimer = window.setTimeout(() => {
            mutationThrottleTimer = 0;
            if (shouldCheckAds) checkAndHandleAds();
            // Skip the deferred subtitle pass if the fast-path already handled it.
            if (shouldApplySubtitles && subtitleEnabled && !hasNewSubtitleElement) applySubtitleStyles();
          }, 50);
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

  function setSubtitleSize(val) {
    const pct = parseInt(val, 10);
    if (!Number.isFinite(pct) || pct < 50 || pct > 400) return;
    subtitleSize = pct + "%";
    window.localStorage.setItem(SUBTITLE_SIZE_KEY, subtitleSize);
    ensureSubtitleStyle();
    applySubtitleStyles();
    // Sync the input element if visible
    const inp = document.getElementById("pvsc-size-input");
    if (inp && inp.value !== String(pct)) inp.value = String(pct);
    updateActivePreset();
  }

  function cycleSubtitleBg() {
    if (subtitleBg === "shadow") subtitleBg = "solid";
    else if (subtitleBg === "solid") subtitleBg = "transparent";
    else subtitleBg = "shadow";
    window.localStorage.setItem(SUBTITLE_BG_KEY, subtitleBg);
    ensureSubtitleStyle();
    applySubtitleStyles();
    updateActivePreset();
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
        background: transparent;
        border: 1px solid transparent;
        border-radius: 10px;
        box-shadow: none;
        user-select: none;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        text-shadow: 0 2px 4px rgba(0, 0, 0, 0.8), 0 0 2px rgba(0, 0, 0, 1);
        transition: background 200ms ease, border-color 200ms ease, box-shadow 200ms ease;
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
      .pvsc-speed-button:focus {
        background: rgba(25, 25, 25, 0.6);
        border-color: rgba(255, 255, 255, 0.2);
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
        backdrop-filter: blur(8px);
        outline: none;
      }
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
      .pvsc-stats-row {
        font-size: 10px;
        text-align: center;
        color: rgba(255, 255, 255, 0.4);
        padding-top: 4px;
        margin-top: 2px;
      }
      .pvsc-subtitle-advanced-grid {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: 6px;
        margin-top: 4px;
      }
      @media (max-width: 768px) {
        .pvsc-menu {
          position: fixed !important;
          bottom: 0 !important;
          top: auto !important;
          left: 0 !important;
          right: 0 !important;
          width: 100% !important;
          max-width: 100% !important;
          border-radius: 20px 20px 0 0 !important;
          border-bottom: none !important;
          padding: 16px 20px max(20px, env(safe-area-inset-bottom)) 20px !important;
          box-shadow: 0 -8px 36px rgba(0, 0, 0, 0.75) !important;
          z-index: 2147483647 !important;
        }
        .pvsc-speed-grid {
          grid-template-columns: repeat(4, 1fr) !important;
        }
        .pvsc-subtitle-advanced-grid {
          grid-template-columns: repeat(2, 1fr) !important;
        }
        .pvsc-menu button {
          height: 42px !important;
          font-size: 14px !important;
        }
        .pvsc-color-swatch {
          width: 34px !important;
          height: 34px !important;
        }
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

  let lastKnownMenuPos = null;

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

    lastKnownMenuPos = { left, top: closeRect.top + (closeRect.height - height) / 2 };
    setPosition(lastKnownMenuPos.left, lastKnownMenuPos.top, false);
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
      if (lastKnownMenuPos) {
        setPosition(lastKnownMenuPos.left, lastKnownMenuPos.top, false);
      } else {
        setPosition(window.innerWidth - 76, 78, false);
      }
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
      subtitleToggleBtn.textContent = subtitleEnabled ? "Subtitles: ON ✓" : "Subtitles: OFF";
    }

    // Sync size input display value
    const inp = document.getElementById("pvsc-size-input");
    if (inp) {
      const pct = parseInt(subtitleSize, 10);
      if (Number.isFinite(pct) && inp.value !== String(pct)) inp.value = String(pct);
    }

    const bgBtn = document.getElementById("pvsc-btn-subbg");
    if (bgBtn) {
      let bgLabel = "Shadow";
      if (subtitleBg === "solid") bgLabel = "Solid";
      else if (subtitleBg === "transparent") bgLabel = "None";
      bgBtn.textContent = "Bg: " + bgLabel;
    }

    for (const swatch of menu.querySelectorAll("[data-color]")) {
      const colorVal = swatch.getAttribute("data-color");
      swatch.classList.toggle("pvsc-swatch-active", subtitleEnabled && colorVal.toLowerCase() === subtitleColor.toLowerCase());
    }
    updateStatsDisplay();
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
  speedTitle.textContent = "⚡ Speed";
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
  subtitleTitle.textContent = "💬 Subtitles";
  menu.appendChild(subtitleTitle);

  const subtitleToggleBtn = document.createElement("button");
  subtitleToggleBtn.type = "button";
  subtitleToggleBtn.className = "pvsc-subtitle-toggle";
  subtitleToggleBtn.textContent = subtitleEnabled ? "Subtitles: ON ✓" : "Subtitles: OFF";
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

  // ── Subtitle size row: label + number input + % label ──────────────────────
  const sizeRow = document.createElement("div");
  sizeRow.style.cssText = "display:flex;align-items:center;gap:6px;margin-top:4px;";

  const sizeLabel = document.createElement("span");
  sizeLabel.textContent = "Size";
  sizeLabel.style.cssText = "font-size:12px;color:rgba(255,255,255,0.7);flex-shrink:0;";

  const sizeInput = document.createElement("input");
  sizeInput.id = "pvsc-size-input";
  sizeInput.type = "number";
  sizeInput.min = "50";
  sizeInput.max = "400";
  sizeInput.step = "10";
  sizeInput.value = String(parseInt(subtitleSize, 10) || 150);
  sizeInput.style.cssText = [
    "flex:1",
    "min-width:0",
    "height:30px",
    "padding:0 6px",
    "background:rgba(255,255,255,0.09)",
    "border:1px solid rgba(255,255,255,0.18)",
    "border-radius:7px",
    "color:#f7f7f8",
    "font:inherit",
    "font-size:13px",
    "text-align:center",
    "-moz-appearance:textfield",
    "outline:none",
  ].join(";");

  const sizePct = document.createElement("span");
  sizePct.textContent = "%";
  sizePct.style.cssText = "font-size:12px;color:rgba(255,255,255,0.55);flex-shrink:0;";

  // Commit on Enter or blur; let the input stay focused so user can keep typing
  function commitSizeInput() {
    setSubtitleSize(sizeInput.value);
  }
  sizeInput.addEventListener("change", commitSizeInput);
  sizeInput.addEventListener("keydown", (e) => {
    e.stopPropagation(); // prevent global hotkeys from firing while typing
    if (e.key === "Enter") { commitSizeInput(); sizeInput.blur(); }
    if (e.key === "Escape") { sizeInput.blur(); }
  });
  // Prevent pointerdown from closing menu
  sizeInput.addEventListener("pointerdown", (e) => e.stopPropagation());
  sizeInput.addEventListener("click", (e) => { e.stopPropagation(); sizeInput.select(); });

  sizeRow.appendChild(sizeLabel);
  sizeRow.appendChild(sizeInput);
  sizeRow.appendChild(sizePct);
  menu.appendChild(sizeRow);

  // ── Bg toggle button ─────────────────────────────────────────────────────────
  const bgBtn = makeMenuButton("Bg: Shadow", "", () => cycleSubtitleBg());
  bgBtn.id = "pvsc-btn-subbg";
  bgBtn.style.cssText = "margin-top:4px;width:100%;";
  menu.appendChild(bgBtn);

  const divider2 = document.createElement("div");
  divider2.className = "pvsc-divider";
  menu.appendChild(divider2);

  const statsRow = document.createElement("div");
  statsRow.className = "pvsc-stats-row";
  statsRow.id = "pvsc-stats-text";
  menu.appendChild(statsRow);

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
  }, { signal: lifecycleSignal });
  document.addEventListener("mousemove", showControls, { passive: true, signal: lifecycleSignal });
  document.addEventListener("touchstart", showControls, { passive: true, signal: lifecycleSignal });

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

    const key = event.key.toLowerCase();
    if (key === "]" || key === "+" || key === "=" || event.key === "ArrowUp") {
      event.preventDefault();
      setSpeed(speed + STEP);
      showControls();
    } else if (key === "[" || key === "-" || key === "_" || event.key === "ArrowDown") {
      event.preventDefault();
      setSpeed(speed - STEP);
      showControls();
    } else if (key === "\\") {
      event.preventDefault();
      setSpeed(DEFAULT_SPEED);
      showControls();
    } else if (key === "s") {
      event.preventDefault();
      setSubtitleEnabled(!subtitleEnabled);
      showControls();
    } else if (key === "n") {
      event.preventDefault();
      const autoSkipButtons = document.querySelectorAll(AUTO_SKIP_SELECTOR);
      for (const btn of autoSkipButtons) {
        if (isVisible(btn)) { try { btn.click(); } catch {} }
      }
    } else if (event.key === "Escape") {
      setMenuOpen(false);
    }
  }, { capture: true, signal: lifecycleSignal });

  window.addEventListener("resize", () => {
    const rect = root.getBoundingClientRect();
    setPosition(rect.left, rect.top, Boolean(readSavedPosition()));
    refresh();
  }, { signal: lifecycleSignal });
  maintenanceTimer = window.setInterval(() => {
    const video = findVideo();
    applySpeed(video);
    checkAndHandleAds(video);
  }, 200);
  refreshTimer = window.setInterval(refresh, 500);

  updateActivePreset();
  applySpeed();
  refresh();

  const controlApi = {
    installed: true,
    version: "3.6.1",
    applySpeed,
    refresh,
    applySubtitleStyles,
    checkAndHandleAds,
    destroy() {
      window.clearInterval(maintenanceTimer);
      window.clearInterval(refreshTimer);
      window.clearTimeout(hideTimer);
      window.clearTimeout(mutationThrottleTimer);
      lifecycleController.abort();
      subtitleObserver?.disconnect();
      detachVideoListeners(attachedVideo);
      restoreInactiveSubtitleElements();

      if (attachedVideo && isAdCurrentlyActive) {
        attachedVideo.muted = wasMutedBeforeAd;
        attachedVideo.playbackRate = speed;
        attachedVideo.defaultPlaybackRate = speed;
      }
      isAdCurrentlyActive = false;
      restoreHiddenVideos();
      removeAdCover();
      root.remove();
      document.getElementById(STYLE_ID)?.remove();
      document.getElementById(SUBTITLE_STYLE_ID)?.remove();
      document.getElementById(AD_SHIELD_STYLE_ID)?.remove();
      if (window.__primeVideoSpeedControl === controlApi) {
        delete window.__primeVideoSpeedControl;
      }
    }
  };
  window.__primeVideoSpeedControl = controlApi;

  return "installed";
})();
