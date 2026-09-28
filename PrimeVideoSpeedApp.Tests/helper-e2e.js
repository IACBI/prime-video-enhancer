// End-to-end check of the real desktop helper. Manual: it needs internet access
// and opens a visible Edge window on www.primevideo.com for a few seconds.
//
//   dotnet build -c Release
//   node PrimeVideoSpeedApp.Tests/helper-e2e.js [path-to-PrimeVideoSpeedApp.dll-or-exe]
//
// The helper runs with PVSC_DATA_DIR pointing at a throwaway folder, so it never
// touches the real profile, session or Start Menu entry. What it proves, on the
// real binary against the real site:
//   * with port 9223 taken, the helper picks another port and still works;
//   * the controller is injected into the Prime Video tab, and again after a reload;
//   * killing the helper takes the browser (and its debugging endpoint) with it.
import { spawn } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import net from "node:net";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");
const version = /const VERSION = "([^"]+)"/.exec(readFileSync(join(root, "speed-control.js"), "utf8"))?.[1];
const target = process.argv[2] ?? join(root, "bin", "Release", "net8.0", "PrimeVideoSpeedApp.dll");
if (!existsSync(target)) throw new Error(`Nothing to run at ${target}; build it first.`);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const check = (condition, message) => { if (!condition) throw new Error(message); };
async function waitFor(fn, what, timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const value = await fn();
    if (value) return value;
    if (Date.now() > deadline) throw new Error(`Timed out waiting for ${what}.`);
    await sleep(250);
  }
}

// Hold the helper's preferred port, as some other program might.
const squatter = net.createServer();
const squatting = await new Promise((resolve) => {
  squatter.once("error", () => resolve(false));
  squatter.listen(9223, "127.0.0.1", () => resolve(true));
});
console.log(squatting ? "holding port 9223 to force the fallback" : "port 9223 was already taken by someone else");

const dataDir = mkdtempSync(join(tmpdir(), "pvsc-e2e-"));
const command = target.endsWith(".dll") ? ["dotnet", [target]] : [target, []];
const helper = spawn(command[0], command[1], { env: { ...process.env, PVSC_DATA_DIR: dataDir }, stdio: ["ignore", "pipe", "pipe"] });
let output = "";
helper.stdout.on("data", (chunk) => { output += chunk; });
helper.stderr.on("data", (chunk) => { output += chunk; });

let failed = false;
try {
  // The helper names the port only when it had to leave the preferred one; an
  // explicit port is not written to DevToolsActivePort, which is for port 0.
  const port = await waitFor(() => {
    const changed = /using port (\d+) instead/.exec(output);
    if (changed) return Number(changed[1]);
    return squatting ? 0 : 9223;
  }, "the helper to choose a port");
  console.log(`the helper chose port ${port}`);
  if (squatting) check(port !== 9223, "the helper used the port another program was holding");

  const tab = await waitFor(async () => {
    try {
      const list = await fetch(`http://127.0.0.1:${port}/json`).then((response) => response.json());
      return list.find((candidate) => candidate.type === "page" && /primevideo\.com/.test(candidate.url));
    } catch {
      return null;
    }
  }, "a Prime Video tab");
  console.log(`tab: ${tab.url}`);

  const socket = new WebSocket(tab.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    const request = pending.get(message.id);
    if (!request) return;
    pending.delete(message.id);
    if (message.error) request.reject(new Error(message.error.message)); else request.resolve(message.result);
  });
  await new Promise((resolve) => socket.addEventListener("open", resolve, { once: true }));
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async (expression) => (await send("Runtime.evaluate", { expression, returnByValue: true })).result.value;
  const installed = async () => {
    try {
      return (await evaluate("window.__primeVideoSpeedControl?.installed ? window.__primeVideoSpeedControl.version : null")) === version;
    } catch {
      return false;
    }
  };

  await waitFor(installed, "the controller to be injected");
  console.log(`PASS the controller ${version} was injected into the real Prime Video page`);

  await evaluate("window.__e2eMarker = 1");
  await send("Page.reload");
  await waitFor(async () => (await evaluate("typeof window.__e2eMarker")) === "undefined" && (await installed()), "the controller after a reload");
  console.log("PASS the controller was injected again after the page reloaded");
  socket.close();

  // Kill only the helper: the browser must go because the job object closes, not
  // because something walked the process tree.
  helper.kill();
  await waitFor(async () => {
    try {
      await fetch(`http://127.0.0.1:${port}/json/version`);
      return false;
    } catch {
      return true;
    }
  }, "the browser to go away with the helper", 20000);
  console.log("PASS the browser and its debugging endpoint ended with the helper");
} catch (error) {
  failed = true;
  console.error(`FAIL ${error.message}`);
  console.error(`--- helper output ---\n${output}`);
} finally {
  squatter.close();
  if (helper.exitCode === null) helper.kill();
  await sleep(1500);
  try {
    rmSync(dataDir, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
  } catch {
    console.warn(`Could not delete ${dataDir}`);
  }
}

process.exit(failed ? 1 : 0);
