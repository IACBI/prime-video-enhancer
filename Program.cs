using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

const int PreferredDebugPort = 9223;
const string PrimeVideoUrl = "https://www.primevideo.com/";
const string ScriptFileName = "speed-control.js";

var edgePath = FindEdgePath();
if (edgePath is null)
{
    Console.Error.WriteLine("Microsoft Edge could not be found. Please install Microsoft Edge and try again.");
    return 1;
}

// One helper per session. A second one (e.g. the Start Menu entry clicked while
// the app is open) would inject into and intercept the same tabs as the first.
// A copy pointed at its own data folder is a separate installation and gets its
// own name, so it neither blocks nor is blocked by the normal one.
var instanceName = AppPaths.IsCustomDataRoot
    ? "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppPaths.DataRoot.ToLowerInvariant())))[..16]
    : "";
using var singleInstance = new Mutex(true, @"Local\PrimeVideoSpeedController.Helper" + instanceName, out var isFirstInstance);
if (!isFirstInstance)
{
    Console.WriteLine("Prime Video Speed & Subtitle Controller is already running; opening another window.");
    StartEdge(edgePath, debugPort: 0, openWindowOnly: true);
    return 0;
}

Console.WriteLine("Starting Prime Video Speed & Subtitle Controller...");
Console.WriteLine("Prime Video will open in a dedicated Microsoft Edge app window.");
Console.WriteLine("The speed & subtitle control appears automatically when the video player is available.");
Console.WriteLine("Zero-Visibility Ad Shield is active across network and player levels.");
Console.WriteLine("Custom Prime Video icon applied to application window and taskbar via AppUserModelID.");
Console.WriteLine("Close this console window to stop the helper and the Prime Video window.");

// The controller ships inside the executable; failing here, once, beats printing
// the same error on every session that tries to inject it.
var scriptCache = new InjectionScriptCache(
    Path.Combine(AppContext.BaseDirectory, ScriptFileName),
    LoadEmbeddedInjectionScript);
_ = scriptCache.GetScript();

var debugPort = DebugPort.Choose(PreferredDebugPort);
if (debugPort != PreferredDebugPort)
{
    Console.WriteLine($"Port {PreferredDebugPort} is in use by another program; using port {debugPort} instead.");
}

StartEdge(edgePath, debugPort);

// The debugging endpoint is loopback, but HttpClient and ClientWebSocket honour
// HTTP_PROXY / the system proxy and would send it there - which fails outright
// behind a proxy, or hands the DevTools traffic to the proxy.
using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
var watcher = new TargetWatcher(httpClient, debugPort, new TargetSessionOptions { Script = scriptCache.GetScript });
var browserAnswered = false;

while (true)
{
    try
    {
        var foundPrimeVideoTarget = await watcher.PollAsync();
        browserAnswered = true;

        if (foundPrimeVideoTarget)
        {
            AppIconHelper.ApplyToEdgeWindows();
        }
    }
    catch (OperationCanceledException)
    {
        // Polling request timed out; retry on next tick.
    }
    catch (HttpRequestException)
    {
        // Edge takes a moment to expose the endpoint at startup. Once the browser
        // we launched is gone and nothing answers, the user closed the window:
        // stop instead of polling a dead port forever. Checking the endpoint too
        // keeps a browser that relaunched itself (e.g. to apply an update) alive.
        if (AppIconHelper.EdgeProcess is { HasExited: true })
        {
            if (!browserAnswered)
            {
                // Edge hands a profile that is already open to the running browser
                // and exits at once; that browser was not started with our port.
                Console.Error.WriteLine("Edge exited without opening its debugging port. It is probably already running for this profile (for example from an older version). Close its Prime Video window and start the helper again.");
                return 1;
            }

            return 0;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error: {ex.Message}");
    }

    await Task.Delay(TimeSpan.FromSeconds(2));
}

static string? FindEdgePath()
{
    string[] candidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
    ];

    return candidates.FirstOrDefault(File.Exists);
}

static string LoadEmbeddedInjectionScript()
{
    try
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.speed-control.js");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException("The embedded speed-control script could not be loaded.", ex);
    }

    throw new FileNotFoundException("The embedded speed-control script was not found.");
}

static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string iconPath)
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            var link = (AppIconHelper.IShellLinkW)new AppIconHelper.ShellLink();
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                link.SetIconLocation(iconPath, 0);
            }
            link.SetDescription("Amazon Prime Video Enhancer");

            if (link is AppIconHelper.IPropertyStore store)
            {
                var pkeyAumid = new AppIconHelper.PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                var pvAumid = new AppIconHelper.PROPVARIANT { vt = 31, pwszVal = Marshal.StringToCoTaskMemUni("PrimeVideoSpeedController.App") };
                store.SetValue(ref pkeyAumid, ref pvAumid);
                store.Commit();
                Marshal.FreeCoTaskMem(pvAumid.pwszVal);
            }

            var persistFile = (AppIconHelper.IPersistFile)link;
            persistFile.Save(shortcutPath, true);
        }
    }
    catch { }
}

static void StartEdge(string edgePath, int debugPort, bool openWindowOnly = false)
{
    var profileDir = AppPaths.EdgeProfile;

    Directory.CreateDirectory(profileDir);

    // Flags every launch of the dedicated profile shares.
    var browserArguments = string.Join(
        " ",
        $"--user-data-dir=\"{profileDir}\"",
        "--no-first-run",
        "--new-window",
        "--app-id=\"PrimeVideoSpeedController.App\"",
        $"--app=\"{PrimeVideoUrl}\"");

    if (openWindowOnly)
    {
        // The profile is already open in a browser another helper owns; launching
        // it again just asks that browser for a new window.
        Process.Start(new ProcessStartInfo { FileName = edgePath, Arguments = browserArguments, UseShellExecute = false })?.Dispose();
        return;
    }

    AppIconHelper.EnsureAppIconLoaded();
    var iconPath = AppIconHelper.IconPath;

    // The DevTools endpoint is unauthenticated and every process on the machine
    // can reach loopback, so it may exist only while this helper is alive to use
    // it. The debugging flags therefore live on the direct spawn below and never
    // in a persisted shortcut: a shortcut carrying them would silently re-arm the
    // endpoint on the signed-in profile on every future click, with no helper.
    var arguments = string.Join(
        " ",
        $"--remote-debugging-port={debugPort}",
        "--remote-debugging-address=127.0.0.1",
        browserArguments);

    // Older versions launched Edge through this shortcut and stored the debugging
    // flags in it. Nothing launches it any more, so remove it rather than leave a
    // way to open the profile that bypasses the helper.
    try
    {
        File.Delete(Path.Combine(profileDir, "PrimeVideoSpeedController.lnk"));
    }
    catch { }

    try
    {
        var startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (!AppPaths.IsCustomDataRoot && !string.IsNullOrEmpty(startMenuPrograms) && Directory.Exists(startMenuPrograms))
        {
            var startMenuLnk = Path.Combine(startMenuPrograms, "Prime Video Enhancer.lnk");

            // Point the Start Menu entry at the helper so relaunching starts the
            // whole app. Rewriting it on every run also disarms the debug-flag
            // shortcut older versions installed.
            var helperPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(helperPath))
            {
                CreateShortcut(startMenuLnk, helperPath, string.Empty, iconPath);
            }
            else
            {
                CreateShortcut(startMenuLnk, edgePath, browserArguments, iconPath);
            }
        }
    }
    catch { }

    // Spawn Edge directly rather than through the .lnk: shell-executing a
    // shortcut returns no usable process handle, so the browser could not be
    // bound to the helper's lifetime (and the window could not be identified).
    AppIconHelper.DebugPort = debugPort;
    AppIconHelper.EdgeProcess = Process.Start(new ProcessStartInfo
    {
        FileName = edgePath,
        Arguments = arguments,
        UseShellExecute = false
    });

    BrowserLifetime.BindToHelper(AppIconHelper.EdgeProcess);
}
