// End-to-end check of the real desktop helper. Manual: it needs internet access
// and opens a visible Edge window on www.primevideo.com.
//
//   dotnet build -c Release
//   node PrimeVideoSpeedApp.Tests/helper-e2e.js [path-to-PrimeVideoSpeedApp.dll-or-exe]
//
// The helper runs with PVSC_DATA_DIR pointing at a throwaway folder, so it never
// touches the real profile, session or Start Menu entry. The helper no longer
// opens a debugging port, so nothing here attaches to the browser. Instead it
// checks the transport from outside, on the real binary against the real site:
//   * the browser was started with --remote-debugging-pipe and no --remote-debugging-port;
//   * no process of that browser listens on a TCP socket, so no other program can reach it;
//   * the helper stays up through the page load, which its DevTools conversation must allow;
//   * killing only the helper takes every browser process for that data folder with it.
// Injecting the controller is covered by the integration tests, which drive the
// same pipe transport against a headless Edge.
import { execFile, spawn } from "node:child_process";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");
const target = process.argv[2] ?? join(root, "bin", "Release", "net8.0", "PrimeVideoSpeedApp.dll");
if (!existsSync(target)) throw new Error(`Nothing to run at ${target}; build it first.`);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const check = (condition, message) => { if (!condition) throw new Error(message); };
async function waitFor(fn, what, timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    if (await fn()) return;
    if (Date.now() > deadline) throw new Error(`Timed out waiting for ${what}.`);
    await sleep(250);
  }
}

async function powershellJson(script) {
  const { stdout } = await execFileAsync("powershell", ["-NoProfile", "-Command", script]);
  const text = stdout.trim();
  if (!text) return [];
  const parsed = JSON.parse(text);
  return Array.isArray(parsed) ? parsed : [parsed];
}

// Every msedge.exe running on this data folder: the browser and its child processes.
function browserProcesses(dataDir) {
  const folder = dataDir.replace(/'/g, "''");
  return powershellJson(
    `Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" | Where-Object { $_.CommandLine -like '*${folder}*' } | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress`,
  );
}

// Get-NetTCPConnection reports "no matches" as an error, which here means no
// listeners; the explicit exit keeps that caught error from failing the command.
function listeningSockets(processIds) {
  return powershellJson(
    `try { Get-NetTCPConnection -State Listen -OwningProcess ${processIds.join(",")} -ErrorAction Stop | Select-Object OwningProcess,LocalPort | ConvertTo-Json -Compress } catch { }; exit 0`,
  );
}

const dataDir = mkdtempSync(join(tmpdir(), "pvsc-e2e-"));
const command = target.endsWith(".dll") ? ["dotnet", [target]] : [target, []];
const helper = spawn(command[0], command[1], { env: { ...process.env, PVSC_DATA_DIR: dataDir }, stdio: ["ignore", "pipe", "pipe"] });
let output = "";
helper.stdout.on("data", (chunk) => { output += chunk; });
helper.stderr.on("data", (chunk) => { output += chunk; });

let failed = false;
try {
  await waitFor(async () => (await browserProcesses(dataDir)).length > 0, "the browser to start", 90000);
  const processes = await browserProcesses(dataDir);
  const main = processes.find((proc) => !(proc.CommandLine ?? "").includes("--type="));
  check(main, "no browser process without a --type= argument was found");
  check(main.CommandLine.includes("--remote-debugging-pipe"), "the browser was not started with --remote-debugging-pipe");
  check(!main.CommandLine.includes("--remote-debugging-port"), "the browser was started with --remote-debugging-port");
  console.log(`PASS the browser (${processes.length} processes) was started with --remote-debugging-pipe and no debugging port`);

  // Time for the Prime Video page to load while the helper is running.
  await sleep(15000);
  check(helper.exitCode === null, `the helper exited with code ${helper.exitCode}`);
  console.log("PASS the helper is still running after 15 seconds");

  // Sampled several times, since a socket opened later would otherwise be missed.
  for (let sample = 0; sample < 4; sample++) {
    const ids = (await browserProcesses(dataDir)).map((proc) => proc.ProcessId);
    check(ids.length > 0, "the browser exited during the socket check");
    const listening = await listeningSockets(ids);
    check(listening.length === 0, `browser processes are listening on TCP: ${JSON.stringify(listening)}`);
    await sleep(2500);
  }
  console.log("PASS no browser process listens on a TCP port (checked over about 10 seconds)");

  // Only the helper is killed. The browser must go because its job object closes,
  // not because this script walked the process tree.
  helper.kill();
  await waitFor(async () => (await browserProcesses(dataDir)).length === 0, "the browser to go away with the helper", 20000);
  console.log("PASS the browser ended with the helper");
} catch (error) {
  failed = true;
  console.error(`FAIL ${error.message}`);
  console.error(`--- helper output ---\n${output}`);
} finally {
  if (helper.exitCode === null) helper.kill();
  // Anything a failed run left behind would otherwise keep the profile locked.
  for (const proc of await browserProcesses(dataDir).catch(() => [])) {
    try { process.kill(proc.ProcessId); } catch { /* already gone */ }
  }
  await sleep(1500);
  try {
    rmSync(dataDir, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
  } catch {
    console.warn(`Could not delete ${dataDir}`);
  }
}

process.exit(failed ? 1 : 0);
