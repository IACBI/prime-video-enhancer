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

const expression = `(() => {
  const control = window.__primeVideoSpeedControl;
  if (!control || control.version !== "3.5.4") {
    return { error: "Expected injected controller version 3.5.4" };
  }

  document.getElementById("pvsc-smoke-fixture")?.remove();
  const fixture = document.createElement("div");
  fixture.id = "pvsc-smoke-fixture";

  const video = document.createElement("video");
  video.style.cssText = "position:fixed;left:0;top:0;width:800px;height:600px;";

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

  const result = {
    version: control.version,
    episodeTitleFontSize: episodeTitle.style.getPropertyValue("font-size"),
    episodeTitleBackground: episodeTitle.style.getPropertyValue("background-color"),
    subtitleFontSize: subtitle.style.getPropertyValue("font-size"),
    subtitleBackground: subtitle.style.getPropertyValue("background-color")
  };
  fixture.remove();
  return result;
})()`;

const evaluation = await send("Runtime.evaluate", {
  expression,
  returnByValue: true
});

const result = evaluation.result?.value;
if (!result || result.error) throw new Error(result?.error || "Smoke test returned no result.");
if (result.episodeTitleFontSize || result.episodeTitleBackground) {
  throw new Error(`Episode title was styled: ${JSON.stringify(result)}`);
}
if (!result.subtitleFontSize || !result.subtitleBackground) {
  throw new Error(`Subtitle was not styled: ${JSON.stringify(result)}`);
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
