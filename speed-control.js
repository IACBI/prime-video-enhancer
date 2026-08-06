(() => {
  // ═══════════════════════════════════════════════════════════════════════════
  //  Prime Video Speed & Subtitle Controller
  //
  //  One script, two hosts: a C# console app that drives Edge over CDP, and a
  //  Flutter/InAppWebView Android app. `mobile/assets/speed-control.js` must
  //  stay byte-identical to this file.
  //
  //  Layers, in order:
  //    1. Constants          6. Subtitles
  //    2. Install guard      7. Panel styles
  //    3. Platform           8. Panel DOM
  //    4. Settings           9. Scheduler & lifecycle
  //    5. Playback / ads
  // ═══════════════════════════════════════════════════════════════════════════

  // ── 1. Constants ───────────────────────────────────────────────────────────

  const VERSION = "3.7.0";

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
  const PRESERVE_PITCH_KEY = "primeVideoSpeedControl.preservesPitch";
  const ADS_BLOCKED_KEY = "primeVideoSpeedControl.adsBlockedCount";
  const ADS_SAVED_SEC_KEY = "primeVideoSpeedControl.adsTimeSavedSecs";

  // Marks the long-lived container Prime renders subtitles into. The stylesheet
  // targets its descendants, so a cue inserted later is styled during style
  // resolution — before its first paint — with no JavaScript in the path.
  const SUB_ROOT_ATTR = "data-pvsc-sub-root";
  // Fallback for players that rebuild the container itself on every cue. Stamped
  // from the MutationObserver callback, which still runs before the next paint.
  const SUB_CUE_ATTR = "data-pvsc-sub-cue";

  // The .atvwebplayersdk-* classes come from the desktop web player. Prime
  // Video serves a different player build to mobile UAs, so generic fallbacks
  // are needed or subtitle styling silently does nothing on a phone. The
  // geometry filter in findSubtitleTextElements (bottom 55% of the video,
  // height under 30%) is what keeps the loose matches from grabbing UI chrome.
  const SUBTITLE_TEXT_SELECTOR = [
    ".atvwebplayersdk-subtitle-text",
    ".atvwebplayersdk-captions-text",
    ".timedText",
    "[class*='subtitle' i]",
    "[class*='caption' i]",
    "[class*='timedtext' i]",
    "[class*='timed-text' i]"
  ].join(", ");

  // Anything in this list disqualifies an element from being treated as a
  // subtitle container. Without it a full-bleed overlay that happens to hold
  // both the captions and the episode title would get stamped, and the title
  // would be restyled along with the dialogue.
  const SUBTITLE_CHROME_SELECTOR =
    "button, [role='button'], input, select, textarea, video, img, svg, [class*='title' i]";

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

  // Subtitle height as a fraction of the *video's* rendered height, at the 100%
  // setting. Roughly the CEA-708 / BBC guideline. Deliberately not vh: in
  // landscape on a phone 1vh is ~3.6px, so a vh-based default rendered at ~10px
  // — and every vh value jumped whenever the host resized the WebView for
  // fullscreen. Video height tracks the picture, which is what the number means.
  const SUBTITLE_HEIGHT_RATIO = 0.045;
  const SUBTITLE_MIN_PX = 12;
  const SUBTITLE_MAX_PX = 72;

  // How long "no ad detected" must hold before ad-mode is exited. Wall-clock,
  // not a tick count: the scheduler below changes rate with playback state, and
  // a tick count would silently mean something different at each rate. A single
  // missed detection (Prime re-rendering the indicator during an ad-to-content
  // transition) would otherwise unmute/un-freeze early and immediately
  // re-trigger ad-mode, producing an audible flicker at ad boundaries.
  const AD_END_CONFIRM_MS = 400;

  // Safety valve: real Amazon Prime Video ad breaks don't run this long. If the
  // shield has been continuously engaged (muted, hyper-speed, video hidden) for longer
  // than this, it's almost certainly a stuck or false detection rather than a
  // real ad, so playback is forcibly handed back to the user instead of racing
  // through the rest of the episode at ad speed behind a black cover.
  const AD_MAX_DURATION_MS = 45000;

  // After the safety valve force-exits ad mode, suppress re-engaging the visual
  // shield for this long. Without a cooldown, a persistently-matching element
  // (a stuck indicator, or UI we misclassify) re-engages ad mode on the very
  // next tick after the valve fires, turning one false positive into an
  // endless loop of black-screen/hyper-speed windows. Skip-button clicking and the
  // network-level blocker stay active during the cooldown, so real ads are
  // still handled — only the mute/hide/hyper-speed shield is suppressed.
  const AD_COOLDOWN_AFTER_VALVE_MS = 120000;

  // Scheduler cadence. Idle playback only needs to notice an ad *starting*, and
  // the MutationObserver already covers that within a frame; the timer is a
  // safety net. Inside an ad the latency is multiplied by the ad speed, so a
  // second of lag would skip half a minute of episode — hence the fast rate.
  const TICK_IDLE_MS = 1000;
  const TICK_AD_MS = 100;

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

  // ── 2. Install guard ───────────────────────────────────────────────────────

  const previousControl = window.__primeVideoSpeedControl;
  if (previousControl?.installed) {
    if (previousControl.version === VERSION) {
      previousControl.refresh();
      previousControl.applySpeed();
      previousControl.applySubtitleStyles();
      previousControl.checkAndHandleAds();
      return "already-installed";
    }
    previousControl.destroy?.();
  }

  // ── 3. Platform ────────────────────────────────────────────────────────────

  // Kept in lockstep with the CSS breakpoints below. They used to disagree —
  // the CSS included a width clause and the JS did not — so a narrow desktop
  // window got the touch layout with desktop positioning logic driving it.
  const TOUCH_QUERY = "(pointer: coarse), (hover: none), (max-width: 768px)";
  const LANDSCAPE_QUERY = "(orientation: landscape)";

  function matchQuery(query) {
    try {
      return window.matchMedia(query).matches;
    } catch {
      return false;
    }
  }

  // Only the touch/desktop split needs to be tracked in JS — it decides default
  // placement. Orientation is watched too, but purely so a rotation re-runs the
  // reflow; the layout itself is the stylesheet's business.
  let isTouch = matchQuery(TOUCH_QUERY);

  const lifecycleController = new AbortController();
  const lifecycleSignal = lifecycleController.signal;

  // ── 4. Settings ────────────────────────────────────────────────────────────

  /**
   * Reads a setting, tolerating storage being unavailable.
   *
   * Touching localStorage throws outright on a document with an opaque origin,
   * and when the user or the embedder has blocked site data. These reads happen
   * at install time, so an unguarded one takes the whole script down with it and
   * the panel simply never appears.
   */
  function readStored(key) {
    try {
      return window.localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  let speed = Number(readStored(STORAGE_KEY));
  if (!Number.isFinite(speed) || speed < MIN_SPEED || speed > MAX_SPEED) {
    speed = DEFAULT_SPEED;
  }

  let subtitleColor = readStored(SUBTITLE_STORAGE_KEY) || DEFAULT_SUBTITLE_COLOR;
  if (!/^#[0-9A-Fa-f]{6}$/.test(subtitleColor)) {
    subtitleColor = DEFAULT_SUBTITLE_COLOR;
  }

  let subtitleEnabled = readStored(SUBTITLE_ENABLED_KEY) !== "false";
  let subtitleSize = readStored(SUBTITLE_SIZE_KEY) || "150%";
  let subtitleBg = readStored(SUBTITLE_BG_KEY) || "shadow"; // transparent, shadow, solid
  let preservePitch = readStored(PRESERVE_PITCH_KEY) !== "false";

  let adsBlockedCount = parseInt(readStored(ADS_BLOCKED_KEY) || "0", 10);
  let adsTimeSavedSecs = parseInt(readStored(ADS_SAVED_SEC_KEY) || "0", 10);

  /**
   * Batches localStorage writes.
   *
   * setItem is synchronous and hits disk. Doing it inside a click handler, on
   * the same task as the playbackRate write, is a measurable stall on a low-end
   * phone — and holding "+" produces a burst of them. These are preferences, so
   * losing the last 300ms to a crash costs nothing.
   */
  const pendingWrites = new Map();
  let persistTimer = 0;

  function persist(key, value) {
    pendingWrites.set(key, value);
    if (persistTimer) return;
    persistTimer = window.setTimeout(flushPersist, 300);
  }

  function flushPersist() {
    window.clearTimeout(persistTimer);
    persistTimer = 0;
    for (const [key, value] of pendingWrites) {
      try {
        window.localStorage.setItem(key, value);
      } catch {}
    }
    pendingWrites.clear();
  }

  // ── 5. Playback / ads ──────────────────────────────────────────────────────

  let attachedVideo = null;
  let hideTimer = 0;
  let isMenuOpen = false;
  let isDragging = false;
  let dragStarted = false;
  let dragOffsetX = 0;
  let dragOffsetY = 0;
  let lastPointerX = 0;
  let lastPointerY = 0;

  const TARGET_AD_SPEED = 30;
  const FALLBACK_AD_SPEED = 16;
  let currentAdSpeed = TARGET_AD_SPEED;

  let isAdCurrentlyActive = false;
  let wasMutedBeforeAd = false;
  let firstNoAdAt = 0;
  let adModeStartedAt = 0;
  let adCooldownUntil = 0;
  let adHiddenVideo = null;
  const handledAutoSkipButtons = new WeakSet();
  const handledAdSkipButtons = new WeakSet();

  // Guards against an unbounded rate-write fight with Amazon's own player SDK.
  // If the SDK insists on resetting the rate, re-asserting from `ratechange`
  // turns into a ping-pong at event rate — and every single toggle makes
  // Chromium resize the audio renderer's buffer, which is the pause the user
  // sees. Above the threshold we stop reacting to the event and let the slow
  // safety tick carry it instead.
  const MAX_REASSERTS_PER_SEC = 3;
  let reassertWindowStart = 0;
  let reassertCount = 0;
  let reassertSuspended = false;

  function clamp(value) {
    return Math.min(MAX_SPEED, Math.max(MIN_SPEED, value));
  }

  function format(value) {
    return `${value.toFixed(2).replace(/\.?0+$/, "")}x`;
  }

  function formatStop(value) {
    return value.toFixed(2).replace(/\.?0+$/, "");
  }

  function findVideo() {
    // Stay with the element we already own. Re-electing on every tick made two
    // consecutive ticks resolve to different <video> elements around ad/content
    // transitions, and the rate then landed on whichever won that tick.
    if (attachedVideo && attachedVideo.isConnected) {
      return attachedVideo;
    }

    const videos = Array.from(document.querySelectorAll("video"));
    return (
      videos.find((video) => video.readyState > 0) ||
      videos.find((video) => video.currentSrc || video.src) ||
      videos[0] ||
      null
    );
  }

  /**
   * The single place playback rate is written.
   *
   * preservesPitch first: it selects which resampling path the audio renderer
   * uses, and changing it *after* the rate makes the renderer reconfigure a
   * second time, at the new rate.
   */
  function writeRate(video, rate) {
    if (!video) return;

    // Pitch correction is a time-stretcher running on the audio thread. At the
    // ad shield's 30x it cannot keep up on a low-end phone and stalls the whole
    // pipeline — and the video is muted during ad mode anyway, so there is
    // nothing to preserve.
    const pitch = isAdCurrentlyActive ? false : preservePitch;
    if ("preservesPitch" in video && video.preservesPitch !== pitch) {
      video.preservesPitch = pitch;
    }

    if (video.playbackRate !== rate) video.playbackRate = rate;
  }

  /**
   * Brings defaultPlaybackRate in line, away from the interaction path.
   *
   * It is worth setting — it is the value playbackRate returns to when the
   * player resets, so without it the speed can silently revert on a seek — but
   * it does not touch the audio renderer, and it still fires `ratechange`.
   * Writing it next to the rate therefore doubled the events Amazon's player
   * SDK sees for one tap while buying nothing. It rides the background tick
   * instead: one event on the path the user can feel, not two.
   */
  function alignDefaultRate(video, rate) {
    if (video && video.defaultPlaybackRate !== rate) {
      video.defaultPlaybackRate = rate;
    }
  }

  function targetRate() {
    return isAdCurrentlyActive ? currentAdSpeed : speed;
  }

  /** True while the pipeline is in a state where writing the rate would extend a rebuffer. */
  function isRateWriteUnsafe(video) {
    return !video || video.seeking || video.readyState < 3;
  }

  function applySpeed(video = findVideo()) {
    if (!video) return;
    attachVideoListeners(video);
    if (isAdCurrentlyActive && video.muted !== true) video.muted = true;
    writeRate(video, targetRate());
  }

  function handleRateDrift() {
    const video = attachedVideo;
    if (!video || isRateWriteUnsafe(video)) return;
    if (video.playbackRate === targetRate()) return;

    const now = Date.now();
    if (now - reassertWindowStart > 1000) {
      reassertWindowStart = now;
      reassertCount = 0;
      reassertSuspended = false;
    }

    reassertCount += 1;
    if (reassertCount > MAX_REASSERTS_PER_SEC) {
      if (!reassertSuspended) {
        reassertSuspended = true;
        console.warn("[pvsc] Playback rate is being reset faster than it can be re-applied; backing off to the safety tick.");
      }
      return;
    }

    writeRate(video, targetRate());
  }

  function handleVideoReady() {
    reassertSuspended = false;
    reassertCount = 0;
    const video = attachedVideo;
    if (video && !isRateWriteUnsafe(video)) {
      alignDefaultRate(video, targetRate());
      writeRate(video, targetRate());
    }
  }

  function handleAdStall() {
    if (isAdCurrentlyActive && currentAdSpeed > FALLBACK_AD_SPEED) {
      console.warn("[pvsc] Ad playback stalled at 30x, falling back to 16x");
      currentAdSpeed = FALLBACK_AD_SPEED;
      applySpeed();
    }
  }

  function handleVideoEmptied() {
    attachedVideo = null;
    subtitleRootChanged();
  }

  function detachVideoListeners(video) {
    if (!video) return;
    video.removeEventListener("play", showControls);
    video.removeEventListener("playing", showControls);
    video.removeEventListener("pause", showControls);
    video.removeEventListener("seeked", showControls);
    video.removeEventListener("ratechange", handleRateDrift);
    video.removeEventListener("loadedmetadata", handleVideoReady);
    video.removeEventListener("loadeddata", handleVideoReady);
    video.removeEventListener("canplay", handleVideoReady);
    video.removeEventListener("play", handleVideoReady);
    video.removeEventListener("seeked", handleVideoReady);
    video.removeEventListener("emptied", handleVideoEmptied);
    video.removeEventListener("waiting", handleAdStall);
    video.removeEventListener("stalled", handleAdStall);
    videoSizeObserver?.disconnect();
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
    // Deliberately NOT bound to timeupdate. It fires ~4x/s on the media hot
    // path, and each re-assert it triggered was another chance to collide with
    // the player SDK. The events below cover every point the rate can actually
    // be lost, and the safety tick covers the rest.
    attachedVideo.addEventListener("ratechange", handleRateDrift, { passive: true });
    attachedVideo.addEventListener("loadedmetadata", handleVideoReady, { passive: true });
    attachedVideo.addEventListener("loadeddata", handleVideoReady, { passive: true });
    attachedVideo.addEventListener("canplay", handleVideoReady, { passive: true });
    attachedVideo.addEventListener("play", handleVideoReady, { passive: true });
    attachedVideo.addEventListener("seeked", handleVideoReady, { passive: true });
    attachedVideo.addEventListener("emptied", handleVideoEmptied, { passive: true });
    attachedVideo.addEventListener("waiting", handleAdStall, { passive: true });
    attachedVideo.addEventListener("stalled", handleAdStall, { passive: true });

    observeVideoSize(attachedVideo);
    showControls();
  }

  function updateStatsDisplay() {
    if (statsRow) {
      statsRow.textContent = `${adsBlockedCount} ads blocked · ${Math.floor(adsTimeSavedSecs / 60)}m saved`;
    }
  }

  function incrementAdStats(count, secs) {
    adsBlockedCount += count;
    adsTimeSavedSecs += secs;
    persist(ADS_BLOCKED_KEY, String(adsBlockedCount));
    persist(ADS_SAVED_SEC_KEY, String(adsTimeSavedSecs));
    updateStatsDisplay();
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
    firstNoAdAt = 0;
    adModeStartedAt = 0;
    video.muted = wasMutedBeforeAd;
    restoreHiddenVideos();
    removeAdCover();
    alignDefaultRate(video, speed);
    writeRate(video, speed);
    setTickRate(TICK_IDLE_MS);
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
      firstNoAdAt = 0;
    }

    if (adDetected && !isAdCurrentlyActive && Date.now() >= adCooldownUntil) {
      isAdCurrentlyActive = true;
      currentAdSpeed = TARGET_AD_SPEED;
      adModeStartedAt = Date.now();
      wasMutedBeforeAd = video.muted;
      showAdCover(video);
      video.muted = true;
      hideVideoForAd(video);
      writeRate(video, currentAdSpeed);
      setTickRate(TICK_AD_MS);
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
      writeRate(video, currentAdSpeed);
      if (video.muted !== true) video.muted = true;
      if (video !== adHiddenVideo || video.style.opacity !== "0") hideVideoForAd(video);
      showAdCover(video);
      if (video.paused) {
        try { video.play(); } catch {}
      }
    } else if (isAdCurrentlyActive && !adDetected) {
      // Require the negative to hold for a while before declaring the ad over,
      // to avoid flicker if an indicator element briefly disappears during
      // Prime Video's own re-render at the ad/content boundary.
      if (!firstNoAdAt) {
        firstNoAdAt = Date.now();
      } else if (Date.now() - firstNoAdAt >= AD_END_CONFIRM_MS) {
        exitAdMode(video);
      }
    }
  }

  // ── 6. Subtitles ───────────────────────────────────────────────────────────

  // Everything visual lives in the stylesheet below and is parameterised by
  // custom properties. Two consequences, both of them the point:
  //
  //   * A cue element inserted by Prime matches the rule during style
  //     resolution, i.e. before its first paint. The previous design wrote
  //     inline styles from JavaScript *after* the element existed, which is
  //     structurally one frame too late — that frame is the white flash.
  //   * Changing a setting is one setProperty on <html>. No DOM walk, no
  //     per-element writes, nothing for the video to hitch on.
  let subtitleRoot = null;
  let subtitleCandidate = null;
  let candidateText = "";
  let candidateTurnovers = 0;
  let discoveryTimer = 0;
  let discoveryMisses = 0;
  let videoSizeObserver = null;

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

    // The var() fallbacks are load-bearing, not defensive noise: a custom
    // property that is unset or invalid makes the declaration invalid at
    // computed-value time, and the property then falls back to `inherit`
    // rather than to the previous cascade winner — i.e. silently to Amazon's
    // white. Never remove them.
    style.textContent = `
      [${SUB_ROOT_ATTR}] :is(span, p, div):not(#${ROOT_ID} *),
      [${SUB_CUE_ATTR}], [${SUB_CUE_ATTR}] :is(span, p, div),
      .atvwebplayersdk-subtitle-text, .atvwebplayersdk-subtitle-text :is(span, p, div),
      .atvwebplayersdk-captions-text, .atvwebplayersdk-captions-text :is(span, p, div) {
        color: var(--pvsc-sub-color, ${DEFAULT_SUBTITLE_COLOR}) !important;
        font-size: var(--pvsc-sub-size, 24px) !important;
        font-weight: 700 !important;
        line-height: 1.35 !important;
        text-shadow: var(--pvsc-sub-shadow, none) !important;
        background-color: var(--pvsc-sub-bg, transparent) !important;
        padding: var(--pvsc-sub-pad, 0) !important;
        border-radius: var(--pvsc-sub-radius, 0) !important;
        -webkit-box-decoration-break: clone !important;
        box-decoration-break: clone !important;
        -webkit-text-size-adjust: 100% !important;
      }
      /* The background is a box property, so on nested spans it would paint
         twice and the padding would compound. Only the innermost element that
         actually holds text keeps the box; wrappers go transparent. */
      [${SUB_ROOT_ATTR}] :is(span, p, div):has(:is(span, p, div)),
      [${SUB_CUE_ATTR}]:has(:is(span, p, div)),
      [${SUB_CUE_ATTR}] :is(span, p, div):has(:is(span, p, div)),
      .atvwebplayersdk-subtitle-text:has(:is(span, p, div)),
      .atvwebplayersdk-captions-text:has(:is(span, p, div)) {
        background-color: transparent !important;
        padding: 0 !important;
        border-radius: 0 !important;
        text-shadow: none !important;
      }
      /* Kept for any build that renders captions as native TextTracks rather
         than DOM elements. Not the primary path — Prime does not use it. */
      video::cue {
        color: var(--pvsc-sub-color, ${DEFAULT_SUBTITLE_COLOR}) !important;
        font-size: var(--pvsc-sub-size, 24px) !important;
        font-weight: 700 !important;
        background-color: var(--pvsc-sub-bg, transparent) !important;
        text-shadow: var(--pvsc-sub-shadow, none) !important;
      }
    `;
  }

  /**
   * Converts the user's percentage into a pixel size derived from the video's
   * rendered height, and pushes every subtitle token onto <html>.
   *
   * This is the whole "apply a subtitle setting" path. It is one style write on
   * a single element.
   */
  function applySubtitleStyles(video = findVideo()) {
    ensureSubtitleStyle();
    if (!subtitleEnabled) return;

    const pct = parseInt(subtitleSize, 10) || 150;
    const rect = video ? video.getBoundingClientRect() : null;
    const basis = rect && rect.height > 0 ? rect.height : window.innerHeight;
    const sizePx = Math.round(Math.min(
      SUBTITLE_MAX_PX,
      Math.max(SUBTITLE_MIN_PX, basis * SUBTITLE_HEIGHT_RATIO * (pct / 100))
    ));

    const scale = Math.max(0.5, pct / 100);
    let bg;
    let shadow;
    let pad;
    let radius;
    if (subtitleBg === "solid") {
      bg = "rgba(0, 0, 0, 0.92)";
      shadow = `0 ${(1.5 * scale).toFixed(1)}px ${(3 * scale).toFixed(1)}px rgba(0, 0, 0, 0.8)`;
      pad = "0.12em 0.35em";
      radius = "4px";
    } else if (subtitleBg === "transparent") {
      bg = "transparent";
      // Without a plate behind it the text needs its own outline to stay
      // readable over bright frames.
      shadow = `-1px -1px 0 #000, 1px -1px 0 #000, -1px 1px 0 #000, 1px 1px 0 #000, 0 ${(2.5 * scale).toFixed(1)}px ${(4 * scale).toFixed(1)}px rgba(0,0,0,0.9)`;
      pad = "0";
      radius = "0";
    } else {
      bg = "rgba(0, 0, 0, 0.48)";
      shadow = `0 ${(1.5 * scale).toFixed(1)}px ${(3 * scale).toFixed(1)}px rgba(0, 0, 0, 0.8)`;
      pad = "0.12em 0.35em";
      radius = "4px";
    }

    const style = document.documentElement.style;
    style.setProperty("--pvsc-sub-color", subtitleColor);
    style.setProperty("--pvsc-sub-size", `${sizePx}px`);
    style.setProperty("--pvsc-sub-bg", bg);
    style.setProperty("--pvsc-sub-shadow", shadow);
    style.setProperty("--pvsc-sub-pad", pad);
    style.setProperty("--pvsc-sub-radius", radius);
  }

  /**
   * Finds the elements currently holding subtitle text.
   *
   * Only used to *discover* the container now — not to style anything. It runs
   * on cue turnover rather than on a timer.
   */
  function findSubtitleTextElements(video) {
    if (!video) return new Set();
    const videoRect = video.getBoundingClientRect();
    if (videoRect.width <= 0 || videoRect.height <= 0) return new Set();

    // Measured on the mobile player: resting subtitles sit at ~0.83 of the
    // video height, but Prime lifts them to ~0.33 while the control overlay is
    // up, which a 0.45 cut-off dropped — styling visibly fell off every time
    // the controls appeared. The episode title, the one thing in this selector
    // that must never be restyled, sits at ~0.09, so 0.28 clears both.
    const minimumSubtitleTop = videoRect.top + videoRect.height * 0.28;
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

  function lowestCommonAncestor(elements) {
    let ancestor = elements[0];
    for (let i = 1; i < elements.length && ancestor; i += 1) {
      while (ancestor && !ancestor.contains(elements[i])) {
        ancestor = ancestor.parentElement;
      }
    }
    return ancestor;
  }

  /**
   * Whether an element is safe to hand the subtitle stylesheet.
   *
   * The band check is what keeps the walk from reaching a full-bleed player
   * overlay: such an element starts at the top of the video, so it fails the
   * 0.28 floor before the episode title inside it is ever a concern. The chrome
   * check covers the layout where captions share a bottom bar with controls.
   */
  function isSubtitleContainer(el, videoRect) {
    if (!(el instanceof HTMLElement)) return false;
    if (el === document.body || el === document.documentElement) return false;
    // ensureRootAttached() moves our own panel into the fullscreen subtree,
    // which can make it a sibling — or a descendant — of the captions wrapper.
    // Stamping an ancestor of the panel would restyle the panel's own text.
    if (root.contains(el) || el.contains(root)) return false;

    const rect = el.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return false;
    if (rect.height > videoRect.height * 0.45) return false;
    if (rect.top < videoRect.top + videoRect.height * 0.28) return false;
    if (rect.bottom > videoRect.bottom + 4) return false;

    return el.querySelector(SUBTITLE_CHROME_SELECTOR) === null
      && el.querySelector(AD_INDICATOR_SELECTOR) === null
      && el.querySelector(AUTO_SKIP_SELECTOR) === null;
  }

  function stampSubtitleRoot(el) {
    if (subtitleRoot === el) return;
    if (subtitleRoot) subtitleRoot.removeAttribute(SUB_ROOT_ATTR);
    subtitleRoot = el;
    // An older installed version wrote `background-color: transparent` onto this
    // node directly. Inline declarations without !important lose to the
    // stylesheet, but clearing it keeps the DOM honest across an upgrade.
    el.style.removeProperty("background-color");
    el.setAttribute(SUB_ROOT_ATTR, "");
  }

  function subtitleRootChanged() {
    if (subtitleRoot) subtitleRoot.removeAttribute(SUB_ROOT_ATTR);
    subtitleRoot = null;
    subtitleCandidate = null;
    candidateText = "";
    candidateTurnovers = 0;
    discoveryMisses = 0;
  }

  /**
   * Promotes a container only once it has survived a cue turnover.
   *
   * There is no way to tell a persistent captions wrapper from a per-cue block
   * by inspection — both match the loose selectors, and the mobile build's
   * class names are hashed. So it is settled empirically: hold the candidate,
   * and stamp it once its text has changed underneath it while it stayed
   * connected. If it dies between cues instead, the walk starts one level up.
   */
  function trackCandidate(candidate) {
    if (candidate !== subtitleCandidate) {
      subtitleCandidate = candidate;
      candidateText = candidate.textContent || "";
      candidateTurnovers = 0;
      return;
    }

    const text = candidate.textContent || "";
    if (text && text !== candidateText) {
      candidateText = text;
      candidateTurnovers += 1;
    }

    if (candidateTurnovers >= 1) {
      stampSubtitleRoot(candidate);
    }
  }

  function discoverSubtitleRoot() {
    if (!subtitleEnabled) return;
    if (subtitleRoot && subtitleRoot.isConnected) return;
    if (subtitleRoot) subtitleRootChanged();

    const video = findVideo();
    if (!video) return;
    const videoRect = video.getBoundingClientRect();
    if (videoRect.width <= 0 || videoRect.height <= 0) return;

    const leaves = [...findSubtitleTextElements(video)];
    if (!leaves.length) {
      // No cue on screen, which is the normal state most of the time. Back off
      // so an idle player is not paying for a DOM sweep on every insertion.
      discoveryMisses += 1;
      return;
    }
    discoveryMisses = 0;

    let node = lowestCommonAncestor(leaves);
    if (!node) return;

    // Take the *highest* ancestor that still looks like a captions region: the
    // higher it is, the likelier it outlives an individual cue.
    let best = null;
    for (let depth = 0; node && depth < 8; depth += 1) {
      if (isSubtitleContainer(node, videoRect)) best = node;
      else if (best) break;
      node = node.parentElement;
    }

    if (best) trackCandidate(best);
  }

  function scheduleDiscovery() {
    if (discoveryTimer || !subtitleEnabled) return;
    if (subtitleRoot && subtitleRoot.isConnected) return;
    const delay = discoveryMisses > 4 ? 2000 : 250;
    discoveryTimer = window.setTimeout(() => {
      discoveryTimer = 0;
      discoverSubtitleRoot();
    }, delay);
  }

  /**
   * Marks a freshly-inserted node as a cue, from inside the observer callback.
   *
   * MutationObserver callbacks run at the microtask checkpoint, before the next
   * paint, so the stylesheet rule is resolved in time even for players that
   * rebuild the container on every cue and therefore never satisfy the
   * turnover test above. One attribute write, and `data-` attributes are not in
   * the observer's attributeFilter, so it cannot feed back into itself.
   */
  function stampCueNode(node) {
    if (subtitleRoot && subtitleRoot.contains(node)) return;
    if (root.contains(node) || node.contains(root)) return;

    let target = null;
    if (node.matches(SUBTITLE_TEXT_SELECTOR)) target = node;
    else target = node.querySelector(SUBTITLE_TEXT_SELECTOR);
    if (!target || target.hasAttribute(SUB_CUE_ATTR)) return;
    if (target.querySelector(SUBTITLE_CHROME_SELECTOR)) return;

    // The same band the discovery walk uses, and it is not optional here:
    // SUBTITLE_TEXT_SELECTOR is deliberately loose, so without a geometry gate
    // this path would stamp the episode title — the one element the gate exists
    // to protect — the moment Prime inserted it. One forced layout per inserted
    // cue is a fair price, and it replaces a sweep that ran twenty times a second.
    const video = attachedVideo;
    if (!video) return;
    const videoRect = video.getBoundingClientRect();
    if (videoRect.height <= 0) return;

    const rect = target.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return;
    if (rect.height > videoRect.height * 0.45) return;
    if (rect.top < videoRect.top + videoRect.height * 0.28) return;
    if (rect.bottom > videoRect.bottom + 4) return;

    target.setAttribute(SUB_CUE_ATTR, "");
  }

  function clearCueStamps() {
    for (const el of document.querySelectorAll(`[${SUB_CUE_ATTR}]`)) {
      el.removeAttribute(SUB_CUE_ATTR);
    }
  }

  function observeVideoSize(video) {
    videoSizeObserver?.disconnect();
    if (typeof ResizeObserver !== "function") return;
    // The subtitle size is derived from the video's rendered height, and that
    // changes on rotation, on fullscreen, and whenever the letterboxing changes
    // for a different aspect ratio — the last of which fires no window event.
    videoSizeObserver = new ResizeObserver(() => applySubtitleStyles(video));
    videoSizeObserver.observe(video);
  }

  // ── Observer ───────────────────────────────────────────────────────────────

  let currentObservedContainer = null;
  let subtitleObserver = null;
  let adCheckQueued = false;
  let adWatchTimers = [];

  function scheduleAdCheck(delay = 60) {
    if (adCheckQueued) return;
    adCheckQueued = true;
    window.setTimeout(() => {
      adCheckQueued = false;
      checkAndHandleAds();
    }, delay);
  }

  /**
   * Re-checks shortly after an ad indicator appears.
   *
   * isAdIndicatorActive requires countdown-shaped text, and Prime mounts the
   * element before filling it in. The insertion is the only mutation record we
   * get — the text arriving is characterData, which is not observed — so the
   * check has to be repeated a couple of times or the shield waits for the
   * safety tick.
   */
  function armAdWatch() {
    for (const timer of adWatchTimers) window.clearTimeout(timer);
    adWatchTimers = [120, 350, 800].map((delay) =>
      window.setTimeout(() => checkAndHandleAds(), delay)
    );
  }

  function updateSubtitleObserver() {
    // This observer drives both ad detection and subtitle discovery, so it must
    // stay connected regardless of subtitleEnabled — otherwise disabling
    // subtitles would silently degrade ad-detection latency from "near-instant
    // on mutation" to a full safety tick, an unintended coupling between two
    // unrelated features.
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
        // Every record is examined. The previous version dropped the whole
        // batch whenever a debounce timer was armed, and broke out of the scan
        // on the first record of either kind — so the record that actually
        // announced a new cue was routinely thrown away.
        let sawInsertion = false;
        let sawAdCandidate = false;
        let sawAttribute = false;

        for (const mutation of mutations) {
          if (mutation.type === "attributes") {
            sawAttribute = true;
            continue;
          }
          if (mutation.type !== "childList") continue;

          for (const node of mutation.addedNodes) {
            if (!(node instanceof HTMLElement) || root.contains(node)) continue;
            sawInsertion = true;
            if (subtitleEnabled) stampCueNode(node);
            if (!sawAdCandidate && (node.matches(AD_INDICATOR_SELECTOR) || node.querySelector(AD_INDICATOR_SELECTOR))) {
              sawAdCandidate = true;
            }
          }
        }

        if (sawInsertion) scheduleDiscovery();
        if (sawAdCandidate) armAdWatch();
        else if (sawInsertion || sawAttribute) scheduleAdCheck();
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

  // ── Setting mutators ───────────────────────────────────────────────────────

  function setSpeed(nextSpeed) {
    speed = Number(clamp(nextSpeed).toFixed(2));
    // The rate write is the only thing the user asked for, and it is cheap.
    // Everything else — storage, label, active states, the rail indicator —
    // is deferred so it cannot land in the same task as the write.
    applySpeed();
    persist(STORAGE_KEY, String(speed));
    scheduleUiSync();
  }

  function setSubtitleColor(nextColor) {
    subtitleColor = nextColor;
    persist(SUBTITLE_STORAGE_KEY, subtitleColor);
    if (!subtitleEnabled) {
      setSubtitleEnabled(true);
      return;
    }
    applySubtitleStyles();
    scheduleUiSync();
  }

  function setSubtitleEnabled(enabled) {
    subtitleEnabled = Boolean(enabled);
    persist(SUBTITLE_ENABLED_KEY, String(subtitleEnabled));
    if (!subtitleEnabled) {
      subtitleRootChanged();
      clearCueStamps();
    }
    ensureSubtitleStyle();
    applySubtitleStyles();
    updateSubtitleObserver();
    scheduleDiscovery();
    scheduleUiSync();
  }

  function setSubtitleSize(val) {
    const pct = parseInt(val, 10);
    if (!Number.isFinite(pct) || pct < 50 || pct > 400) return;
    subtitleSize = pct + "%";
    persist(SUBTITLE_SIZE_KEY, subtitleSize);
    applySubtitleStyles();
    scheduleUiSync();
  }

  function cycleSubtitleBg() {
    if (subtitleBg === "shadow") subtitleBg = "solid";
    else if (subtitleBg === "solid") subtitleBg = "transparent";
    else subtitleBg = "shadow";
    persist(SUBTITLE_BG_KEY, subtitleBg);
    applySubtitleStyles();
    scheduleUiSync();
  }

  function setPreservePitch(enabled) {
    preservePitch = Boolean(enabled);
    persist(PRESERVE_PITCH_KEY, String(preservePitch));
    applySpeed();
    scheduleUiSync();
  }

  // ── 7. Panel styles ────────────────────────────────────────────────────────

  function ensureStyle() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }

    const style = document.createElement("style");
    style.id = STYLE_ID;
    // Design note: this panel sits on top of a moving picture, so it borrows
    // the one visual language that has always lived there — the broadcast
    // caption block. Flat, opaque, hard-edged, one accent (CEA-608 caption
    // yellow, which is also this app's default subtitle colour), and emphasis
    // by inversion rather than by fill. There is deliberately no backdrop
    // blur: it is a per-frame GPU cost over playing video, and on a low-end
    // phone that is not decoration anyone is paying for.
    //
    // Breakpoints change tokens only, never rules. That is what keeps the
    // three layouts from turning back into a stack of !important overrides.
    style.textContent = `
      #${ROOT_ID} {
        --pvsc-ink: #0A0A0B;
        --pvsc-ink-2: #17171A;
        --pvsc-ink-3: #232328;
        --pvsc-line: rgba(255, 255, 255, 0.14);
        --pvsc-text: #F2F2F3;
        --pvsc-dim: rgba(242, 242, 243, 0.52);
        --pvsc-live: #FFCC00;
        --pvsc-radius: 4px;
        --pvsc-gap: 8px;
        --pvsc-ctl: 32px;
        --pvsc-pad: 12px;
        --pvsc-panel-w: 240px;
        --pvsc-label: 10px;
        --pvsc-body: 12px;
        --pvsc-launcher-w: 76px;
        --pvsc-launcher-h: 34px;
        --pvsc-launcher-shift: 0px;
        --pvsc-swatch: 28px;
        --pvsc-cols: 1;
        --pvsc-font: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
        --pvsc-mono: ui-monospace, "Cascadia Mono", "Segoe UI Mono", "Roboto Mono", "Droid Sans Mono", monospace;

        position: fixed;
        top: 76px;
        right: 18px;
        z-index: 2147483647;
        display: inline-flex;
        color: var(--pvsc-text);
        font-family: var(--pvsc-font);
        font-size: var(--pvsc-body);
        line-height: 1.2;
        pointer-events: auto;
        transition: opacity 220ms ease;
      }
      #${ROOT_ID}, #${ROOT_ID} * {
        box-sizing: border-box;
      }
      #${ROOT_ID}.pvsc-hidden {
        opacity: 0;
        pointer-events: none;
      }
      #${ROOT_ID}.pvsc-no-video {
        display: none;
      }

      /* ── Launcher ─────────────────────────────────────────────────────── */
      .pvsc-wrap {
        position: relative;
        display: inline-flex;
        align-items: center;
      }
      .pvsc-launcher {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 5px;
        /* Fixed width, not min-width. The label swings between "1x" and
           "1.25x", and a growing button used to force a re-clamp — a synchronous
           layout, inside the click handler, on the same task as the rate write.
           Tabular figures plus a fixed box means the width never changes. */
        width: var(--pvsc-launcher-w);
        height: var(--pvsc-launcher-h);
        padding: 0;
        color: var(--pvsc-text);
        font-family: var(--pvsc-mono);
        font-size: var(--pvsc-body);
        font-weight: 600;
        font-variant-numeric: tabular-nums;
        background: rgba(10, 10, 11, 0.72);
        border: 1px solid var(--pvsc-line);
        border-radius: var(--pvsc-radius);
        cursor: grab;
        user-select: none;
        /* Required for pointer-event dragging on touch: without it the browser
           claims the gesture for panning and fires pointercancel. */
        touch-action: none;
        transform: translateX(var(--pvsc-launcher-shift));
        transition: transform 140ms cubic-bezier(0.2, 0.8, 0.2, 1),
                    background 160ms ease, border-color 160ms ease;
      }
      .pvsc-launcher:active { cursor: grabbing; }
      .pvsc-launcher:hover,
      .pvsc-launcher:focus-visible {
        background: var(--pvsc-ink);
        border-color: rgba(255, 255, 255, 0.28);
        outline: none;
      }
      .pvsc-launcher-dot {
        font-size: 11px;
        line-height: 1;
      }

      /* ── Panel shell ──────────────────────────────────────────────────── */
      .pvsc-panel {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        display: none;
        flex-direction: column;
        gap: var(--pvsc-gap);
        width: var(--pvsc-panel-w);
        padding: var(--pvsc-pad);
        background: var(--pvsc-ink);
        border: 1px solid var(--pvsc-line);
        border-radius: var(--pvsc-radius);
        box-shadow: 0 12px 32px rgba(0, 0, 0, 0.6);
        max-height: calc(100vh - 24px);
        overflow-y: auto;
        overscroll-behavior: contain;
      }
      #${ROOT_ID}.pvsc-menu-open .pvsc-panel {
        display: flex;
      }
      .pvsc-eyebrow {
        font-size: var(--pvsc-label);
        font-weight: 700;
        letter-spacing: 0.09em;
        text-transform: uppercase;
        color: var(--pvsc-dim);
      }
      .pvsc-rule {
        height: 1px;
        margin: 0;
        border: 0;
        background: var(--pvsc-line);
      }
      .pvsc-head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--pvsc-gap);
      }
      .pvsc-row {
        display: flex;
        align-items: center;
        gap: 6px;
      }
      .pvsc-label {
        font-size: var(--pvsc-label);
        letter-spacing: 0.06em;
        text-transform: uppercase;
        color: var(--pvsc-dim);
        flex-shrink: 0;
      }

      /* ── Shared control chrome ────────────────────────────────────────── */
      .pvsc-panel button,
      .pvsc-panel input {
        height: var(--pvsc-ctl);
        color: var(--pvsc-text);
        font-family: var(--pvsc-font);
        font-size: var(--pvsc-body);
        background: var(--pvsc-ink-2);
        border: 1px solid var(--pvsc-line);
        border-radius: var(--pvsc-radius);
        cursor: pointer;
        user-select: none;
        transition: background 120ms ease, border-color 120ms ease, color 120ms ease;
      }
      .pvsc-panel button:hover {
        background: var(--pvsc-ink-3);
      }
      .pvsc-panel button:focus-visible,
      .pvsc-panel input:focus-visible {
        outline: 2px solid var(--pvsc-live);
        outline-offset: 1px;
      }

      /* ── Speed: stepper + rail ────────────────────────────────────────── */
      .pvsc-stepper {
        display: inline-flex;
        align-items: center;
        gap: 4px;
      }
      .pvsc-stepper button {
        width: var(--pvsc-ctl);
        flex-shrink: 0;
        font-size: 15px;
        font-weight: 700;
        line-height: 1;
      }
      .pvsc-readout {
        min-width: 52px;
        text-align: center;
        font-family: var(--pvsc-mono);
        font-size: var(--pvsc-body);
        font-weight: 700;
        font-variant-numeric: tabular-nums;
        color: var(--pvsc-live);
      }
      .pvsc-rail {
        position: relative;
        display: grid;
        grid-template-columns: repeat(6, 1fr);
        gap: 2px;
        padding: 2px;
        background: var(--pvsc-ink-2);
        border: 1px solid var(--pvsc-line);
        border-radius: var(--pvsc-radius);
      }
      /* The signature element: emphasis by inversion, the way a caption block
         marks emphasis, on a strip that reads as a transport control. */
      .pvsc-rail-marker {
        position: absolute;
        top: 2px;
        left: 0;
        height: calc(100% - 4px);
        background: var(--pvsc-live);
        border-radius: 2px;
        opacity: 0;
        pointer-events: none;
        transition: transform 140ms cubic-bezier(0.2, 0.8, 0.2, 1),
                    width 140ms cubic-bezier(0.2, 0.8, 0.2, 1),
                    opacity 120ms ease;
      }
      .pvsc-rail button {
        position: relative;
        z-index: 1;
        height: calc(var(--pvsc-ctl) - 4px);
        padding: 0 2px;
        font-family: var(--pvsc-mono);
        font-size: var(--pvsc-body);
        font-variant-numeric: tabular-nums;
        background: transparent;
        border: 0;
        border-radius: 2px;
      }
      .pvsc-rail button:hover {
        background: rgba(255, 255, 255, 0.07);
      }
      .pvsc-rail button.pvsc-on {
        color: var(--pvsc-ink);
        font-weight: 700;
        background: transparent;
      }

      /* ── Subtitles ────────────────────────────────────────────────────── */
      .pvsc-switch {
        min-width: 46px;
        padding: 0 10px;
        font-size: var(--pvsc-label);
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }
      .pvsc-switch.pvsc-on {
        color: var(--pvsc-ink);
        background: var(--pvsc-live);
        border-color: var(--pvsc-live);
      }
      .pvsc-switch.pvsc-on:hover {
        background: var(--pvsc-live);
      }
      .pvsc-swatches {
        display: grid;
        grid-template-columns: repeat(5, 1fr);
        gap: 6px;
      }
      /* Scoped through .pvsc-panel so it outranks the shared button chrome
         above, which would otherwise force the swatch to the control height. */
      .pvsc-panel .pvsc-swatch {
        width: 100%;
        height: var(--pvsc-swatch);
        padding: 0;
        border: 2px solid rgba(255, 255, 255, 0.22);
        border-radius: var(--pvsc-radius);
      }
      .pvsc-swatch.pvsc-on {
        border-color: var(--pvsc-text);
        box-shadow: 0 0 0 2px var(--pvsc-live);
      }
      .pvsc-size-input {
        flex: 1;
        min-width: 0;
        padding: 0 4px;
        font-family: var(--pvsc-mono);
        font-variant-numeric: tabular-nums;
        text-align: center;
        -moz-appearance: textfield;
      }
      .pvsc-size-input::-webkit-outer-spin-button,
      .pvsc-size-input::-webkit-inner-spin-button {
        -webkit-appearance: none;
        margin: 0;
      }
      .pvsc-value-btn {
        flex: 1;
        min-width: 0;
        padding: 0 8px;
      }

      /* ── Actions ──────────────────────────────────────────────────────── */
      .pvsc-col {
        display: flex;
        flex-direction: column;
        gap: var(--pvsc-gap);
        min-width: 0;
      }
      .pvsc-cols {
        display: grid;
        grid-template-columns: repeat(var(--pvsc-cols), minmax(0, 1fr));
        gap: var(--pvsc-gap);
        align-items: start;
      }
      .pvsc-skip {
        width: 100%;
        padding: 0 8px;
      }
      .pvsc-stats {
        font-family: var(--pvsc-mono);
        font-size: var(--pvsc-label);
        font-variant-numeric: tabular-nums;
        color: var(--pvsc-dim);
        text-align: center;
      }

      /* ══ Touch: bigger targets, panel becomes a sheet ══════════════════ */
      @media ${TOUCH_QUERY} {
        #${ROOT_ID} {
          --pvsc-ctl: 44px;
          --pvsc-gap: 10px;
          --pvsc-pad: 16px;
          --pvsc-label: 11px;
          --pvsc-body: 14px;
          --pvsc-launcher-w: 84px;
          --pvsc-launcher-h: 44px;
          --pvsc-swatch: 40px;
        }
        /* On touch there is no hover to bring the control back, and a tap that
           passes through to the player toggles Prime's own overlay instead. So
           the idle state stays dimly visible and tappable rather than gone. */
        #${ROOT_ID}.pvsc-hidden {
          opacity: 0.32;
          pointer-events: auto;
        }
        .pvsc-launcher {
          background: rgba(10, 10, 11, 0.82);
          border-color: rgba(255, 255, 255, 0.24);
          box-shadow: 0 4px 14px rgba(0, 0, 0, 0.5);
        }
        .pvsc-panel {
          position: fixed;
          inset: auto 0 0 0;
          width: 100%;
          max-width: 100%;
          max-height: 82vh;
          border-radius: 12px 12px 0 0;
          border-bottom: 0;
          padding-bottom: max(var(--pvsc-pad), env(safe-area-inset-bottom, 0px));
          padding-left: max(var(--pvsc-pad), env(safe-area-inset-left, 0px));
          padding-right: max(var(--pvsc-pad), env(safe-area-inset-right, 0px));
          box-shadow: 0 -8px 32px rgba(0, 0, 0, 0.75);
        }
      }

      /* ══ Touch landscape: side sheet, two columns, no scrolling ════════ */
      @media (pointer: coarse) and (orientation: landscape),
             (hover: none) and (orientation: landscape),
             (max-width: 768px) and (orientation: landscape) {
        #${ROOT_ID} {
          --pvsc-ctl: 38px;
          --pvsc-gap: 7px;
          --pvsc-pad: 12px;
          --pvsc-body: 13px;
          --pvsc-swatch: 34px;
          --pvsc-panel-w: min(440px, 58vw);
          --pvsc-cols: 2;
        }
        .pvsc-panel {
          left: auto;
          width: var(--pvsc-panel-w);
          max-width: var(--pvsc-panel-w);
          /* Two columns is what makes this fit. The single-column sheet needed
             ~440px of height in a 360px-tall viewport, so it always scrolled —
             on the axis a phone in landscape has least of. */
          max-height: calc(100vh - 16px);
          border-radius: 12px 0 0 0;
        }
      }

      @media (prefers-reduced-motion: reduce) {
        #${ROOT_ID} *,
        #${ROOT_ID} {
          transition-duration: 0ms !important;
        }
      }
    `;
    document.documentElement.appendChild(style);
  }

  // ── 8. Panel DOM ───────────────────────────────────────────────────────────

  function clickSkipButtons() {
    let clicked = 0;
    for (const button of document.querySelectorAll(AUTO_SKIP_SELECTOR)) {
      if (isVisible(button)) {
        try {
          button.click();
          clicked += 1;
        } catch {}
      }
    }
    return clicked;
  }

  function makeButton(text, className, onClick) {
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

  let closeButtonCache = null;
  let closeButtonCacheKey = "";

  function findCloseButton() {
    // Sweeping every interactive element in Prime's DOM and calling
    // getComputedStyle on each is one of the most expensive things this script
    // can do, so the result is cached per document/viewport and only computed
    // when the panel actually has nowhere else to go.
    const cacheKey = `${location.pathname}|${window.innerWidth}x${window.innerHeight}`;
    if (closeButtonCacheKey === cacheKey && closeButtonCache?.isConnected) {
      return closeButtonCache;
    }

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
        closeButtonCacheKey = cacheKey;
        closeButtonCache = element;
        return element;
      }

      if (!fallback || rect.right > fallback.getBoundingClientRect().right) {
        fallback = element;
      }
    }

    closeButtonCacheKey = cacheKey;
    closeButtonCache = fallback;
    return fallback;
  }

  let savedPositionCache;

  function readSavedPosition() {
    if (savedPositionCache !== undefined) return savedPositionCache;
    try {
      const position = JSON.parse(readStored(POSITION_KEY) || "null");
      savedPositionCache = position && Number.isFinite(position.left) && Number.isFinite(position.top)
        ? position
        : null;
    } catch {
      savedPositionCache = null;
    }
    return savedPositionCache;
  }

  function clampPosition(left, top) {
    const rect = root.getBoundingClientRect();
    // The fallbacks matter: while no video is on screen the panel is
    // display:none and measures 0x0, and that is exactly when the saved
    // position is first restored. Reading the launcher tokens keeps the
    // fallback correct on both layouts instead of hard-coding desktop sizes.
    const styles = window.getComputedStyle(root);
    const width = rect.width || parseFloat(styles.getPropertyValue("--pvsc-launcher-w")) || 76;
    const height = rect.height || parseFloat(styles.getPropertyValue("--pvsc-launcher-h")) || 34;

    return {
      left: Math.min(Math.max(8, left), Math.max(8, window.innerWidth - width - 8)),
      top: Math.min(Math.max(8, top), Math.max(8, window.innerHeight - height - 8)),
    };
  }

  function setPosition(left, top, save) {
    const position = clampPosition(left, top);
    root.style.left = `${position.left}px`;
    root.style.top = `${position.top}px`;
    root.style.right = "auto";

    if (save) {
      savedPositionCache = position;
      persist(POSITION_KEY, JSON.stringify(position));
    }
  }

  let lastKnownMenuPos = null;

  function placeNearCloseButton(closeButton) {
    if (readSavedPosition()) {
      return;
    }

    const closeRect = closeButton.getBoundingClientRect();
    const rootRect = root.getBoundingClientRect();
    const width = rootRect.width || 76;
    const height = rootRect.height || 34;
    let left = closeRect.right + 10;

    if (left + width > window.innerWidth - 8) {
      left = closeRect.left - width - 10;
    }

    lastKnownMenuPos = { left, top: closeRect.top + (closeRect.height - height) / 2 };
    setPosition(lastKnownMenuPos.left, lastKnownMenuPos.top, false);
  }

  function getFullscreenElement() {
    return document.fullscreenElement
      || document.webkitFullscreenElement
      || document.mozFullScreenElement
      || document.msFullscreenElement
      || null;
  }

  /**
   * Keeps the panel inside whichever element is currently fullscreen.
   *
   * A fullscreen element is promoted to the browser's top layer, which renders
   * above the whole normal layer tree — `z-index` cannot compete with it. On
   * Android WebView the fullscreen subtree is handed to the host as a separate
   * view entirely. Either way a panel left on `documentElement` is invisible
   * for the whole of fullscreen playback, which on a phone is the only way
   * anyone watches. So it has to be re-parented into the fullscreen element,
   * and moved back out when fullscreen ends.
   */
  function ensureRootAttached() {
    const host = getFullscreenElement() || document.documentElement;
    if (root.parentNode !== host) {
      host.appendChild(root);
    }
  }

  function refresh() {
    ensureRootAttached();
    ensureAdShieldStyle();

    const video = findVideo();
    if (!video) {
      root.classList.add("pvsc-no-video");
      setMenuOpen(false);
      return;
    }

    root.classList.remove("pvsc-no-video");
    attachVideoListeners(video);

    if (!readSavedPosition()) {
      // The close-button heuristic assumes a desktop title bar and on a phone it
      // parks the panel on top of Prime's own control row (volume / close), where
      // the two fight for the same taps. Touch devices get a fixed safe corner
      // clear of that row instead; the panel is still draggable from there.
      const closeButton = isTouch ? null : findCloseButton();
      if (closeButton) {
        placeNearCloseButton(closeButton);
      } else if (isTouch) {
        // 110px down clears Prime's own top control row.
        const width = root.getBoundingClientRect().width || 84;
        setPosition(window.innerWidth - width - 12, 110, false);
      } else if (lastKnownMenuPos) {
        setPosition(lastKnownMenuPos.left, lastKnownMenuPos.top, false);
      } else {
        setPosition(window.innerWidth - 96, 78, false);
      }
    }

    updateSubtitleObserver();
    applySubtitleStyles(video);
    scheduleDiscovery();
  }

  // ── UI sync (rAF-coalesced) ────────────────────────────────────────────────

  let uiSyncHandle = 0;

  function scheduleUiSync() {
    if (uiSyncHandle) return;
    uiSyncHandle = window.requestAnimationFrame(() => {
      uiSyncHandle = 0;
      syncUi();
    });
  }

  function syncUi() {
    launcherValue.textContent = format(speed);
    if (subtitleEnabled) {
      launcherDot.textContent = "●";
      launcherDot.style.color = subtitleColor;
    } else {
      launcherDot.textContent = "○";
      launcherDot.style.color = "rgba(255,255,255,0.4)";
    }

    readout.textContent = format(speed);

    for (const stop of stopButtons) {
      const value = Number(stop.getAttribute("data-speed"));
      stop.classList.toggle("pvsc-on", Math.abs(value - speed) < 0.001);
    }
    updateRailMarker();

    subtitleSwitch.textContent = subtitleEnabled ? "On" : "Off";
    subtitleSwitch.classList.toggle("pvsc-on", subtitleEnabled);

    pitchSwitch.textContent = preservePitch ? "On" : "Off";
    pitchSwitch.classList.toggle("pvsc-on", preservePitch);

    const pct = parseInt(subtitleSize, 10);
    if (Number.isFinite(pct) && sizeInput.value !== String(pct) && document.activeElement !== sizeInput) {
      sizeInput.value = String(pct);
    }

    let bgLabel = "Shadow";
    if (subtitleBg === "solid") bgLabel = "Solid";
    else if (subtitleBg === "transparent") bgLabel = "None";
    bgButton.textContent = bgLabel;

    for (const swatch of swatchButtons) {
      const colorVal = swatch.getAttribute("data-color");
      swatch.classList.toggle("pvsc-on", subtitleEnabled && colorVal.toLowerCase() === subtitleColor.toLowerCase());
    }

    updateStatsDisplay();
    updateLauncherShift();
  }

  function updateRailMarker() {
    const index = PRESET_SPEEDS.findIndex((preset) => Math.abs(preset - speed) < 0.001);
    if (index < 0 || !isMenuOpen) {
      railMarker.style.opacity = "0";
      return;
    }
    const stop = stopButtons[index];
    // offsetLeft/offsetWidth are only meaningful once the panel is laid out,
    // which is why this runs from the rAF sync and on menu open rather than
    // straight out of the click handler.
    if (!stop.offsetWidth) {
      railMarker.style.opacity = "0";
      return;
    }
    // offsetLeft and `left: 0` are both resolved against the offsetParent's
    // padding edge — the rail is position:relative, so the two share an origin
    // and the offset can be used as the translation directly.
    railMarker.style.width = `${stop.offsetWidth}px`;
    railMarker.style.transform = `translateX(${stop.offsetLeft}px)`;
    railMarker.style.opacity = "1";
  }

  /**
   * Moves the launcher clear of the open panel when the two overlap.
   *
   * In touch landscape the sheet is anchored bottom-right and the launcher's
   * default corner sits inside it, so opening the panel used to bury its own
   * dismiss target — the only ways out were a tap on the remaining strip of
   * video, Escape, or the Android back button.
   *
   * Decided purely on measured geometry rather than on which breakpoint is
   * active. "Is the button covered" is a question the rects answer directly,
   * and reading them cannot fall out of step with the CSS the way a cached
   * orientation flag can.
   */
  function updateLauncherShift() {
    root.style.setProperty("--pvsc-launcher-shift", "0px");
    if (!isMenuOpen) return;

    const panelRect = panel.getBoundingClientRect();
    const buttonRect = launcher.getBoundingClientRect();
    if (panelRect.width === 0 || buttonRect.width === 0) return;

    const overlaps = buttonRect.right > panelRect.left
      && buttonRect.left < panelRect.right
      && buttonRect.bottom > panelRect.top
      && buttonRect.top < panelRect.bottom;
    if (!overlaps) return;

    // Clamped so shifting clear of the panel can never push it off the left edge.
    const wanted = panelRect.left - 8 - buttonRect.right;
    const shift = Math.max(wanted, 8 - buttonRect.left);
    root.style.setProperty("--pvsc-launcher-shift", `${Math.min(0, Math.round(shift))}px`);
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
      scheduleUiSync();
    } else {
      root.style.setProperty("--pvsc-launcher-shift", "0px");
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
  root.setAttribute("aria-label", "Prime Video speed and subtitle controls");

  const wrap = document.createElement("div");
  wrap.className = "pvsc-wrap";

  const launcher = document.createElement("button");
  launcher.type = "button";
  launcher.className = "pvsc-launcher";
  launcher.title = "Playback speed and subtitle controls";
  launcher.setAttribute("aria-haspopup", "true");
  const launcherValue = document.createElement("span");
  const launcherDot = document.createElement("span");
  launcherDot.className = "pvsc-launcher-dot";
  launcher.append(launcherValue, launcherDot);

  const panel = document.createElement("div");
  panel.className = "pvsc-panel";
  panel.setAttribute("role", "group");

  // ── Speed band ─────────────────────────────────────────────────────────────
  const speedHead = document.createElement("div");
  speedHead.className = "pvsc-head";
  const speedEyebrow = document.createElement("span");
  speedEyebrow.className = "pvsc-eyebrow";
  speedEyebrow.textContent = "Speed";

  const stepper = document.createElement("div");
  stepper.className = "pvsc-stepper";
  const readout = document.createElement("span");
  readout.className = "pvsc-readout";
  readout.setAttribute("aria-live", "polite");
  const stepDown = makeButton("−", "", () => setSpeed(speed - STEP));
  stepDown.setAttribute("aria-label", "Slower");
  const stepUp = makeButton("+", "", () => setSpeed(speed + STEP));
  stepUp.setAttribute("aria-label", "Faster");
  stepper.append(stepDown, readout, stepUp);
  speedHead.append(speedEyebrow, stepper);

  const rail = document.createElement("div");
  rail.className = "pvsc-rail";
  const railMarker = document.createElement("div");
  railMarker.className = "pvsc-rail-marker";
  rail.appendChild(railMarker);

  const stopButtons = PRESET_SPEEDS.map((preset) => {
    const stop = makeButton(formatStop(preset), "", () => setSpeed(preset));
    stop.setAttribute("data-speed", String(preset));
    stop.setAttribute("aria-label", `${formatStop(preset)} times speed`);
    rail.appendChild(stop);
    return stop;
  });

  panel.append(speedHead, rail);

  const rule = document.createElement("hr");
  rule.className = "pvsc-rule";
  panel.appendChild(rule);

  // ── Two-column region ──────────────────────────────────────────────────────
  const cols = document.createElement("div");
  cols.className = "pvsc-cols";

  const subsCol = document.createElement("div");
  subsCol.className = "pvsc-col";

  const subsHead = document.createElement("div");
  subsHead.className = "pvsc-head";
  const subsEyebrow = document.createElement("span");
  subsEyebrow.className = "pvsc-eyebrow";
  subsEyebrow.textContent = "Subtitles";
  const subtitleSwitch = makeButton("On", "pvsc-switch", () => setSubtitleEnabled(!subtitleEnabled));
  subtitleSwitch.setAttribute("aria-label", "Toggle subtitle styling");
  subsHead.append(subsEyebrow, subtitleSwitch);

  const swatches = document.createElement("div");
  swatches.className = "pvsc-swatches";
  const swatchButtons = PRESET_COLORS.map((presetColor) => {
    const swatch = makeButton("", "pvsc-swatch", () => setSubtitleColor(presetColor.hex));
    swatch.title = presetColor.name;
    swatch.setAttribute("aria-label", presetColor.name);
    swatch.style.backgroundColor = presetColor.hex;
    swatch.setAttribute("data-color", presetColor.hex);
    swatches.appendChild(swatch);
    return swatch;
  });

  const sizeRow = document.createElement("div");
  sizeRow.className = "pvsc-row";
  const sizeLabel = document.createElement("span");
  sizeLabel.className = "pvsc-label";
  sizeLabel.textContent = "Size";

  const sizeInput = document.createElement("input");
  sizeInput.className = "pvsc-size-input";
  sizeInput.type = "number";
  sizeInput.min = "50";
  sizeInput.max = "400";
  sizeInput.step = "10";
  sizeInput.value = String(parseInt(subtitleSize, 10) || 150);
  sizeInput.setAttribute("aria-label", "Subtitle size, percent");
  sizeInput.addEventListener("change", () => setSubtitleSize(sizeInput.value));
  sizeInput.addEventListener("keydown", (event) => {
    event.stopPropagation(); // prevent global hotkeys from firing while typing
    if (event.key === "Enter") { setSubtitleSize(sizeInput.value); sizeInput.blur(); }
    if (event.key === "Escape") { sizeInput.blur(); }
  });
  sizeInput.addEventListener("pointerdown", (event) => event.stopPropagation());
  sizeInput.addEventListener("click", (event) => { event.stopPropagation(); sizeInput.select(); });

  sizeRow.append(sizeLabel, sizeInput);

  const bgRow = document.createElement("div");
  bgRow.className = "pvsc-row";
  const bgLabel = document.createElement("span");
  bgLabel.className = "pvsc-label";
  bgLabel.textContent = "Backdrop";
  const bgButton = makeButton("Shadow", "pvsc-value-btn", () => cycleSubtitleBg());
  bgButton.setAttribute("aria-label", "Cycle subtitle backdrop");
  bgRow.append(bgLabel, bgButton);

  subsCol.append(subsHead, swatches, sizeRow, bgRow);

  const actionsCol = document.createElement("div");
  actionsCol.className = "pvsc-col";

  const pitchRow = document.createElement("div");
  pitchRow.className = "pvsc-row";
  const pitchLabel = document.createElement("span");
  pitchLabel.className = "pvsc-label";
  pitchLabel.textContent = "Pitch";
  const pitchSwitch = makeButton("On", "pvsc-switch", () => setPreservePitch(!preservePitch));
  pitchSwitch.title = "Keep voices at natural pitch. Turning this off makes speed changes smoother.";
  pitchSwitch.setAttribute("aria-label", "Toggle pitch correction");
  pitchRow.append(pitchLabel, pitchSwitch);

  // Skip was keyboard-only ("n"), i.e. unreachable on a phone. Same action,
  // given a button.
  const skipButton = makeButton("⏭ Skip intro", "pvsc-skip", () => clickSkipButtons());

  const statsRow = document.createElement("div");
  statsRow.className = "pvsc-stats";

  actionsCol.append(pitchRow, skipButton, statsRow);
  cols.append(subsCol, actionsCol);
  panel.appendChild(cols);

  wrap.append(launcher, panel);
  root.appendChild(wrap);
  document.documentElement.appendChild(root);

  const savedPosition = readSavedPosition();
  if (savedPosition) {
    setPosition(savedPosition.left, savedPosition.top, false);
  }

  // Whatever changes the panel's box — opening it, rotating the device, a
  // breakpoint switch, content reflowing — is a reason to re-check whether it
  // now covers the launcher. Watching the box itself catches all of them
  // without having to enumerate the triggers.
  let panelSizeObserver = null;
  if (typeof ResizeObserver === "function") {
    panelSizeObserver = new ResizeObserver(() => updateLauncherShift());
    panelSizeObserver.observe(panel);
  }

  syncUi();

  // ── 9. Scheduler & lifecycle ───────────────────────────────────────────────

  launcher.addEventListener("pointerdown", (event) => {
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
    launcher.setPointerCapture(event.pointerId);
  });

  launcher.addEventListener("pointermove", (event) => {
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

  launcher.addEventListener("pointerup", (event) => {
    if (!isDragging) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    isDragging = false;
    try {
      launcher.releasePointerCapture(event.pointerId);
    } catch {}

    if (dragStarted) {
      const rect = root.getBoundingClientRect();
      setPosition(rect.left, rect.top, true);
      dragStarted = false;
      return;
    }

    setMenuOpen(!isMenuOpen);
  });

  // Without this, a gesture the browser steals (scroll, multi-touch) leaves
  // isDragging stuck true, which permanently disables the auto-hide timer.
  launcher.addEventListener("pointercancel", (event) => {
    if (!isDragging) {
      return;
    }

    isDragging = false;
    try {
      launcher.releasePointerCapture(event.pointerId);
    } catch {}

    if (dragStarted) {
      const rect = root.getBoundingClientRect();
      setPosition(rect.left, rect.top, true);
      dragStarted = false;
    }
  });

  root.addEventListener("click", (event) => event.stopPropagation());
  // The panel is re-parented into the fullscreen player (see ensureRootAttached),
  // so without this every menu tap also reaches Prime's tap-to-toggle handler.
  root.addEventListener("pointerdown", (event) => event.stopPropagation());
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
      clickSkipButtons();
    } else if (event.key === "Escape") {
      setMenuOpen(false);
    }
  }, { capture: true, signal: lifecycleSignal });

  /**
   * Re-clamps the panel into the current viewport after it changes size.
   *
   * Never persists: rotation and the on-screen keyboard both fire resize, and
   * re-saving the squeezed coordinates would let a transient viewport
   * permanently overwrite the position the user actually chose.
   */
  function reflowPosition() {
    const saved = readSavedPosition();
    if (saved) {
      setPosition(saved.left, saved.top, false);
      return;
    }

    // While no video is on screen the panel is display:none and measures as a
    // zero rect at the origin. Repositioning from that would pin it to the
    // top-left corner for the rest of the session.
    const rect = root.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
      setPosition(rect.left, rect.top, false);
    }
  }

  function handleViewportChange() {
    isTouch = matchQuery(TOUCH_QUERY);
    closeButtonCacheKey = "";
    reflowPosition();
    refresh();
    scheduleUiSync();
  }

  window.addEventListener("resize", handleViewportChange, { signal: lifecycleSignal });

  // The layout used to be decided once, at install. A phone that was rotated,
  // or a desktop window that was resized across the breakpoint, kept whatever
  // the script guessed on the first frame while the CSS had already switched.
  const mediaWatchers = [TOUCH_QUERY, LANDSCAPE_QUERY].map((query) => {
    try {
      const list = window.matchMedia(query);
      list.addEventListener("change", handleViewportChange);
      return list;
    } catch {
      return null;
    }
  });

  for (const eventName of ["fullscreenchange", "webkitfullscreenchange"]) {
    document.addEventListener(eventName, () => {
      ensureRootAttached();
      reflowPosition();
      applySubtitleStyles();
      showControls();
    }, { signal: lifecycleSignal });
  }

  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      flushPersist();
      stopTick();
    } else {
      setTickRate(isAdCurrentlyActive ? TICK_AD_MS : TICK_IDLE_MS);
    }
  }, { signal: lifecycleSignal });

  window.addEventListener("pagehide", flushPersist, { signal: lifecycleSignal });

  // ── Tick ───────────────────────────────────────────────────────────────────

  let tickTimer = 0;
  let tickRate = 0;
  let refreshCounter = 0;

  function tick() {
    const video = findVideo();
    if (video) {
      attachVideoListeners(video);
      if (!isRateWriteUnsafe(video)) {
        alignDefaultRate(video, targetRate());
        writeRate(video, targetRate());
      }
    }
    checkAndHandleAds(video);

    // The heavier housekeeping (re-parenting, positioning, observer retarget)
    // only needs to run occasionally, so it rides on every other second of the
    // idle tick rather than owning a timer of its own.
    refreshCounter += 1;
    if (tickRate >= TICK_IDLE_MS || refreshCounter % 20 === 0) {
      refresh();
    }
  }

  function setTickRate(rate) {
    if (tickTimer && tickRate === rate) return;
    stopTick();
    tickRate = rate;
    tickTimer = window.setInterval(tick, rate);
  }

  function stopTick() {
    if (tickTimer) window.clearInterval(tickTimer);
    tickTimer = 0;
  }

  setTickRate(TICK_IDLE_MS);

  applySpeed();
  refresh();

  const controlApi = {
    installed: true,
    version: VERSION,
    applySpeed,
    refresh,
    applySubtitleStyles,
    checkAndHandleAds,
    clickSkipButtons,
    /**
     * Closes the menu if it is open. Returns whether it actually closed, so the
     * Android host can let the hardware Back button dismiss the menu first and
     * only fall through to history navigation when there was nothing to close.
     */
    closeMenu() {
      if (!isMenuOpen) {
        return false;
      }
      setMenuOpen(false);
      return true;
    },
    destroy() {
      stopTick();
      window.clearTimeout(hideTimer);
      window.clearTimeout(discoveryTimer);
      for (const timer of adWatchTimers) window.clearTimeout(timer);
      if (uiSyncHandle) window.cancelAnimationFrame(uiSyncHandle);
      flushPersist();
      lifecycleController.abort();
      for (const list of mediaWatchers) {
        try { list?.removeEventListener("change", handleViewportChange); } catch {}
      }
      subtitleObserver?.disconnect();
      videoSizeObserver?.disconnect();
      panelSizeObserver?.disconnect();
      detachVideoListeners(attachedVideo);
      subtitleRootChanged();
      clearCueStamps();

      if (attachedVideo && isAdCurrentlyActive) {
        attachedVideo.muted = wasMutedBeforeAd;
        isAdCurrentlyActive = false;
        alignDefaultRate(attachedVideo, speed);
        writeRate(attachedVideo, speed);
      }
      isAdCurrentlyActive = false;
      restoreHiddenVideos();
      removeAdCover();
      root.remove();
      document.getElementById(STYLE_ID)?.remove();
      document.getElementById(SUBTITLE_STYLE_ID)?.remove();
      document.getElementById(AD_SHIELD_STYLE_ID)?.remove();
      for (const token of ["--pvsc-sub-color", "--pvsc-sub-size", "--pvsc-sub-bg", "--pvsc-sub-shadow", "--pvsc-sub-pad", "--pvsc-sub-radius"]) {
        document.documentElement.style.removeProperty(token);
      }
      if (window.__primeVideoSpeedControl === controlApi) {
        delete window.__primeVideoSpeedControl;
      }
    }
  };
  window.__primeVideoSpeedControl = controlApi;

  return "installed";
})();
