// Behaviour test for speed-control.js in a real (headless) Chromium browser.
//
//   node PrimeVideoSpeedApp.Tests/controller-headless.js
//
// Unlike browser-smoke.js this needs no running app and no Prime Video account:
// it starts its own Edge on a throwaway profile, serves a small fixture page that
// looks enough like Prime's player for the controller to attach, and drives the
// controller the way a user and the page would. Set PVSC_BROWSER to use another
// Chromium-based browser.
import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import http from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const script = readFileSync(join(here, "..", "speed-control.js"), "utf8");
const version = /const VERSION = "([^"]+)"/.exec(script)?.[1];
if (!version) throw new Error("Could not read VERSION out of speed-control.js.");

function findBrowser() {
  const candidates = [
    process.env.PVSC_BROWSER,
    process.env["ProgramFiles(x86)"] && join(process.env["ProgramFiles(x86)"], "Microsoft", "Edge", "Application", "msedge.exe"),
    process.env.ProgramFiles && join(process.env.ProgramFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
    "/usr/bin/microsoft-edge",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/usr/bin/chromium-browser"
  ];
  const found = candidates.find((candidate) => candidate && existsSync(candidate));
  if (!found) throw new Error("No Chromium-based browser found. Set PVSC_BROWSER to its path.");
  return found;
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function waitFor(check, description, timeoutMs = 15000) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const value = await check();
    if (value) return value;
    if (Date.now() > deadline) throw new Error(`Timed out waiting for ${description}.`);
    await sleep(100);
  }
}

function check(condition, message) {
  if (!condition) throw new Error(message);
}

// Player-shaped fixture: a video inside Prime's container class, and a caption
// block low in the picture. Positions are absolute so the geometry gates in the
// controller see the same layout on every machine.
const fixturePage = (videoAttributes) => `<!doctype html><html><head>
<meta name="viewport" content="width=device-width, initial-scale=1"></head><body style="margin:0;overflow:hidden">
<div id="player" class="webPlayerSDKContainer" style="position:relative;width:800px;height:450px">
  <video id="v" ${videoAttributes} style="position:absolute;left:0;top:0;width:800px;height:450px"></video>
  <div class="atvwebplayersdk-subtitle-text" style="position:absolute;left:150px;top:380px;width:500px;height:40px"><span id="staticCue">Existing line</span></div>
</div></body></html>`;

// 30 seconds of 8 kHz mono silence: enough for a real timeline without shipping a
// binary fixture.
function makeWav(seconds) {
  const rate = 8000;
  const dataBytes = rate * seconds * 2;
  const header = Buffer.alloc(44);
  header.write("RIFF", 0);
  header.writeUInt32LE(36 + dataBytes, 4);
  header.write("WAVEfmt ", 8);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(rate, 24);
  header.writeUInt32LE(rate * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36);
  header.writeUInt32LE(dataBytes, 40);
  return Buffer.concat([header, Buffer.alloc(dataBytes)]);
}
const wav = makeWav(30);

const server = http.createServer((request, response) => {
  if (request.url === "/tone.wav") {
    const range = /bytes=(\d*)-(\d*)/.exec(request.headers.range ?? "");
    const start = range && range[1] ? Number(range[1]) : 0;
    const end = range && range[2] ? Number(range[2]) : wav.length - 1;
    response.writeHead(range ? 206 : 200, {
      "content-type": "audio/wav",
      "accept-ranges": "bytes",
      "content-length": end - start + 1,
      ...(range ? { "content-range": `bytes ${start}-${end}/${wav.length}` } : {})
    });
    response.end(wav.subarray(start, end + 1));
    return;
  }
  response.writeHead(200, { "content-type": "text/html" });
  response.end(fixturePage(request.url === "/play" ? `src="/tone.wav" loop muted` : ""));
});
await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const baseUrl = `http://127.0.0.1:${server.address().port}/`;

const profile = mkdtempSync(join(tmpdir(), "pvsc-headless-"));
const browser = spawn(findBrowser(), [
  "--headless=new",
  "--remote-debugging-port=0",
  "--remote-debugging-address=127.0.0.1",
  `--user-data-dir=${profile}`,
  "--no-first-run",
  "--no-default-browser-check",
  "--disable-extensions",
  "--disable-sync",
  "--disable-background-networking",
  "--disable-gpu",
  // The panel's wording follows the browser language; pin it so the assertions
  // read the same on every machine.
  "--lang=en-US",
  "--autoplay-policy=no-user-gesture-required",
  "about:blank"
], { stdio: "ignore" });

let debugPort = 0;

async function browserIsUp() {
  try {
    await fetch(`http://127.0.0.1:${debugPort}/json/version`);
    return true;
  } catch {
    return false;
  }
}

async function shutdown() {
  server.close();

  // The process spawned above is only a launcher: msedge.exe hands the session to
  // a detached browser process and exits, so its pid is useless for cleanup and
  // killing it would orphan the real browser. Ask the browser to close itself.
  if (debugPort && (await browserIsUp())) {
    try {
      const { webSocketDebuggerUrl } = await fetch(`http://127.0.0.1:${debugPort}/json/version`).then((response) => response.json());
      const control = new WebSocket(webSocketDebuggerUrl);
      await new Promise((resolve) => {
        control.addEventListener("open", () => {
          control.send(JSON.stringify({ id: 1, method: "Browser.close" }));
          resolve();
        }, { once: true });
        control.addEventListener("error", resolve, { once: true });
      });
    } catch {
      // Fall through to the forced cleanup below.
    }
    for (let attempt = 0; attempt < 50 && (await browserIsUp()); attempt += 1) await sleep(100);
  }

  // Anything still holding the profile is matched by its command line.
  if (debugPort && (await browserIsUp())) {
    if (process.platform === "win32") {
      spawnSync("powershell", ["-NoProfile", "-Command",
        `Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*${profile}*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }`], { stdio: "ignore" });
    } else {
      spawnSync("pkill", ["-9", "-f", profile], { stdio: "ignore" });
    }
    await sleep(500);
  }

  // The browser process can outlive its debugging endpoint by a moment, and
  // Windows will not delete a directory while a handle into it is still open.
  for (let attempt = 0; ; attempt += 1) {
    try {
      rmSync(profile, { recursive: true, force: true });
      return;
    } catch (error) {
      if (attempt >= 40) {
        console.warn(`Could not delete the temporary profile ${profile}: ${error.code}`);
        return;
      }
      await sleep(250);
    }
  }
}

let failures = 0;
try {
  const port = debugPort = await waitFor(() => {
    try {
      return Number(readFileSync(join(profile, "DevToolsActivePort"), "utf8").split("\n")[0]) || 0;
    } catch {
      return 0;
    }
  }, "the browser to publish its debugging port");

  const targets = await waitFor(async () => {
    try {
      const list = await fetch(`http://127.0.0.1:${port}/json`).then((response) => response.json());
      return list.some((target) => target.type === "page") ? list : null;
    } catch {
      return null;
    }
  }, "a page target");
  const target = targets.find((candidate) => candidate.type === "page");

  const socket = new WebSocket(target.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    const request = pending.get(message.id);
    if (!request) return;
    pending.delete(message.id);
    if (message.error) request.reject(new Error(message.error.message));
    else request.resolve(message.result);
  });
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", () => reject(new Error("CDP WebSocket connection failed.")), { once: true });
  });

  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });

  async function evaluate(expression) {
    const result = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    }
    return result.result.value;
  }

  async function open(url) {
    await send("Page.navigate", { url });
    await waitFor(async () => (await evaluate("document.readyState")) === "complete" && (await evaluate("location.href")) === url, `${url} to load`);
  }

  const press = (key) => evaluate(
    `document.dispatchEvent(new KeyboardEvent("keydown", { key: ${JSON.stringify(key)}, bubbles: true, cancelable: true })), true`);
  const install = () => evaluate(script);
  const control = "window.__primeVideoSpeedControl";
  const css = (element, property) => evaluate(`getComputedStyle(${element}).${property}`);
  const byLabel = (label) => `document.querySelector('[aria-label="${label}"]')`;

  /** A clean page with the controller installed and no settings left over from an earlier test. */
  async function freshPage(path = "") {
    await open(baseUrl + path);
    await evaluate("localStorage.clear()");
    check((await install()) === "installed", "controller did not install on a fresh page");
  }

  async function test(name, run) {
    try {
      await run();
      console.log(`PASS ${name}`);
    } catch (error) {
      failures += 1;
      console.error(`FAIL ${name}: ${error.message}`);
    }
  }

  // ── Ad helpers ───────────────────────────────────────────────────────────────

  const adState = () => evaluate(`(() => {
    const v = document.getElementById("v");
    return { muted: v.muted, rate: v.playbackRate, hidden: v.style.opacity === "0", cover: !!document.getElementById("pvsc-ad-freeze-canvas") };
  })()`);

  async function showAd(text) {
    await evaluate(`(() => {
      const ad = document.createElement("div");
      ad.id = "ad"; ad.className = "atvwebplayersdk-ad-timer-countdown"; ad.textContent = ${JSON.stringify(text)};
      ad.style.cssText = "position:absolute;left:10px;top:10px;width:60px;height:20px";
      document.getElementById("player").appendChild(ad);
      ${control}.checkAndHandleAds();
      return true;
    })()`);
  }

  async function endAd() {
    await evaluate(`document.getElementById("ad")?.remove(), ${control}.checkAndHandleAds(), true`);
    await sleep(500);
    await evaluate(`${control}.checkAndHandleAds()`);
  }

  // ── Install, subtitles, speed, ad shield, teardown ───────────────────────────

  await open(baseUrl);

  await test("installs once and is idempotent", async () => {
    check((await install()) === "installed", "first injection did not report 'installed'");
    check((await evaluate(`${control}.version`)) === version, "installed version does not match the script");
    check((await evaluate(`document.querySelectorAll("#pvsc-root").length`)) === 1, "expected one panel");
    check(!(await evaluate(`document.getElementById("pvsc-root").classList.contains("pvsc-no-video")`)), "panel stayed hidden although a video is present");
    check((await install()) === "already-installed", "second injection did not report 'already-installed'");
    check((await evaluate(`document.querySelectorAll("#pvsc-root").length`)) === 1, "second injection duplicated the panel");
  });

  // refresh() runs every idle tick. Rewriting the stylesheet each time forces a
  // document-wide style recalculation once a second for the whole session.
  await test("subtitle stylesheet is not rewritten while idle", async () => {
    await evaluate(`(() => {
      window.__styleWrites = 0;
      window.__styleObserver = new MutationObserver((records) => { window.__styleWrites += records.length; });
      window.__styleObserver.observe(document.getElementById("pvsc-subtitle-style"), { childList: true, characterData: true, subtree: true });
      return true;
    })()`);
    await sleep(3500);
    const writes = await evaluate("window.__styleWrites");
    await evaluate("window.__styleObserver.disconnect()");
    check(writes === 0, `stylesheet was rewritten ${writes} times in 3.5s`);
  });

  await test("subtitle toggle empties and restores the stylesheet", async () => {
    const length = () => evaluate(`document.getElementById("pvsc-subtitle-style").textContent.length`);
    const enabled = await length();
    check(enabled > 500, "stylesheet is unexpectedly small while enabled");
    await press("s");
    check((await length()) === 0, "stylesheet was not cleared when subtitles were switched off");
    await press("s");
    check((await length()) === enabled, "stylesheet was not restored when subtitles were switched on");
  });

  await test("existing caption line takes the configured colour", async () => {
    check((await css(`document.getElementById("staticCue")`, "color")) === "rgb(255, 204, 0)", "caption did not take the default yellow");
    check((await css(`document.getElementById("staticCue")`, "fontWeight")) === "700", "caption did not take the bold weight");
  });

  await test("new cue is styled, a title near the top is left alone", async () => {
    await evaluate(`(() => {
      const player = document.getElementById("player");
      const cue = document.createElement("div");
      cue.id = "newCue"; cue.className = "timedText"; cue.textContent = "Fresh line";
      cue.style.cssText = "position:absolute;left:150px;top:330px;width:500px;height:40px";
      const title = document.createElement("div");
      title.id = "title"; title.className = "timedText"; title.textContent = "Episode title";
      title.style.cssText = "position:absolute;left:100px;top:20px;width:500px;height:40px";
      player.append(cue, title);
      return true;
    })()`);
    await waitFor(() => evaluate(`document.getElementById("newCue").hasAttribute("data-pvsc-sub-cue")`), "the new cue to be stamped", 3000);
    check((await css(`document.getElementById("newCue")`, "color")) === "rgb(255, 204, 0)", "new cue was not coloured");
    await sleep(300);
    check(!(await evaluate(`document.getElementById("title").hasAttribute("data-pvsc-sub-cue")`)), "the episode title was stamped as a subtitle");
    check((await css(`document.getElementById("title")`, "color")) !== "rgb(255, 204, 0)", "the episode title picked up the subtitle colour");
  });

  await test("speed keys change the rate and are remembered", async () => {
    const rate = () => evaluate(`document.getElementById("v").playbackRate`);
    check((await rate()) === 1, "initial rate is not 1x");
    await press("]");
    check(Math.abs((await rate()) - 1.1) < 1e-9, "']' did not step the rate to 1.1x");
    await sleep(500);
    check((await evaluate(`localStorage.getItem("primeVideoSpeedControl.speed")`)) === "1.1", "speed was not persisted");
    await press("\\");
    check((await rate()) === 1, "'\\' did not reset the rate");
    await press("-");
    check(Math.abs((await rate()) - 0.9) < 1e-9, "'-' did not step the rate down");
    await press("\\");
  });

  await test("ad shield engages on a countdown and releases afterwards", async () => {
    await showAd("0:27");
    const engaged = await adState();
    // 16x, or the 8x fallback: a video with no source fires 'waiting' as soon as
    // the shield asks it to play, which is exactly the stall the fallback is for.
    check(engaged.muted && (engaged.rate === 16 || engaged.rate === 8) && engaged.hidden && engaged.cover, `shield did not engage: ${JSON.stringify(engaged)}`);

    await endAd();
    const released = await adState();
    check(!released.muted && released.rate === 1 && !released.hidden && !released.cover, `shield did not release: ${JSON.stringify(released)}`);
    await sleep(400);
    check(/^1 ads blocked/.test(await evaluate(`document.querySelector(".pvsc-stats").textContent`)), "the ad was not counted");
  });

  await test("ad shield ignores look-alike UI", async () => {
    await evaluate(`(() => {
      for (const name of ["loadTimer", "threadIndicator", "broadBreak"]) {
        const el = document.createElement("div");
        el.className = name; el.textContent = "0:27";
        el.style.cssText = "position:absolute;left:10px;top:60px;width:60px;height:20px";
        document.getElementById("player").appendChild(el);
      }
      ${control}.checkAndHandleAds();
      return true;
    })()`);
    const v = await evaluate(`({ muted: document.getElementById("v").muted, rate: document.getElementById("v").playbackRate, cover: !!document.getElementById("pvsc-ad-freeze-canvas") })`);
    check(!v.muted && v.rate === 1 && !v.cover, `shield engaged on ordinary UI: ${JSON.stringify(v)}`);
  });

  await test("destroy leaves nothing behind and the controller can reinstall", async () => {
    await evaluate(`${control}.destroy()`);
    const leftovers = await evaluate(`({
      globals: typeof window.__primeVideoSpeedControl,
      nodes: document.querySelectorAll("#pvsc-root, #pvsc-style, #pvsc-subtitle-style, #pvsc-ad-shield-style, #pvsc-ad-freeze-canvas, [data-pvsc-sub-cue], [data-pvsc-sub-root]").length,
      token: document.documentElement.style.getPropertyValue("--pvsc-sub-color") + document.documentElement.style.getPropertyValue("--pvsc-sub-lift")
    })`);
    check(leftovers.globals === "undefined" && leftovers.nodes === 0 && leftovers.token === "", `destroy left state behind: ${JSON.stringify(leftovers)}`);
    check((await install()) === "installed", "could not reinstall after destroy");
    await evaluate(`${control}.destroy()`);
  });

  // ── Ad detection in the languages Prime ships in ─────────────────────────────

  await test("ad countdown is recognised in other languages, and only as a countdown", async () => {
    await freshPage();
    for (const text of ["0:27", "27 s", "27 sec", "27 Sek.", "27 Sekunden", "27 seg", "27 secondes", "27 secondi", "27 saniye", "27 сек", "27 秒"]) {
      await showAd(text);
      const state = await adState();
      check(state.cover && state.muted, `'${text}' did not engage the shield`);
      await endAd();
    }
    for (const text of ["0:00", "0 Sek.", "Werbung", "3 Sekundenzeiger", "27 segway", ""]) {
      await showAd(text);
      const state = await adState();
      check(!state.cover && !state.muted, `'${text}' engaged the shield`);
      await evaluate(`document.getElementById("ad")?.remove(), true`);
    }
  });

  await test("ad skip buttons are pressed in other languages, plain skip controls only during an ad", async () => {
    await freshPage();
    await evaluate(`(() => {
      window.__skipClicks = [];
      for (const label of ["Werbung überspringen", "Passer la publicité", "Omitir anuncio", "Salta annuncio", "Pular anúncio", "Reklamı atla"]) {
        const button = document.createElement("button");
        button.setAttribute("aria-label", label);
        button.style.cssText = "position:absolute;left:10px;top:120px;width:90px;height:30px";
        button.addEventListener("click", () => window.__skipClicks.push(label));
        document.getElementById("player").appendChild(button);
      }
      return true;
    })()`);
    // An ordinary player control that merely says "skip" must be left alone
    // outside an ad break.
    await evaluate(`(() => {
      window.__forwardClicks = 0;
      const button = document.createElement("button");
      button.title = "Skip forward 10 seconds";
      button.style.cssText = "position:absolute;left:10px;top:160px;width:90px;height:30px";
      button.addEventListener("click", () => { window.__forwardClicks += 1; });
      document.getElementById("player").appendChild(button);
      return true;
    })()`);
    await sleep(1300);
    check((await evaluate("window.__forwardClicks")) === 0, "a plain skip-forward control was pressed outside an ad");
    await showAd("0:27");
    await sleep(200);
    const clicked = await evaluate("window.__skipClicks.length");
    check(clicked === 6, `expected all six localized ad-skip buttons to be pressed, got ${clicked}`);
    check((await evaluate("window.__forwardClicks")) === 1, "the generic skip control was not pressed during the ad");
    await endAd();
  });

  // ── Auto-skip ────────────────────────────────────────────────────────────────

  await test("auto-skip presses Skip intro, and can be switched off from the panel", async () => {
    await freshPage();
    const addSkip = (id) => evaluate(`(() => {
      window.__introClicks = window.__introClicks || 0;
      const button = document.createElement("button");
      button.id = ${JSON.stringify(id)}; button.className = "atvwebplayersdk-skip-button"; button.textContent = "Skip";
      button.style.cssText = "position:absolute;left:10px;top:200px;width:90px;height:30px";
      button.addEventListener("click", () => { window.__introClicks += 1; });
      document.getElementById("player").appendChild(button);
      ${control}.checkAndHandleAds();
      return true;
    })()`);
    const clicks = () => evaluate("window.__introClicks");

    await addSkip("first");
    check((await clicks()) === 1, "Skip intro was not pressed automatically");

    await evaluate(`${byLabel("Toggle automatic skipping")}.click()`);
    await sleep(400);
    check((await evaluate(`localStorage.getItem("primeVideoSpeedControl.autoSkip")`)) === "false", "the switch was not persisted");
    await addSkip("second");
    await sleep(200);
    check((await clicks()) === 1, "Skip intro was pressed although auto-skip is off");

    await press("n");
    check((await clicks()) >= 2, "the manual shortcut stopped working while auto-skip is off");

    const before = await clicks();
    await evaluate(`${byLabel("Toggle automatic skipping")}.click()`);
    await waitFor(async () => (await clicks()) > before, "auto-skip to resume when switched back on", 2000);
  });

  // ── Subtitle raise ───────────────────────────────────────────────────────────

  await test("subtitles can be raised, cleared and are never raised twice", async () => {
    await freshPage();
    const setLift = (value) => evaluate(`(() => {
      const input = ${byLabel("Raise subtitles, percent of the picture height")};
      input.value = ${JSON.stringify(String(value))};
      input.dispatchEvent(new Event("change", { bubbles: true }));
      return true;
    })()`);
    await evaluate(`(() => {
      const player = document.getElementById("player");
      const wrapper = document.createElement("div");
      wrapper.id = "wrapper"; wrapper.setAttribute("data-pvsc-sub-root", "");
      wrapper.style.cssText = "position:absolute;left:100px;top:300px;width:600px;height:100px";
      const nested = document.createElement("div");
      nested.id = "nested"; nested.className = "atvwebplayersdk-subtitle-text"; nested.textContent = "Nested line";
      wrapper.appendChild(nested);
      player.appendChild(wrapper);
      return true;
    })()`);
    const caption = `document.getElementById("staticCue").parentElement`;

    check((await css(caption, "translate")) === "none", "a caption moved although nothing was raised");
    check(!(await evaluate(`document.getElementById("pvsc-subtitle-style").textContent.includes("translate")`)), "the stylesheet carries a positioning rule at zero lift");

    await setLift(10);
    // The picture is the 800x450 fixture, so ten percent of it is 45px.
    check((await css(caption, "translate")) === "0px -45px", "the caption was not raised by ten percent of the picture");
    check((await css(`document.getElementById("wrapper")`, "translate")) === "0px -45px", "the stamped caption container was not raised");
    check((await css(`document.getElementById("nested")`, "translate")) === "none", "a caption inside a raised container was raised a second time");

    await setLift(90);
    await sleep(400);
    check((await evaluate(`localStorage.getItem("primeVideoSpeedControl.subtitleLift")`)) === "40", "the raise was not clamped to 40 percent");

    await setLift(0);
    check((await css(caption, "translate")) === "none", "the caption stayed raised at zero");
    check(!(await evaluate(`document.getElementById("pvsc-subtitle-style").textContent.includes("translate")`)), "the positioning rule stayed in the stylesheet at zero lift");
  });

  // ── Real playback: time saved by speed, sleep timer ──────────────────────────

  const playing = () => evaluate(`(() => { const v = document.getElementById("v"); return !v.paused && v.readyState >= 3 && v.currentTime > 0.2; })()`);
  const paused = () => evaluate(`document.getElementById("v").paused`);
  const savedSeconds = () => evaluate(`${control}.stats().speedSavedSecs`);

  await test("time saved by speed counts only real, faster-than-normal playback", async () => {
    await freshPage("play");
    await evaluate(`document.getElementById("v").play()`);
    await waitFor(playing, "the media to start playing");

    // At 1x nothing is saved.
    await sleep(2500);
    check((await savedSeconds()) < 0.05, "time was credited at normal speed");

    await evaluate(`document.querySelector('.pvsc-rail button[data-speed="2"]').click()`);
    check((await evaluate(`document.getElementById("v").playbackRate`)) === 2, "the 2x preset did not apply");
    const start = await savedSeconds();
    await sleep(5500);
    const gained = (await savedSeconds()) - start;
    // Half of every real second is saved at 2x; ticks land on whole seconds.
    check(gained > 1.5 && gained < 3.5, `expected about 2.75s saved after 5.5s at 2x, got ${gained.toFixed(2)}`);

    await evaluate(`document.getElementById("v").pause()`);
    await sleep(300);
    const whenPaused = await savedSeconds();
    await sleep(2500);
    check((await savedSeconds()) - whenPaused < 0.05, "time was credited while paused");

    // The figure survives the page going away and shows in the panel.
    await evaluate(`${control}.destroy()`);
    const stored = Number(await evaluate(`localStorage.getItem("primeVideoSpeedControl.speedTimeSavedSecs")`));
    check(stored >= 1, `the saved time was not persisted (stored ${stored})`);
    await install();
    check((await savedSeconds()) >= 1, "the saved time was not restored on the next install");
    check(/saved by speed/.test(await evaluate(`document.querySelector(".pvsc-stats").textContent`)), "the saved time is missing from the panel");
  });

  await test("sleep timer pauses playback, cycles through its presets and counts down", async () => {
    await freshPage("play");
    await evaluate(`document.getElementById("v").play()`);
    await waitFor(playing, "the media to start playing");

    const label = () => evaluate(`${byLabel("Sleep timer")}.textContent`);
    const labelIs = (text) => waitFor(async () => (await label()) === text, `the sleep label to read '${text}'`, 2000);

    await labelIs("Off");
    for (const expected of ["15m", "30m", "45m", "60m", "90m", "Off"]) {
      await evaluate(`${byLabel("Sleep timer")}.click()`);
      await labelIs(expected);
    }
    check(await playing(), "changing the timer interrupted playback");

    await evaluate(`${control}.setSleepTimer(0.03)`);
    await labelIs("1m");
    check(await playing(), "the timer fired early");
    await waitFor(paused, "the timer to pause playback", 6000);
    await labelIs("Off");

    // An ad break does not use up the timer: it waits for the episode to be back.
    await evaluate(`document.getElementById("v").play()`);
    await waitFor(playing, "playback to resume");
    await evaluate(`${control}.setSleepTimer(0.03)`);
    await showAd("0:27");
    await sleep(2600);
    check(!(await paused()), "the timer paused the ad instead of waiting for it to end");
    await endAd();
    await waitFor(paused, "the timer to pause once the ad ended", 5000);
  });

  // ── Language and layout ──────────────────────────────────────────────────────

  await test("the panel speaks Turkish when the browser does, and English otherwise", async () => {
    await open(baseUrl);
    const userAgent = await evaluate("navigator.userAgent");
    await send("Emulation.setUserAgentOverride", { userAgent, acceptLanguage: "tr-TR" });
    try {
      await freshPage();
      check((await evaluate("navigator.language")) === "tr-TR", "the language override did not take effect");
      check((await evaluate(`document.querySelector(".pvsc-launcher").title`)) === "Oynatma hızı ve altyazı kontrolleri", "launcher title is not Turkish");
      check((await evaluate(`${byLabel("Altyazı stilini aç/kapat")}.textContent`)) === "Açık", "subtitle switch is not Turkish");
      check((await evaluate(`document.querySelector(".pvsc-swatch").getAttribute("aria-label")`)) === "Sarı", "swatch name is not Turkish");
      check(/reklam engellendi/.test(await evaluate(`document.querySelector(".pvsc-stats").textContent`)), "the statistics are not Turkish");
    } finally {
      await send("Emulation.setUserAgentOverride", { userAgent, acceptLanguage: "en-US" });
    }
    await freshPage();
    check((await evaluate(`document.querySelector(".pvsc-launcher").title`)) === "Playback speed and subtitle controls", "English did not come back");
  });

  async function openMenu() {
    const point = await evaluate(`(() => { const r = document.querySelector(".pvsc-launcher").getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 }; })()`);
    await send("Input.dispatchMouseEvent", { type: "mouseMoved", x: point.x, y: point.y });
    await send("Input.dispatchMouseEvent", { type: "mousePressed", x: point.x, y: point.y, button: "left", clickCount: 1 });
    await send("Input.dispatchMouseEvent", { type: "mouseReleased", x: point.x, y: point.y, button: "left", clickCount: 1 });
    await waitFor(() => evaluate(`document.getElementById("pvsc-root").classList.contains("pvsc-menu-open")`), "the menu to open", 2000);
    await sleep(250);
  }

  // Portrait has to show every row at once. A phone held sideways has too little
  // height for all of them, so there the sheet is allowed to scroll, but it must
  // stay on screen, keep its text unclipped and remain scrollable.
  for (const [name, language, viewport] of [
    ["phone landscape, English", "en-US", { width: 724, height: 332 }],
    ["phone landscape, Turkish", "tr-TR", { width: 724, height: 332 }],
    ["phone portrait, English", "en-US", { width: 390, height: 780 }],
    ["phone portrait, Turkish", "tr-TR", { width: 390, height: 780 }]
  ]) {
    await test(`the panel fits the screen: ${name}`, async () => {
      await open(baseUrl);
      const userAgent = await evaluate("navigator.userAgent");
      await send("Emulation.setUserAgentOverride", { userAgent, acceptLanguage: language });
      await send("Emulation.setDeviceMetricsOverride", { ...viewport, deviceScaleFactor: 1, mobile: true });
      await send("Emulation.setTouchEmulationEnabled", { enabled: true });
      try {
        await freshPage();
        await openMenu();
        const fit = await evaluate(`(() => {
          const panel = document.querySelector(".pvsc-panel");
          const overflowing = [...panel.querySelectorAll("button, input, span")].filter((el) => el.scrollWidth > el.clientWidth + 1 && el.clientWidth > 0)
            .map((el) => el.className + ":" + el.textContent);
          return { vertical: panel.scrollHeight - panel.clientHeight, horizontal: panel.scrollWidth - panel.clientWidth, overflowing,
                   panelHeight: Math.round(panel.getBoundingClientRect().height), viewportHeight: innerHeight,
                   scrollable: getComputedStyle(panel).overflowY === "auto" };
        })()`);
        check(fit.horizontal <= 0, `the panel overflows sideways: ${JSON.stringify(fit)}`);
        check(fit.overflowing.length === 0, `text is clipped in: ${JSON.stringify(fit.overflowing)}`);
        check(fit.panelHeight <= fit.viewportHeight, `the panel is taller than the screen: ${JSON.stringify(fit)}`);
        // A phone held sideways has too little height for every row, so there the
        // panel may scroll; in portrait it has to show everything at once.
        if (viewport.width > viewport.height) {
          check(fit.scrollable || fit.vertical <= 0, `the overflowing panel cannot be scrolled: ${JSON.stringify(fit)}`);
          if (fit.vertical > 0) console.log(`     note: the sheet scrolls by ${fit.vertical}px on a ${viewport.height}px-tall screen`);
        } else {
          check(fit.vertical <= 0, `the panel scrolls in portrait: ${JSON.stringify(fit)}`);
        }
      } finally {
        await send("Emulation.clearDeviceMetricsOverride");
        await send("Emulation.setTouchEmulationEnabled", { enabled: false });
        await send("Emulation.setUserAgentOverride", { userAgent, acceptLanguage: "en-US" });
      }
    });
  }

  // ── Installation edge cases ──────────────────────────────────────────────────

  // The Android host injects before the parser has created <html>, and CDP's
  // script-on-new-document does the same on desktop. Until this was deferred,
  // that injection threw and the page was left with no controller.
  await test("installs when injected before the document exists", async () => {
    await open("about:blank");
    await send("Page.enable");
    const { identifier } = await send("Page.addScriptToEvaluateOnNewDocument", { source: script });
    try {
      await send("Page.navigate", { url: baseUrl });
      await waitFor(() => evaluate(`!!${control}?.installed`).catch(() => false), "the controller to install from a document-start script", 8000);
      check((await evaluate(`document.querySelectorAll("#pvsc-root").length`)) === 1, "expected one panel");
      check((await evaluate(`${control}.version`)) === version, "installed version does not match the script");
    } finally {
      await send("Page.removeScriptToEvaluateOnNewDocument", { identifier });
    }
  });

  // No origin means no localStorage; touching it throws. The panel must still come up.
  await test("installs on a page where storage is blocked", async () => {
    await open("data:text/html,<video></video>");
    check((await evaluate(`(() => { try { localStorage.length; return "available"; } catch { return "blocked"; } })()`)) === "blocked", "fixture unexpectedly has storage");
    check((await install()) === "installed", "controller failed to install without storage");
    check((await evaluate(`!!document.getElementById("pvsc-root")`)), "panel is missing without storage");
  });

  socket.close();
} catch (error) {
  failures += 1;
  console.error(`FAIL harness: ${error.stack || error.message}`);
} finally {
  await shutdown();
}

process.exit(failures === 0 ? 0 : 1);
