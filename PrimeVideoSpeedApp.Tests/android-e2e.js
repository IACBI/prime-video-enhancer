// End-to-end check of the Android app on an emulator or a device. Manual: it needs
// adb, a running device with the Android System WebView, and internet access.
//
//   cd mobile
//   flutter build apk --release --dart-define=PVSC_WEBVIEW_DEBUG=true
//   node PrimeVideoSpeedApp.Tests/android-e2e.js [path-to-apk]
//
// The APK must be the inspectable build above: release builds keep WebView
// inspection off, which is also what this script relies on being off by default.
// It installs over any existing copy (uninstall a differently signed one first).
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");
const version = /const VERSION = "([^"]+)"/.exec(readFileSync(join(root, "speed-control.js"), "utf8"))?.[1];
const apk = process.argv[2] ?? join(root, "mobile", "build", "app", "outputs", "flutter-apk", "app-release.apk");
const packageName = "com.iacbi.primevideoenhancer.prime_video_enhancer_mobile";
if (!existsSync(apk)) throw new Error(`No APK at ${apk}; build it first.`);

const sdk = process.env.ANDROID_SDK_ROOT ?? process.env.ANDROID_HOME ?? join(process.env.LOCALAPPDATA ?? "", "Android", "Sdk");
const adbPath = join(sdk, "platform-tools", process.platform === "win32" ? "adb.exe" : "adb");
const adb = (...args) => {
  const result = spawnSync(existsSync(adbPath) ? adbPath : "adb", args, { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`adb ${args.join(" ")} failed: ${result.stderr || result.stdout}`);
  return result.stdout.trim();
};
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const check = (condition, message) => { if (!condition) throw new Error(message); };
async function waitFor(fn, what, timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const value = await fn();
    if (value) return value;
    if (Date.now() > deadline) throw new Error(`Timed out waiting for ${what}.`);
    await sleep(500);
  }
}

let localPort = 0;
let failed = false;
try {
  adb("install", "-r", apk);
  adb("shell", "am", "force-stop", packageName);
  adb("shell", "am", "start", "-n", `${packageName}/.MainActivity`);

  const socket = await waitFor(() => /webview_devtools_remote_\d+/.exec(adb("shell", "cat", "/proc/net/unix"))?.[0], "the WebView inspection socket (is this an inspectable build?)");
  localPort = Number(adb("forward", "tcp:0", `localabstract:${socket}`));
  const tabs = async () => fetch(`http://127.0.0.1:${localPort}/json`).then((response) => response.json());
  const page = () => tabs().then((list) => list.find((tab) => tab.type === "page"));

  async function session() {
    const tab = await waitFor(page, "a page in the WebView");
    const ws = new WebSocket(tab.webSocketDebuggerUrl);
    await new Promise((resolve) => ws.addEventListener("open", resolve, { once: true }));
    const pending = new Map();
    let nextId = 1;
    ws.addEventListener("message", (event) => {
      const message = JSON.parse(event.data);
      pending.get(message.id)?.(message.result);
      pending.delete(message.id);
    });
    const evaluate = (expression) => new Promise((resolve) => {
      const id = nextId++;
      pending.set(id, (result) => resolve(result?.result?.value));
      ws.send(JSON.stringify({ id, method: "Runtime.evaluate", params: { expression, returnByValue: true } }));
    });
    return { evaluate, close: () => ws.close() };
  }

  const installed = async () => {
    const s = await session();
    try {
      return await s.evaluate("window.__primeVideoSpeedControl?.installed ? window.__primeVideoSpeedControl.version : null") === version;
    } finally {
      s.close();
    }
  };
  const navigate = async (url) => {
    const s = await session();
    await s.evaluate(`location.href = ${JSON.stringify(url)}`);
    s.close();
    await sleep(6000);
    return (await page())?.url;
  };

  await waitFor(async () => /primevideo\.com/.test((await page())?.url ?? ""), "Prime Video to load");
  await waitFor(installed, "the controller to be injected");
  console.log(`PASS the controller ${version} was injected into the real Prime Video page`);

  const blocked = await navigate("https://example.com/");
  check(/primevideo\.com/.test(blocked), `an off-site navigation was not refused (now at ${blocked})`);
  console.log("PASS a navigation to a page that is not Amazon or Prime Video was refused");

  const amazon = await navigate("https://www.amazon.com/");
  check(/^https:\/\/www\.amazon\.com\//.test(amazon), `an Amazon page was refused (now at ${amazon})`);
  const redirected = await navigate("http://www.amazon.de/");
  check(/^https:\/\/www\.amazon\.de\//.test(redirected), `an http redirect through Amazon was refused (now at ${redirected})`);
  console.log("PASS Amazon pages load, including through an http redirect");

  const back = await navigate("https://www.primevideo.com/");
  check(/primevideo\.com/.test(back), `could not return to Prime Video (at ${back})`);
  await waitFor(installed, "the controller after moving between sites");
  console.log("PASS the controller is present again after moving between sites");
} catch (error) {
  failed = true;
  console.error(`FAIL ${error.message}`);
} finally {
  try { if (localPort) adb("forward", "--remove", `tcp:${localPort}`); } catch {}
  try { adb("shell", "am", "force-stop", packageName); } catch {}
}

process.exit(failed ? 1 : 0);
