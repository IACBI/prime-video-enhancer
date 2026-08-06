// Live smoke test. Requires the desktop app to be running, i.e. an Edge process
// with --remote-debugging-port=9223 sitting on a signed-in Prime Video page.
//
//   node PrimeVideoSpeedApp.Tests/browser-smoke.js
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

// Read the expected version from the script itself. This used to be a literal,
// and it was left behind at 3.6.7 while the script moved on — so the very first
// check failed and none of the assertions below ever ran.
const here = dirname(fileURLToPath(import.meta.url));
const scriptSource = readFileSync(join(here, "..", "speed-control.js"), "utf8");
const expectedVersion = /const VERSION = "([^"]+)"/.exec(scriptSource)?.[1];
if (!expectedVersion) {
  throw new Error("Could not read VERSION out of speed-control.js.");
}

const targets = await fetch("http://127.0.0.1:9223/json").then((response) => response.json());
const target = targets.find((candidate) =>
  candidate.type === "page" && new URL(candidate.url).hostname.endsWith("primevideo.com"));

if (!target?.webSocketDebuggerUrl) {
  throw new Error("A Prime Video CDP target was not found.");
}

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

function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

// Subtitle styling is declarative now — a stylesheet rule keyed on the stamp
// attribute, driven by custom properties — so the assertions read computed
// style rather than the inline `style` attribute the old design wrote to.
const expression = `(() => {
  const control = window.__primeVideoSpeedControl;
  if (!control?.installed) {
    return { error: "The controller is not installed on this page." };
  }

  document.getElementById("pvsc-smoke-fixture")?.remove();
  const fixture = document.createElement("div");
  fixture.id = "pvsc-smoke-fixture";

  const video = document.createElement("video");
  video.style.cssText = "position:fixed;left:0;top:0;width:800px;height:600px;";

  // 5% down the video: above the 28% floor the discovery walk uses, so it must
  // never be treated as a caption region no matter what its class says.
  const episodeTitle = document.createElement("div");
  episodeTitle.className = "timedText";
  episodeTitle.textContent = "Episode title must remain unchanged";
  episodeTitle.style.cssText = "position:fixed;left:100px;top:30px;width:500px;height:50px;";

  const subtitle = document.createElement("div");
  subtitle.className = "timedText";
  subtitle.textContent = "Real subtitle must be styled";
  subtitle.style.cssText = "position:fixed;left:150px;top:500px;width:500px;height:40px;";

  fixture.append(video, episodeTitle, subtitle);
  document.body.appendChild(fixture);
  control.applySubtitleStyles(video);

  const titleColor = getComputedStyle(episodeTitle).color;
  const tokens = getComputedStyle(document.documentElement);

  // The stamp is the entire styling mechanism, so this is the contract that
  // replaced the per-element inline writes.
  subtitle.setAttribute("data-pvsc-sub-cue", "");
  const styled = getComputedStyle(subtitle);

  const result = {
    version: control.version,
    configuredColor: tokens.getPropertyValue("--pvsc-sub-color").trim(),
    configuredSize: tokens.getPropertyValue("--pvsc-sub-size").trim(),
    titleColor,
    titleStamped: episodeTitle.hasAttribute("data-pvsc-sub-cue")
      || episodeTitle.hasAttribute("data-pvsc-sub-root"),
    subtitleColor: styled.color,
    subtitleFontSize: styled.fontSize,
    subtitleWeight: styled.fontWeight
  };
  fixture.remove();
  return result;
})()`;

const evaluation = await send("Runtime.evaluate", { expression, returnByValue: true });

const result = evaluation.result?.value;
if (!result || result.error) throw new Error(result?.error || "Smoke test returned no result.");

if (result.version !== expectedVersion) {
  throw new Error(`Injected controller is ${result.version}, expected ${expectedVersion}.`);
}
if (result.titleStamped) {
  throw new Error(`The episode title was stamped as a subtitle: ${JSON.stringify(result)}`);
}
if (!/^#[0-9A-Fa-f]{6}$/.test(result.configuredColor)) {
  throw new Error(`Subtitle colour token is not set: ${JSON.stringify(result)}`);
}

const [, r, g, b] = /^#(..)(..)(..)$/.exec(result.configuredColor);
const expectedRgb = `rgb(${parseInt(r, 16)}, ${parseInt(g, 16)}, ${parseInt(b, 16)})`;
if (result.subtitleColor !== expectedRgb) {
  throw new Error(`Stamped subtitle is ${result.subtitleColor}, expected ${expectedRgb}.`);
}
if (result.titleColor === expectedRgb) {
  throw new Error(`The episode title picked up the subtitle colour: ${JSON.stringify(result)}`);
}
if (parseFloat(result.subtitleFontSize) <= 0 || result.subtitleWeight !== "700") {
  throw new Error(`Stamped subtitle did not take the size/weight rules: ${JSON.stringify(result)}`);
}

const identityToken = crypto.randomUUID();
await send("Runtime.evaluate", {
  expression: `window.__primeVideoSpeedControl.__smokeIdentity = "${identityToken}"`
});
await new Promise((resolve) => setTimeout(resolve, 4500));
const identityCheck = await send("Runtime.evaluate", {
  expression: "window.__primeVideoSpeedControl?.__smokeIdentity",
  returnByValue: true
});
if (identityCheck.result?.value !== identityToken) {
  throw new Error("The controller was unexpectedly reinjected during the polling interval.");
}
socket.close();

console.log(`PASS browser subtitle isolation ${JSON.stringify(result)}`);
console.log("PASS browser controller identity remained stable across polling cycles");
