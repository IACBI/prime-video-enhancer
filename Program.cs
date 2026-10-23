using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

const string PrimeVideoUrl = "https://www.primevideo.com/";
const string ScriptFileName = "speed-control.js";

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Prime Video Speed & Subtitle Controller runs on Windows only.");
    return 1;
}

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
    StartEdge(edgePath, openWindowOnly: true);
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

var browser = StartEdge(edgePath) ?? throw new InvalidOperationException("Microsoft Edge was not started.");
var watcher = new TargetWatcher(browser.Connection, new TargetSessionOptions { Script = scriptCache.GetScript });
var browserAnswered = false;

// The pipe closes when the browser exits. Edge can also relaunch itself, for
// example to apply an update, and the new process gets no pipe: nothing can
// reach it, so the helper ends and the job closes the window with it.
while (!browser.Connection.Completion.IsCompleted)
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
    catch (IOException)
    {
        // The connection closed while the poll was writing or waiting.
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error: {ex.Message}");
    }

    // Wakes at once when the browser exits instead of after the full delay.
    await Task.WhenAny(browser.Connection.Completion, Task.Delay(TimeSpan.FromSeconds(2)));
}

if (!browserAnswered)
{
    // Edge hands a profile that is already open to the running browser and exits
    // at once; that browser was not started with our pipe.
    Console.Error.WriteLine("Edge exited without accepting the helper's DevTools connection. It is probably already running for this profile (for example from an older version). Close its Prime Video window and start the helper again.");
    return 1;
}

return 0;

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

[SupportedOSPlatform("windows")]
static PipeBrowser? StartEdge(string edgePath, bool openWindowOnly = false)
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
        return null;
    }

    AppIconHelper.EnsureAppIconLoaded();
    var iconPath = AppIconHelper.IconPath;

    // The browser's DevTools endpoint exists only as the two pipe handles that
    // PipeBrowser.Launch passes to it, so no other process has an address to
    // connect to. The pipe flag is added at that spawn and never persisted: a
    // shortcut cannot carry the handles, and one holding the flag would open the
    // signed-in profile with no helper there to drive it.

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
    // The pipe handles can only be inherited by a direct spawn for the same reason.
    var browser = PipeBrowser.Launch(edgePath, browserArguments, beforeResume: BrowserLifetime.BindToHelper);
    AppIconHelper.EdgeProcess = browser.Process;
    return browser;
}
