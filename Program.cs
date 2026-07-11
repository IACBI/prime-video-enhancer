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
        script = LoadInjectionScript();
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
        "--app-id=\"PrimeVideoSpeedController.App\"",
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

    // --- Layer 1: Network.setBlockedURLs (legacy, broad pattern matching) ---
    var enableNetworkPayload = JsonSerializer.Serialize(new
    {
        id = 10,
        method = "Network.enable",
        @params = new { }
    });
    await socket.SendAsync(Encoding.UTF8.GetBytes(enableNetworkPayload), WebSocketMessageType.Text, true, cts.Token);

    var blockedUrlsPayload = JsonSerializer.Serialize(new
    {
        id = 11,
        method = "Network.setBlockedURLs",
        @params = new { urls = AdBlocker.Patterns }
    });
    await socket.SendAsync(Encoding.UTF8.GetBytes(blockedUrlsPayload), WebSocketMessageType.Text, true, cts.Token);

    // NOTE: Fetch-domain interception is intentionally NOT enabled on this short-lived
    // socket. It used to be (a separate Fetch.enable call here), but that socket is
    // disposed as soon as this method returns, without ever reading/answering
    // Fetch.requestPaused events on this session. CDP's Fetch domain is not designed
    // for multiple concurrent owners on the same target, so having this ephemeral
    // socket "own" an interception it never services raced against the persistent
    // interceptor below (RunFetchInterceptorLoop) and could stall matching requests.
    // The persistent interceptor is the single owner of Fetch interception per target.

    // --- Script injection (check if already installed with correct version) ---
    const string fastCheckExpression = "(window.__primeVideoSpeedControl?.installed && window.__primeVideoSpeedControl?.version === '3.0.0' ? (window.__primeVideoSpeedControl.refresh(), window.__primeVideoSpeedControl.applySpeed(), window.__primeVideoSpeedControl.applySubtitleStyles(), window.__primeVideoSpeedControl.checkAndHandleAds?.(), 'already-installed') : null)";
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

    await socket.SendAsync(Encoding.UTF8.GetBytes(checkPayload), WebSocketMessageType.Text, true, cts.Token);

    var buffer = new byte[16384];
    bool alreadyInstalled = false;
    for (int i = 0; i < 10; i++)
    {
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
        {
            var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (responseText.Contains("\"id\":1", StringComparison.OrdinalIgnoreCase) || responseText.Contains("\"already-installed\"", StringComparison.OrdinalIgnoreCase))
            {
                alreadyInstalled = responseText.Contains("\"already-installed\"", StringComparison.OrdinalIgnoreCase);
                break;
            }
        }
        if (cts.Token.IsCancellationRequested) break;
    }

    if (!alreadyInstalled)
    {
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

        await socket.SendAsync(Encoding.UTF8.GetBytes(fullPayload), WebSocketMessageType.Text, true, cts.Token);
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

    // --- Start persistent Fetch interceptor if not already active for this target ---
    if (!AdBlocker.ActiveInterceptors.Contains(webSocketDebuggerUrl))
    {
        AdBlocker.ActiveInterceptors.Add(webSocketDebuggerUrl);
        _ = Task.Run(() => RunFetchInterceptorLoop(webSocketDebuggerUrl));
    }
}

static async Task RunFetchInterceptorLoop(string webSocketDebuggerUrl)
{
    try
    {
        using var socket = new ClientWebSocket();
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri(webSocketDebuggerUrl), connectCts.Token);

        // Re-enable Fetch on this persistent connection
        // Only pause requests that match a known ad/telemetry pattern (AdBlocker.Patterns).
        // Previously this used a catch-all "*" pattern, which paused every single
        // network request on the page (video segments, manifests, images, scripts)
        // and round-tripped each one through this .NET process before Chromium was
        // allowed to proceed — a major source of playback stutter/latency. Narrowing
        // the pattern list means non-ad requests never enter the Fetch domain at all
        // and load at full speed.
        var fetchEnablePayload = JsonSerializer.Serialize(new
        {
            id = 100,
            method = "Fetch.enable",
            @params = new
            {
                patterns = AdBlocker.Patterns
                    .Select(pattern => new { urlPattern = pattern, requestStage = "Request" })
                    .ToArray()
            }
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(fetchEnablePayload), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[32768];
        var nextId = 300;
        while (socket.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult? result = null;
            try
            {
                // A single CDP event can span multiple WebSocket frames (e.g. a
                // Fetch.requestPaused event with large request headers). The previous
                // implementation assumed one ReceiveAsync call always captured the
                // full message and fed the (possibly truncated) bytes straight to
                // JsonDocument.Parse; a truncated message threw, was swallowed by an
                // empty catch, and the paused request was never resolved — it hung
                // in the browser forever. Looping until EndOfMessage fixes this.
                do
                {
                    using var recvCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    result = await socket.ReceiveAsync(buffer, recvCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.Count > 0)
                    {
                        messageStream.Write(buffer, 0, result.Count);
                    }
                } while (!result.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                // Receive timed out; send a ping to check the connection is still alive.
                try
                {
                    var pingPayload = JsonSerializer.Serialize(new
                    {
                        id = nextId++,
                        method = "Runtime.evaluate",
                        @params = new { expression = "1", returnByValue = true }
                    });
                    await socket.SendAsync(Encoding.UTF8.GetBytes(pingPayload), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    break;
                }
                continue;
            }

            if (result is null || result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text || messageStream.Length == 0) continue;

            var responseText = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);

            // Handle Fetch.requestPaused events
            if (responseText.Contains("\"Fetch.requestPaused\"", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("params", out var paramsEl) &&
                        paramsEl.TryGetProperty("requestId", out var requestIdEl))
                    {
                        var requestId = requestIdEl.GetString();
                        var requestUrl = "";
                        if (paramsEl.TryGetProperty("request", out var requestEl) &&
                            requestEl.TryGetProperty("url", out var urlEl))
                        {
                            requestUrl = urlEl.GetString() ?? "";
                        }

                        if (AdBlocker.IsAdRequest(requestUrl))
                        {
                            // Check if it's a VAST/VPAID request — return empty VAST XML
                            if (requestUrl.Contains("/vast", StringComparison.OrdinalIgnoreCase) ||
                                requestUrl.Contains("/vpaid", StringComparison.OrdinalIgnoreCase) ||
                                requestUrl.Contains("vast.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                var emptyVast = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><VAST version=\"3.0\"/>";
                                var emptyVastBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(emptyVast));
                                var fulfillPayload = JsonSerializer.Serialize(new
                                {
                                    id = nextId++,
                                    method = "Fetch.fulfillRequest",
                                    @params = new
                                    {
                                        requestId = requestId,
                                        responseCode = 200,
                                        responseHeaders = new[]
                                        {
                                            new { name = "Content-Type", value = "application/xml" },
                                            new { name = "Access-Control-Allow-Origin", value = "*" }
                                        },
                                        body = emptyVastBase64
                                    }
                                });
                                await socket.SendAsync(Encoding.UTF8.GetBytes(fulfillPayload), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            else
                            {
                                // Block the ad request entirely
                                var failPayload = JsonSerializer.Serialize(new
                                {
                                    id = nextId++,
                                    method = "Fetch.failRequest",
                                    @params = new
                                    {
                                        requestId = requestId,
                                        errorReason = "BlockedByClient"
                                    }
                                });
                                await socket.SendAsync(Encoding.UTF8.GetBytes(failPayload), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                        else
                        {
                            // Allow non-ad requests to continue
                            var continuePayload = JsonSerializer.Serialize(new
                            {
                                id = nextId++,
                                method = "Fetch.continueRequest",
                                @params = new { requestId = requestId }
                            });
                            await socket.SendAsync(Encoding.UTF8.GetBytes(continuePayload), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ad shield: failed to handle a network request event ({ex.GetType().Name}: {ex.Message}).");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ad shield: network interceptor for a Prime Video tab stopped ({ex.GetType().Name}: {ex.Message}). It will restart on the next poll.");
    }
    finally
    {
        AdBlocker.ActiveInterceptors.Remove(webSocketDebuggerUrl);
    }
}

static JsonSerializerOptions JsonOptions()
{
    return new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}

internal static class AdBlocker
{
    // Single source of truth for ad/telemetry URL glob patterns, consumed by both
    // Network.setBlockedURLs and the Fetch.enable interception patterns. Wildcards
    // are broadened (e.g. "*aax-*.amazon-adsystem.com*") to cover regional edge
    // hosts without needing a new entry per region, and to avoid the drift that
    // previously existed between this list and a second, hand-maintained copy
    // inlined in InjectSpeedControl.
    public static readonly string[] Patterns = new[]
    {
        "*amazon-adsystem.com*",
        "*aax-*.amazon-adsystem.com*",
        "*amazon-adsystem.com/aax2/*",
        "*amazon-adsystem.com/e/dtb/*",
        "*unagi*.amazon.com*",
        "*aan.amazon.co*",
        "*fls-*.amazon.com*",
        "*device-metrics*.amazon.com*",
        "*mads*.amazon.com*",
        "*m.media-amazon.com/images/G/01/csm/*",
        "*csm/csa*",
        "*a2z.com/telemetry*",
        "*a2z.com/gp/uedata*",
        "*completion.amazon.com/api/2017/suggestions*",
        "*/vast/*",
        "*/vpaid/*",
        "*/vast.xml*",
        "*/VAST*",
        "*/ad-manifest*",
        "*/interstitial*",
        "*doubleclick.net*",
        "*googlesyndication.com*",
        "*googleadservices.com*",
        "*google-analytics.com*",
        "*googletagmanager.com*",
        "*googletagservices.com*",
        "*fwmrm.net*",
        "*flashtalking.com*",
        "*innovid.com*",
        "*scorecardresearch.com*",
        "*moatads.com*",
        "*serving-sys.com*",
        "*adsrvr.org*",
        "*adnxs.com*",
        "*rubiconproject.com*",
        "*pubmatic.com*",
        "*openx.net*",
        "*casalemedia.com*",
        "*advertising.com*",
        "*tapad.com*",
        "*spotxchange.com*",
        "*spotx.tv*",
        "*springserve.com*",
        "*tremorhub.com*",
        "*yieldmo.com*"
    };

    static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon-adsystem.com", "unagi.amazon.com", "unagi-na.amazon.com",
        "aan.amazon.com", "mads.amazon.com", "mads-eu.amazon.com",
        "device-metrics-us.amazon.com", "device-metrics-us-2.amazon.com",
        "fls-na.amazon.com", "fls-eu.amazon.com", "fls-fe.amazon.com",
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "google-analytics.com", "googletagmanager.com", "googletagservices.com",
        "fwmrm.net", "flashtalking.com", "innovid.com",
        "scorecardresearch.com", "moatads.com", "serving-sys.com",
        "adsrvr.org", "adnxs.com", "rubiconproject.com",
        "pubmatic.com", "openx.net", "casalemedia.com",
        "advertising.com", "tapad.com", "spotxchange.com",
        "spotx.tv", "springserve.com", "tremorhub.com", "yieldmo.com"
    };

    static readonly string[] PathPatterns = new[]
    {
        "/vast/", "/vpaid/", "/vast.xml", "/VAST", "/ad-manifest", "/interstitial",
        "/aax2/", "/e/dtb/", "/telemetry", "/gp/uedata", "/csm/"
    };

    public static readonly HashSet<string> ActiveInterceptors = new();

    public static bool IsAdRequest(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            foreach (var domain in Domains)
            {
                if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            var pathAndQuery = uri.PathAndQuery;
            foreach (var pattern in PathPatterns)
            {
                if (pathAndQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }
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

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    public interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, nint pfd, int fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(nint hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
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
            string? installDir = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-AppxPackage *AmazonVideo* -ErrorAction SilentlyContinue).InstallLocation\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    installDir = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(2000);
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                var assetsDir = Path.Combine(installDir, "Assets");
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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, bool> DedicatedPidCache = new();

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
            if (!titleStr.Contains("Prime Video", StringComparison.OrdinalIgnoreCase) && !titleStr.Contains("Amazon", StringComparison.OrdinalIgnoreCase))
                return false;

            if (DedicatedPidCache.TryGetValue(windowPid, out var isDedicated))
                return isDedicated;

            bool verified = false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId = {windowPid}').CommandLine\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var cmdProc = Process.Start(psi);
                if (cmdProc != null)
                {
                    var cmdLine = cmdProc.StandardOutput.ReadToEnd();
                    cmdProc.WaitForExit(1000);
                    if (cmdLine.Contains("9223") || cmdLine.Contains("PrimeVideoSpeedController"))
                    {
                        verified = true;
                    }
                }
            }
            catch { }

            if (verified)
            {
                DedicatedPidCache[windowPid] = true;
                return true;
            }
            else
            {
                DedicatedPidCache[windowPid] = false;
                return false;
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
