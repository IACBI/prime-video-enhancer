(() => {
  const ROOT_ID = "pvsc-root";
  const STYLE_ID = "pvsc-style";
  const STORAGE_KEY = "primeVideoSpeedControl.speed";
  const POSITION_KEY = "primeVideoSpeedControl.position";
  const MIN_SPEED = 0.25;
  const MAX_SPEED = 4;
  const STEP = 0.1;
  const DEFAULT_SPEED = 1;
  const PRESET_SPEEDS = [0.5, 1, 1.25, 1.5, 1.75, 2];

  if (window.__primeVideoSpeedControl?.installed) {
    window.__primeVideoSpeedControl.refresh();
    window.__primeVideoSpeedControl.applySpeed();
    return "already-installed";
  }

  let speed = Number(window.localStorage.getItem(STORAGE_KEY));
  if (!Number.isFinite(speed) || speed < MIN_SPEED || speed > MAX_SPEED) {
    speed = DEFAULT_SPEED;
  }

  let hideTimer = 0;
  let isMenuOpen = false;
  let isDragging = false;
  let dragStarted = false;
  let dragOffsetX = 0;
  let dragOffsetY = 0;
  let lastPointerX = 0;
  let lastPointerY = 0;

  function clamp(value) {
    return Math.min(MAX_SPEED, Math.max(MIN_SPEED, value));
  }

  function format(value) {
    return `${value.toFixed(2).replace(/\.?0+$/, "")}x`;
  }

  function findVideo() {
    const videos = Array.from(document.querySelectorAll("video"));
    return videos.find((video) => video.readyState > 0) || videos[0] || null;
  }

  function applySpeed() {
    const video = findVideo();
    if (!video) {
      return;
    }

    if (video.playbackRate !== speed) {
      video.playbackRate = speed;
    }

    if (video.defaultPlaybackRate !== speed) {
      video.defaultPlaybackRate = speed;
    }
  }

  function setSpeed(nextSpeed) {
    speed = Number(clamp(nextSpeed).toFixed(2));
    window.localStorage.setItem(STORAGE_KEY, String(speed));
    speedButton.textContent = format(speed);
    updateActivePreset();
    applySpeed();
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
        transition: opacity 160ms ease;
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
        min-width: 46px;
        height: 38px;
        padding: 0 12px;
        color: #f7f7f8;
        font: inherit;
        cursor: grab;
        background: rgba(28, 31, 38, 0.82);
        border: 1px solid rgba(255, 255, 255, 0.22);
        border-radius: 10px;
        box-shadow: 0 6px 18px rgba(0, 0, 0, 0.28);
        user-select: none;
        backdrop-filter: blur(10px);
      }
      .pvsc-speed-button:active {
        cursor: grabbing;
      }
      .pvsc-menu {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        display: none;
        grid-template-columns: repeat(2, minmax(64px, auto));
        gap: 6px;
        min-width: 154px;
        padding: 8px;
        background: rgba(20, 22, 28, 0.94);
        border: 1px solid rgba(255, 255, 255, 0.16);
        border-radius: 12px;
        box-shadow: 0 14px 32px rgba(0, 0, 0, 0.42);
        backdrop-filter: blur(12px);
      }
      #${ROOT_ID}.pvsc-menu-open .pvsc-menu {
        display: grid;
      }
      .pvsc-menu button {
        width: 100%;
        height: 34px;
        padding: 0 12px;
        color: #f7f7f8;
        font: inherit;
        cursor: pointer;
        background: rgba(255, 255, 255, 0.08);
        border: 1px solid rgba(255, 255, 255, 0.14);
        border-radius: 8px;
        user-select: none;
      }
      .pvsc-speed-button:hover,
      .pvsc-speed-button:focus,
      .pvsc-menu button:hover,
      .pvsc-menu button:focus {
        background: rgba(255, 255, 255, 0.16);
        outline: none;
      }
      .pvsc-menu .pvsc-step {
        font-size: 17px;
      }
      .pvsc-menu .pvsc-active {
        color: #ffffff;
        border-color: rgba(99, 179, 237, 0.66);
        background: rgba(46, 118, 211, 0.58);
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
    const width = rect.width || 52;
    const height = rect.height || 36;

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
    const width = rootRect.width || 52;
    const height = rootRect.height || 36;
    let left = closeRect.right + 10;

    if (left + width > window.innerWidth - 8) {
      left = closeRect.left - width - 10;
    }

    setPosition(left, closeRect.top + (closeRect.height - height) / 2, false);
  }

  function refresh() {
    const video = findVideo();
    if (!video) {
      root.classList.add("pvsc-no-video");
      setMenuOpen(false);
      return;
    }

    root.classList.remove("pvsc-no-video");

    const closeButton = findCloseButton();
    if (closeButton) {
      placeNearCloseButton(closeButton);
    } else if (!readSavedPosition()) {
      setPosition(window.innerWidth - 76, 78, false);
    }
  }

  function updateActivePreset() {
    for (const button of menu.querySelectorAll("[data-speed]")) {
      const value = Number(button.getAttribute("data-speed"));
      button.classList.toggle("pvsc-active", Math.abs(value - speed) < 0.001);
    }
  }

  function showControls() {
    const video = findVideo();
    if (!video) {
      root.classList.add("pvsc-no-video");
      return;
    }

    root.classList.remove("pvsc-no-video");
    root.classList.remove("pvsc-hidden");
    window.clearTimeout(hideTimer);

    if (!video.paused && !isMenuOpen) {
      hideTimer = window.setTimeout(() => {
        root.classList.add("pvsc-hidden");
      }, 2400);
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

  const existingRoot = document.getElementById(ROOT_ID);
  if (existingRoot) {
    existingRoot.remove();
  }

  const root = document.createElement("div");
  root.id = ROOT_ID;
  root.className = "pvsc-no-video";
  root.setAttribute("aria-label", "Prime Video speed control");

  const wrap = document.createElement("div");
  wrap.className = "pvsc-wrap";

  const speedButton = document.createElement("button");
  speedButton.type = "button";
  speedButton.className = "pvsc-speed-button";
  speedButton.textContent = format(speed);
  speedButton.title = "Playback speed";

  const menu = document.createElement("div");
  menu.className = "pvsc-menu";

  menu.appendChild(makeMenuButton("-", "pvsc-step", () => setSpeed(speed - STEP)));
  menu.appendChild(makeMenuButton("+", "pvsc-step", () => setSpeed(speed + STEP)));

  for (const preset of PRESET_SPEEDS) {
    const presetButton = makeMenuButton(format(preset), "", () => {
      setSpeed(preset);
      setMenuOpen(false);
    });
    presetButton.setAttribute("data-speed", String(preset));
    menu.appendChild(presetButton);
  }

  wrap.append(speedButton, menu);
  root.appendChild(wrap);
  document.documentElement.appendChild(root);

  const savedPosition = readSavedPosition();
  if (savedPosition) {
    setPosition(savedPosition.left, savedPosition.top, false);
  }

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
  window.setInterval(applySpeed, 1000);
  window.setInterval(refresh, 1000);

  updateActivePreset();
  applySpeed();
  refresh();

  window.__primeVideoSpeedControl = {
    installed: true,
    applySpeed,
    refresh,
  };

  return "installed";
})();
