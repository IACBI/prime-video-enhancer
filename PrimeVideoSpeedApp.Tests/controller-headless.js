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

// Player-shaped fixture: a video inside Prime's container class, and a caption
// block low in the picture. Positions are absolute so the geometry gates in the
// controller see the same layout on every machine.
const fixturePage = `<!doctype html><html><body style="margin:0">
<div id="player" class="webPlayerSDKContainer" style="position:relative;width:800px;height:450px">
  <video id="v" style="position:absolute;left:0;top:0;width:800px;height:450px"></video>
  <div class="atvwebplayersdk-subtitle-text" style="position:absolute;left:150px;top:380px;width:500px;height:40px"><span id="staticCue">Existing line</span></div>
</div></body></html>`;

const server = http.createServer((request, response) => {
  response.writeHead(200, { "content-type": "text/html" });
  response.end(fixturePage);
});
await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const pageUrl = `http://127.0.0.1:${server.address().port}/`;

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

  function check(condition, message) {
    if (!condition) throw new Error(message);
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

  await open(pageUrl);

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
    const state = () => evaluate(`(() => {
      const v = document.getElementById("v");
      return { muted: v.muted, rate: v.playbackRate, hidden: v.style.opacity === "0", cover: !!document.getElementById("pvsc-ad-freeze-canvas") };
    })()`);
    await evaluate(`(() => {
      const ad = document.createElement("div");
      ad.id = "ad"; ad.className = "atvwebplayersdk-ad-timer-countdown"; ad.textContent = "0:27";
      ad.style.cssText = "position:absolute;left:10px;top:10px;width:60px;height:20px";
      document.getElementById("player").appendChild(ad);
      ${control}.checkAndHandleAds();
      return true;
    })()`);
    const engaged = await state();
    // 16x, or the 8x fallback: a video with no source fires 'waiting' as soon as
    // the shield asks it to play, which is exactly the stall the fallback is for.
    check(engaged.muted && (engaged.rate === 16 || engaged.rate === 8) && engaged.hidden && engaged.cover, `shield did not engage: ${JSON.stringify(engaged)}`);

    await evaluate(`document.getElementById("ad").remove(), ${control}.checkAndHandleAds(), true`);
    await sleep(500);
    await evaluate(`${control}.checkAndHandleAds()`);
    const released = await state();
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
      token: document.documentElement.style.getPropertyValue("--pvsc-sub-color")
    })`);
    check(leftovers.globals === "undefined" && leftovers.nodes === 0 && leftovers.token === "", `destroy left state behind: ${JSON.stringify(leftovers)}`);
    check((await install()) === "installed", "could not reinstall after destroy");
    await evaluate(`${control}.destroy()`);
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
