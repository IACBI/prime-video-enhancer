using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

const int RemoteDebuggingPort = 9223;
const string PrimeVideoUrl = "https://www.primevideo.com/";
const string ScriptFileName = "speed-control.js";

var edgePath = FindEdgePath();
if (edgePath is null)
{
    Console.Error.WriteLine("Microsoft Edge could not be found. Please install Microsoft Edge and try again.");
    return 1;
}

Console.WriteLine("Starting Prime Video Speed & Subtitle Controller...");
Console.WriteLine("Prime Video will open in a dedicated Microsoft Edge app window.");
Console.WriteLine("The speed & subtitle control appears automatically when the video player is available.");
Console.WriteLine("Zero-Visibility Ad Shield is active across network and player levels.");
Console.WriteLine("Custom Prime Video icon applied to application window and taskbar via AppUserModelID.");
Console.WriteLine("Close this console window to stop the helper.");

StartEdge(edgePath);

using var httpClient = new HttpClient();
var script = LoadInjectionScript();

while (true)
{
    try
    {
        var targets = await GetTargets(httpClient);
        foreach (var target in targets)
        {
            if (IsPrimeVideoTarget(target) && target.WebSocketDebuggerUrl is not null)
            {
                await InjectSpeedControl(target.WebSocketDebuggerUrl, script);
                AppIconHelper.ApplyToEdgeWindows();
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Polling request or WebSocket operation timed out; retry on next tick.
    }
    catch (HttpRequestException)
    {
        // Edge can take a moment to expose the local debugging endpoint.
    }
    catch (WebSocketException)
    {
        // Prime Video can navigate while the script is being injected; the next poll retries.
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

static string LoadInjectionScript()
{
    var scriptPath = Path.Combine(AppContext.BaseDirectory, ScriptFileName);
    if (File.Exists(scriptPath))
    {
        return File.ReadAllText(scriptPath, Encoding.UTF8);
    }

    try
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.speed-control.js");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
    catch { }

    throw new FileNotFoundException($"Required script file was not found locally or embedded: {scriptPath}");
}

static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string iconPath)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$s = (New-Object -COM WScript.Shell).CreateShortcut($env:LNK_PATH); $s.TargetPath = $env:TARGET_PATH; $s.Arguments = $env:ARGS_STR; $s.IconLocation = $env:ICON_PATH; $s.Description = 'Amazon Prime Video Enhancer'; $s.Save()\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["LNK_PATH"] = shortcutPath;
        psi.Environment["TARGET_PATH"] = targetPath;
        psi.Environment["ARGS_STR"] = arguments;
        psi.Environment["ICON_PATH"] = iconPath;

        var proc = Process.Start(psi);
        proc?.WaitForExit(2000);
    }
    catch { }
}

static void StartEdge(string edgePath)
{
    var profileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeVideoSpeedController",
        "EdgeProfile");

    Directory.CreateDirectory(profileDir);

    AppIconHelper.EnsureAppIconLoaded();
    var iconPath = AppIconHelper.GetOrCreateIconPath();

    var arguments = string.Join(
        " ",
        $"--remote-debugging-port={RemoteDebuggingPort}",
        "--remote-debugging-address=127.0.0.1",
        $"--user-data-dir=\"{profileDir}\"",
        "--no-first-run",
        "--new-window",
        $"--app=\"{PrimeVideoUrl}\"");

    var shortcutPath = Path.Combine(profileDir, "PrimeVideoSpeedController.lnk");
    CreateShortcut(shortcutPath, edgePath, arguments, iconPath);

    try
    {
        var startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (!string.IsNullOrEmpty(startMenuPrograms) && Directory.Exists(startMenuPrograms))
        {
            var startMenuLnk = Path.Combine(startMenuPrograms, "Prime Video Enhancer.lnk");
            CreateShortcut(startMenuLnk, edgePath, arguments, iconPath);
        }
    }
    catch { }

    try
    {
        AppIconHelper.EdgeProcess = Process.Start(new ProcessStartInfo
        {
            FileName = shortcutPath,
            UseShellExecute = true
        });
    }
    catch
    {
        AppIconHelper.EdgeProcess = Process.Start(new ProcessStartInfo
        {
            FileName = edgePath,
            Arguments = arguments,
            UseShellExecute = false
        });
    }
}

static async Task<List<DebugTarget>> GetTargets(HttpClient httpClient)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    using var response = await httpClient.GetAsync($"http://127.0.0.1:{RemoteDebuggingPort}/json", cts.Token);
    response.EnsureSuccessStatusCode();
    using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
    var targets = await JsonSerializer.DeserializeAsync<List<DebugTarget>>(stream, JsonOptions(), cts.Token);
    return targets ?? [];
}

static bool IsPrimeVideoTarget(DebugTarget target)
{
    if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return target.Url.Contains("primevideo.com", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.com/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.com.tr/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.co.uk/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.de/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.fr/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.it/gp/video", StringComparison.OrdinalIgnoreCase)
        || target.Url.Contains("amazon.es/gp/video", StringComparison.OrdinalIgnoreCase);
}

static async Task InjectSpeedControl(string webSocketDebuggerUrl, string script)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using var socket = new ClientWebSocket();
    await socket.ConnectAsync(new Uri(webSocketDebuggerUrl), cts.Token);

    var enableNetworkPayload = JsonSerializer.Serialize(new
    {
        id = 10,
        method = "Network.enable",
        @params = new { }
    });
    var enableNetworkBytes = Encoding.UTF8.GetBytes(enableNetworkPayload);
    await socket.SendAsync(enableNetworkBytes, WebSocketMessageType.Text, true, cts.Token);

    var blockedUrlsPayload = JsonSerializer.Serialize(new
    {
        id = 11,
        method = "Network.setBlockedURLs",
        @params = new
        {
            urls = new[]
            {
                "*amazon-adsystem.com*",
                "*a2z.com/telemetry*",
                "*flashtalking.com*",
                "*scorecardresearch.com*",
                "*doubleclick.net*",
                "*fwmrm.net*",
                "*innovid.com*"
            }
        }
    });
    var blockedUrlsBytes = Encoding.UTF8.GetBytes(blockedUrlsPayload);
    await socket.SendAsync(blockedUrlsBytes, WebSocketMessageType.Text, true, cts.Token);

    const string fastCheckExpression = "(window.__primeVideoSpeedControl?.installed ? (window.__primeVideoSpeedControl.refresh(), window.__primeVideoSpeedControl.applySpeed(), window.__primeVideoSpeedControl.applySubtitleStyles(), window.__primeVideoSpeedControl.checkAndHandleAds?.(), 'already-installed') : null)";
    var checkPayload = JsonSerializer.Serialize(new
    {
        id = 1,
        method = "Runtime.evaluate",
        @params = new
        {
            expression = fastCheckExpression,
            awaitPromise = false,
            returnByValue = true
        }
    });

    var checkBytes = Encoding.UTF8.GetBytes(checkPayload);
    await socket.SendAsync(checkBytes, WebSocketMessageType.Text, true, cts.Token);

    var buffer = new byte[8192];
    for (int i = 0; i < 10; i++)
    {
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
        {
            var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (responseText.Contains("\"id\":1", StringComparison.OrdinalIgnoreCase) || responseText.Contains("\"already-installed\"", StringComparison.OrdinalIgnoreCase))
            {
                if (responseText.Contains("\"already-installed\"", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                break;
            }
        }
        if (cts.Token.IsCancellationRequested) break;
    }

    var fullPayload = JsonSerializer.Serialize(new
    {
        id = 2,
        method = "Runtime.evaluate",
        @params = new
        {
            expression = script,
            awaitPromise = false,
            returnByValue = true
        }
    });

    var fullBytes = Encoding.UTF8.GetBytes(fullPayload);
    await socket.SendAsync(fullBytes, WebSocketMessageType.Text, true, cts.Token);
    for (int i = 0; i < 10; i++)
    {
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
        {
            var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (responseText.Contains("\"id\":2", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }
        if (cts.Token.IsCancellationRequested) break;
    }
}

static JsonSerializerOptions JsonOptions()
{
    return new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}

internal static class AppIconHelper
{
    const uint IMAGE_ICON = 1;
    const uint LR_LOADFROMFILE = 0x00000010;
    const uint LR_DEFAULTSIZE = 0x00000040;
    const uint WM_SETICON = 0x0080;
    const nint ICON_SMALL = 0;
    const nint ICON_BIG = 1;
    const ushort VT_LPWSTR = 31;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint LoadImage(nint hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

    delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("shell32.dll", SetLastError = true)]
    static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid iid, out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PROPVARIANT pvar);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
        public PROPERTYKEY(Guid guid, uint pid)
        {
            fmtid = guid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public nint pwszVal;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        int Commit();
    }

    private static nint appIconHandle = nint.Zero;
    public static Process? EdgeProcess { get; set; }

    private static bool ConvertPngToIco(string pngPath, string targetIcoPath)
    {
        try
        {
            var pngBytes = File.ReadAllBytes(pngPath);
            using var fs = File.Create(targetIcoPath);
            using var writer = new BinaryWriter(fs);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)pngBytes.Length);
            writer.Write((uint)22);
            writer.Write(pngBytes);
            return true;
        }
        catch { }
        return false;
    }

    private static bool TryExtractSystemPrimeVideoIcon(string targetIcoPath)
    {
        try
        {
            var windowsAppsDir = @"C:\Program Files\WindowsApps";
            if (Directory.Exists(windowsAppsDir))
            {
                try
                {
                    var dirs = Directory.GetDirectories(windowsAppsDir, "AmazonVideo.PrimeVideo*");
                    foreach (var dir in dirs)
                    {
                        var assetsDir = Path.Combine(dir, "Assets");
                        if (Directory.Exists(assetsDir))
                        {
                            string[] candidates = [
                                "Square44x44Logo.targetsize-256.png",
                                "Square150x150Logo.scale-100.png",
                                "StoreLogo.scale-100.png",
                                "LargeTile.scale-100.png"
                            ];
                            foreach (var cand in candidates)
                            {
                                var pngPath = Path.Combine(assetsDir, cand);
                                if (File.Exists(pngPath))
                                {
                                    if (ConvertPngToIco(pngPath, targetIcoPath))
                                        return true;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            var edgeWebApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Edge\User Data\Default\Web Applications");
            if (Directory.Exists(edgeWebApps))
            {
                var icoFiles = Directory.GetFiles(edgeWebApps, "*.ico", SearchOption.AllDirectories);
                foreach (var ico in icoFiles)
                {
                    if (ico.Contains("prime", StringComparison.OrdinalIgnoreCase) || ico.Contains("amazon", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(ico, targetIcoPath, true);
                        return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static string GetOrCreateIconPath()
    {
        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeVideoSpeedController");
            Directory.CreateDirectory(cacheDir);
            var cachedIconPath = Path.Combine(cacheDir, "AppIcon.ico");

            if (!File.Exists(cachedIconPath))
            {
                if (TryExtractSystemPrimeVideoIcon(cachedIconPath))
                    return cachedIconPath;
            }
            else
            {
                return cachedIconPath;
            }
        }
        catch { }

        var localPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(localPath)) return localPath;

        try
        {
            var psScript = Path.Combine(AppContext.BaseDirectory, "Assets", "generate-app-icon.ps1");
            if (File.Exists(psScript))
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(3000);
            }
        }
        catch { }

        if (File.Exists(localPath)) return localPath;

        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeVideoSpeedController");
            Directory.CreateDirectory(cacheDir);
            var cachedIconPath = Path.Combine(cacheDir, "AppIcon.ico");
            if (!File.Exists(cachedIconPath))
            {
                using var stream = typeof(AppIconHelper).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.Assets.AppIcon.ico");
                if (stream != null)
                {
                    using var fs = File.Create(cachedIconPath);
                    stream.CopyTo(fs);
                }
            }
            if (File.Exists(cachedIconPath)) return cachedIconPath;
        }
        catch { }

        return localPath;
    }

    public static void EnsureAppIconLoaded()
    {
        if (appIconHandle != nint.Zero) return;
        var iconPath = GetOrCreateIconPath();
        if (File.Exists(iconPath))
        {
            appIconHandle = LoadImage(nint.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        }
    }

    private static bool IsOurDedicatedEdgeProcess(uint windowPid, string titleStr)
    {
        if (EdgeProcess != null && !EdgeProcess.HasExited && windowPid == EdgeProcess.Id)
            return true;

        // 1. Exclude windows whose title looks like an IDE / editor / tab search right away before opening Process handle
        if (titleStr.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains("Cursor", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains(".cs", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains(".md", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains(".js", StringComparison.OrdinalIgnoreCase) ||
            titleStr.Contains("Altyazı Öz", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var proc = Process.GetProcessById((int)windowPid);
            // 2. MUST be msedge.exe (excludes Antigravity IDE, VS Code, Cursor, Chrome, Electron apps, etc.)
            if (!proc.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                return false;

            // 3. Must match Prime Video or Amazon in the title
            if (titleStr.Contains("Prime Video", StringComparison.OrdinalIgnoreCase) || titleStr.Contains("Amazon", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    public static void ApplyToEdgeWindows()
    {
        EnsureAppIconLoaded();
        if (appIconHandle == nint.Zero) return;

        var iconPath = GetOrCreateIconPath();
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var storeGuid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        var pkeyAumid = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        var pkeyIcon = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 3);
        var pkeyCmd = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 2);

        EnumWindows((hWnd, lParam) =>
        {
            var className = new StringBuilder(256);
            if (GetClassName(hWnd, className, 256) > 0 && className.ToString().Contains("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase))
            {
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                var title = new StringBuilder(512);
                GetWindowText(hWnd, title, 512);
                var titleStr = title.ToString();

                if (IsOurDedicatedEdgeProcess(windowPid, titleStr))
                {
                    SendMessage(hWnd, WM_SETICON, ICON_SMALL, appIconHandle);
                    SendMessage(hWnd, WM_SETICON, ICON_BIG, appIconHandle);

                    try
                    {
                        if (SHGetPropertyStoreForWindow(hWnd, ref storeGuid, out var propStore) == 0 && propStore != null)
                        {
                            try
                            {
                                var pvAumid = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni("PrimeVideoSpeedController.App") };
                                propStore.SetValue(ref pkeyAumid, ref pvAumid);
                                PropVariantClear(ref pvAumid);

                                var pvIcon = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni(iconPath) };
                                propStore.SetValue(ref pkeyIcon, ref pvIcon);
                                PropVariantClear(ref pvIcon);

                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    var pvCmd = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni(exePath) };
                                    propStore.SetValue(ref pkeyCmd, ref pvCmd);
                                    PropVariantClear(ref pvCmd);
                                }

                                propStore.Commit();
                            }
                            finally
                            {
                                if (Marshal.IsComObject(propStore))
                                {
                                    if (OperatingSystem.IsWindows())
                                    {
                                        Marshal.ReleaseComObject(propStore);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return true;
        }, nint.Zero);
    }
}

internal sealed class DebugTarget
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
    public string? WebSocketDebuggerUrl { get; set; }
}
