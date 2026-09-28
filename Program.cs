using System.Collections.Concurrent;
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

// One helper per session. A second one (e.g. the Start Menu entry clicked while
// the app is open) would inject into and intercept the same tabs as the first.
using var singleInstance = new Mutex(true, @"Local\PrimeVideoSpeedController.Helper", out var isFirstInstance);
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

StartEdge(edgePath);

// The debugging endpoint is loopback, but HttpClient and ClientWebSocket honour
// HTTP_PROXY / the system proxy and would send it there - which fails outright
// behind a proxy, or hands the DevTools traffic to the proxy.
using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
var scriptCache = new InjectionScriptCache(
    Path.Combine(AppContext.BaseDirectory, ScriptFileName),
    LoadEmbeddedInjectionScript);

while (true)
{
    try
    {
        var script = scriptCache.GetScript();
        var targets = await GetTargets(httpClient);
        var foundPrimeVideoTarget = false;
        foreach (var target in targets)
        {
            if (PrimeVideoTargetMatcher.IsMatch(target) && target.WebSocketDebuggerUrl is not null)
            {
                foundPrimeVideoTarget = true;
                // Per target, so one tab that is navigating or hung does not skip
                // the others for this poll.
                try
                {
                    await InjectSpeedControl(target.WebSocketDebuggerUrl, script);
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or HttpRequestException)
                {
                    // Prime Video can navigate while the script is being injected; the next poll retries.
                }
            }
        }

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

static void StartEdge(string edgePath, bool openWindowOnly = false)
{
    var profileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeVideoSpeedController",
        "EdgeProfile");

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
        $"--remote-debugging-port={RemoteDebuggingPort}",
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
        if (!string.IsNullOrEmpty(startMenuPrograms) && Directory.Exists(startMenuPrograms))
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
    AppIconHelper.EdgeProcess = Process.Start(new ProcessStartInfo
    {
        FileName = edgePath,
        Arguments = arguments,
        UseShellExecute = false
    });

    BrowserLifetime.BindToHelper(AppIconHelper.EdgeProcess);
}

static async Task<List<DebugTarget>> GetTargets(HttpClient httpClient)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    using var response = await httpClient.GetAsync($"http://127.0.0.1:{RemoteDebuggingPort}/json", cts.Token);
    response.EnsureSuccessStatusCode();
    using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
    var targets = await JsonSerializer.DeserializeAsync<List<DebugTarget>>(stream, AppJson.Options, cts.Token);
    return targets ?? [];
}

static async Task InjectSpeedControl(string webSocketDebuggerUrl, string script)
{
    // Started first, and independently of injection succeeding: ad requests fired
    // during the first page load would otherwise go through, and a tab whose
    // injection keeps failing would never be protected at all.
    if (InterceptorRegistry.TryRegister(webSocketDebuggerUrl))
    {
        _ = Task.Run(() => RunFetchInterceptorLoop(webSocketDebuggerUrl));
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using var socket = new ClientWebSocket();
    socket.Options.Proxy = null;
    await socket.ConnectAsync(new Uri(webSocketDebuggerUrl), cts.Token);

    // No request blocking on this short-lived socket. Network.setBlockedURLs used
    // to be sent here, but it only holds while the session that set it stays
    // attached - i.e. for the few milliseconds before this socket closes - so it
    // blocked nothing. Fetch.enable here would be worse: an owner that never
    // answers Fetch.requestPaused stalls the requests it paused. The persistent
    // interceptor (RunFetchInterceptorLoop) is the single owner of blocking.

    // --- Script injection (check if already installed with correct version) ---
    await socket.SendAsync(CdpPayloads.CheckInstalledScript, WebSocketMessageType.Text, true, cts.Token);

    var buffer = new byte[16384];
    bool alreadyInstalled = false;
    for (int i = 0; i < 10; i++)
    {
        var responseText = await CdpResponseReader.ReceiveTextMessageAsync(socket, buffer, cts.Token);
        if (responseText is null) break;
        if (CdpResponseReader.IsResponseForId(responseText, 1))
        {
            alreadyInstalled = responseText.Contains("\"already-installed\"", StringComparison.OrdinalIgnoreCase);
            break;
        }
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
            var responseText = await CdpResponseReader.ReceiveTextMessageAsync(socket, buffer, cts.Token);
            if (responseText is null || CdpResponseReader.IsResponseForId(responseText, 2)) break;
        }
    }
}

static async Task RunFetchInterceptorLoop(string webSocketDebuggerUrl)
{
    try
    {
        using var socket = new ClientWebSocket();
        socket.Options.Proxy = null;
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
        await socket.SendAsync(CdpPayloads.EnableFetch, WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[32768];
        var nextId = 300;
        while (socket.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            // A single CDP event can span multiple WebSocket frames (e.g. a
            // Fetch.requestPaused event with large request headers). The previous
            // implementation assumed one ReceiveAsync call always captured the
            // full message and fed the (possibly truncated) bytes straight to
            // JsonDocument.Parse; a truncated message threw, was swallowed by an
            // empty catch, and the paused request was never resolved — it hung
            // in the browser forever. Looping until EndOfMessage fixes this.
            //
            // No receive timeout: cancelling a ClientWebSocket receive aborts the
            // socket. An idle timeout used to do exactly that after every quiet
            // 30 seconds, so the interceptor tore itself down and ad requests went
            // unintercepted until the next poll restarted it. Edge closes the
            // socket when the tab goes away, which ends the loop on its own.
            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.Count > 0)
                {
                    messageStream.Write(buffer, 0, result.Count);
                }
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text || messageStream.Length == 0) continue;

            var responseText = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);

            // Handle Fetch.requestPaused events
            if (responseText.Contains("\"Fetch.requestPaused\"", StringComparison.OrdinalIgnoreCase))
            {
                string? pausedRequestId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("params", out var paramsEl) &&
                        paramsEl.TryGetProperty("requestId", out var requestIdEl))
                    {
                        var requestId = requestIdEl.GetString();
                        if (string.IsNullOrEmpty(requestId)) continue;
                        pausedRequestId = requestId;
                        var requestUrl = "";
                        if (paramsEl.TryGetProperty("request", out var requestEl) &&
                            requestEl.TryGetProperty("url", out var urlEl))
                        {
                            requestUrl = urlEl.GetString() ?? "";
                        }

                        // A navigation is never an ad call, and failing one leaves the
                        // user on a blocked-by-client error page.
                        var isDocument = paramsEl.TryGetProperty("resourceType", out var typeEl) &&
                            typeEl.ValueEquals("Document");
                        var action = isDocument ? AdRequestAction.Continue : AdBlocker.ClassifyRequest(requestUrl);
                        if (action == AdRequestAction.FulfillEmptyVast)
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
                            pausedRequestId = null;
                        }
                        else if (action == AdRequestAction.Block)
                        {
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
                            pausedRequestId = null;
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
                            pausedRequestId = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ad shield: failed to handle a network request event ({ex.GetType().Name}: {ex.Message}).");
                    if (pausedRequestId is not null && socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            var continuePayload = JsonSerializer.Serialize(new
                            {
                                id = nextId++,
                                method = "Fetch.continueRequest",
                                @params = new { requestId = pausedRequestId }
                            });
                            await socket.SendAsync(Encoding.UTF8.GetBytes(continuePayload), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (Exception continueEx)
                        {
                            Console.WriteLine($"Ad shield: could not release a paused request ({continueEx.GetType().Name}).");
                        }
                    }
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
        InterceptorRegistry.Remove(webSocketDebuggerUrl);
    }
}

internal static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal static class CdpResponseReader
{
    public static bool IsResponseForId(string json, int expectedId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt32(out var id) &&
                id == expectedId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static async Task<string?> ReceiveTextMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var messageStream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text) continue;
            if (result.Count > 0) messageStream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return messageStream.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);
    }
}

internal sealed class InjectionScriptCache
{
    private readonly string scriptPath;
    private readonly Func<string> embeddedScriptLoader;
    private readonly object syncRoot = new();
    private DateTime cachedLastWriteTimeUtc;
    private long cachedLength = -1;
    private string? cachedExternalScript;
    private string? cachedEmbeddedScript;

    public InjectionScriptCache(string scriptPath, Func<string> embeddedScriptLoader)
    {
        this.scriptPath = scriptPath;
        this.embeddedScriptLoader = embeddedScriptLoader;
    }

    // Assembly.Location is empty in a single-file bundle, which is how every
    // release ships. Such a build never lays a speed-control.js beside the exe,
    // so a file found there was planted after download - and in a shared
    // directory that is a one-file path to running arbitrary JavaScript inside
    // the signed-in Prime Video session. Live reload stays on for source-tree
    // builds, which is the only place it was ever useful.
#pragma warning disable IL3000 // The empty-Location-in-a-single-file-bundle behaviour is exactly what is being detected here.
    private static readonly bool IsSingleFileBundle =
        string.IsNullOrEmpty(typeof(InjectionScriptCache).Assembly.Location);
#pragma warning restore IL3000

    public string GetScript()
    {
        var scriptInfo = new FileInfo(scriptPath);
        if (!IsSingleFileBundle && scriptInfo.Exists)
        {
            lock (syncRoot)
            {
                if (cachedExternalScript is not null &&
                    cachedLastWriteTimeUtc == scriptInfo.LastWriteTimeUtc &&
                    cachedLength == scriptInfo.Length)
                {
                    return cachedExternalScript;
                }

                try
                {
                    var script = File.ReadAllText(scriptPath, Encoding.UTF8);
                    cachedExternalScript = script;
                    cachedLastWriteTimeUtc = scriptInfo.LastWriteTimeUtc;
                    cachedLength = scriptInfo.Length;
                    return script;
                }
                catch (IOException) when (cachedExternalScript is not null)
                {
                    // An editor may briefly replace or lock the file while saving.
                    // Keep the last valid script and retry on the next polling cycle.
                    return cachedExternalScript;
                }
            }
        }

        lock (syncRoot)
        {
            return cachedEmbeddedScript ??= embeddedScriptLoader();
        }
    }
}

internal static class PrimeVideoTargetMatcher
{
    private static readonly HashSet<string> AmazonVideoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon.com", "amazon.com.tr", "amazon.co.uk", "amazon.de",
        "amazon.fr", "amazon.it", "amazon.es"
    };

    public static bool IsMatch(DebugTarget target)
    {
        if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(target.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (HostMatches(uri.Host, "primevideo.com"))
        {
            return true;
        }

        var isAmazonVideoPath = uri.AbsolutePath.Equals("/gp/video", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.StartsWith("/gp/video/", StringComparison.OrdinalIgnoreCase);
        return isAmazonVideoPath && AmazonVideoHosts.Any(host => HostMatches(uri.Host, host));
    }

    private static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}

internal static class InterceptorRegistry
{
    private static readonly ConcurrentDictionary<string, byte> Active = new(StringComparer.Ordinal);

    public static bool TryRegister(string targetUrl) => Active.TryAdd(targetUrl, 0);

    public static bool Remove(string targetUrl) => Active.TryRemove(targetUrl, out _);
}

internal static class CdpPayloads
{
    // Must match the version exported by speed-control.js. Version drift either
    // causes needless reinjection or prevents a corrected script from loading.
    private const string ScriptVersion = "3.7.0";

    public static readonly byte[] CheckInstalledScript = Serialize(new
    {
        id = 1,
        method = "Runtime.evaluate",
        @params = new
        {
            expression = $"(window.__primeVideoSpeedControl?.installed && window.__primeVideoSpeedControl?.version === '{ScriptVersion}' ? (window.__primeVideoSpeedControl.refresh(), window.__primeVideoSpeedControl.applySpeed(), window.__primeVideoSpeedControl.applySubtitleStyles(), window.__primeVideoSpeedControl.checkAndHandleAds?.(), 'already-installed') : null)",
            awaitPromise = false,
            returnByValue = true
        }
    });

    public static readonly byte[] EnableFetch = Serialize(new
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

    private static byte[] Serialize<T>(T payload) =>
        JsonSerializer.SerializeToUtf8Bytes(payload);
}

internal enum AdRequestAction
{
    Continue,
    Block,
    FulfillEmptyVast
}

internal static class AdBlocker
{
    // Host-anchored ad/telemetry URL glob patterns for Fetch.enable. Every entry
    // names an ad or tracking host. Wildcards are broad (e.g. "*unagi*.amazon.com*"
    // also matches unagi.amazon.com.tr) to cover regional edge hosts without a new
    // entry per region; IsAdRequest must classify everything these pause, or the
    // request is paused for nothing and continued.
    public static readonly string[] SafeBlockPatterns = new[]
    {
        "*amazon-adsystem.com*",
        "*unagi*.amazon.com*",
        "*aan.amazon.co*",
        "*fls-*.amazon.com*",
        "*device-metrics*.amazon.com*",
        "*mads*.amazon.com*",
        "*m.media-amazon.com/images/G/01/csm/*",
        "*a2z.com/telemetry*",
        "*a2z.com/gp/uedata*",
        "*completion.amazon.com/api/2017/suggestions*",
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
        "*yieldmo.com*",
        "*ad-delivery.net*",
        "*adtech.de*",
        "*smartadserver.com*",
        "*imrworldwide.com*",
        "*quantserve.com*",
        "*quantcount.com*",
        "*amazon.com/api/ads/*",
        "*amazon.com/api/telemetry/*"
    };

    // Generic path-shaped globs (no host component). These are ONLY safe as
    // Fetch.enable interception patterns, because a paused request still goes
    // through IsAdRequest — which scopes path heuristics to first-party Amazon
    // hosts — before anything is blocked; a non-ad match is simply continued.
    // Handing these to Network.setBlockedURLs (as was done before) hard-blocked
    // ANY host whose URL contained e.g. "/interstitial" or "/VAST", which can
    // kill legitimate video CDN segment requests and stall/black-screen playback.
    static readonly string[] GenericPathGlobs = new[]
    {
        "*/vast/*",
        "*/vpaid/*",
        "*/vast.xml*",
        "*/VAST*",
        "*/ad-manifest*",
        "*/interstitial*",
        "*csm/csa*"
    };

    // Interception patterns for Fetch.enable: everything in SafeBlockPatterns
    // plus the generic path globs described above.
    public static readonly string[] Patterns =
        SafeBlockPatterns.Concat(GenericPathGlobs).ToArray();

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
        "spotx.tv", "springserve.com", "tremorhub.com", "yieldmo.com",
        "ad-delivery.net", "adtech.de", "smartadserver.com",
        "imrworldwide.com", "quantserve.com", "quantcount.com"
    };

    static readonly string[] PathPatterns = new[]
    {
        "/vast/", "/vpaid/", "/vast.xml", "/VAST", "/ad-manifest", "/interstitial",
        "/aax2/", "/e/dtb/", "/telemetry", "/gp/uedata", "/csm/", "/api/ads/",
        "/api/2017/suggestions"
    };

    // Hosts on which the generic PathPatterns above are allowed to match. Prime
    // Video serves its VAST/telemetry/metrics endpoints from first-party Amazon
    // infrastructure, so scoping the path heuristics here keeps them effective
    // while guaranteeing they can never hard-fail a request to a third-party
    // video CDN whose segment/license URLs merely happen to contain a string
    // like "/interstitial" or "/csm/" — failing such a request with
    // BlockedByClient kills real playback (black screen / stalled stream).
    static readonly string[] PathPatternHosts = new[]
    {
        "amazon.com", "primevideo.com", "media-amazon.com", "a2z.com",
        "amazon.co.uk", "amazon.de", "amazon.co.jp", "amazon.in", "amazon.com.br",
        "amazon.com.mx", "amazon.es", "amazon.it", "amazon.fr", "amazon.ca",
        "amazon.com.au", "amazon.nl", "amazon.se", "amazon.com.tr", "amazon-adsystem.com"
    };

    static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    // Amazon's retail domains in every region. The ad and telemetry hosts use the
    // same first labels on all of them (unagi.amazon.com.tr, fls-eu.amazon.co.uk),
    // and the Fetch globs pause those too.
    static readonly string[] AmazonRetailDomains =
        PathPatternHosts.Where(domain => domain.StartsWith("amazon.", StringComparison.Ordinal)).ToArray();

    static bool IsAmazonAdHost(string host)
    {
        if (HostMatches(host, "amazon-adsystem.com")) return true;
        if (host.Equals("aan.amazon.co", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("aan.amazon.co.", StringComparison.OrdinalIgnoreCase)) return true;
        if (!AmazonRetailDomains.Any(domain => HostMatches(host, domain))) return false;

        var firstLabel = host.Split('.')[0];
        return firstLabel.Equals("aan", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("unagi", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("fls-", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("device-metrics", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("mads", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAdRequest(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            if (IsAmazonAdHost(host)) return true;
            foreach (var domain in Domains)
            {
                if (HostMatches(host, domain))
                    return true;
            }
            // Generic path heuristics only apply to first-party Amazon hosts; see
            // PathPatternHosts for why.
            var pathHeuristicsApply = false;
            foreach (var trusted in PathPatternHosts)
            {
                if (HostMatches(host, trusted))
                {
                    pathHeuristicsApply = true;
                    break;
                }
            }
            if (!pathHeuristicsApply) return false;
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

    public static AdRequestAction ClassifyRequest(string url)
    {
        if (!IsAdRequest(url)) return AdRequestAction.Continue;
        if (url.Contains("/vast", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/vpaid", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("vast.xml", StringComparison.OrdinalIgnoreCase))
        {
            return AdRequestAction.FulfillEmptyVast;
        }
        return AdRequestAction.Block;
    }
}

/// <summary>
/// Ties the dedicated browser to this helper's own lifetime.
/// </summary>
/// <remarks>
/// The browser exposes an unauthenticated DevTools endpoint on loopback, which
/// any process on the machine can drive - including processes of other OS users,
/// against whom the profile directory on disk is ACL-protected but the port is
/// not. Left to itself the browser outlives the helper (the documented shutdown
/// is "close this console window"), so the endpoint would keep serving the
/// signed-in profile with nothing left that needs it.
///
/// A job object with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE is used rather than an
/// exit handler because the kernel closes the job handle however the helper
/// dies - console close, Ctrl+C, task kill or crash - whereas managed exit
/// handlers do not run reliably for all of those.
/// </remarks>
internal static class BrowserLifetime
{
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    // Held for the process lifetime: closing this handle is what kills the browser.
    private static nint jobHandle;

    public static void BindToHelper(Process? browser)
    {
        if (browser is null || !OperatingSystem.IsWindows()) return;

        try
        {
            if (jobHandle == nint.Zero)
            {
                jobHandle = CreateJob();
            }

            if (jobHandle != nint.Zero)
            {
                AssignProcessToJobObject(jobHandle, browser.Handle);
            }
        }
        catch { }
    }

    private static nint CreateJob()
    {
        var handle = CreateJobObject(nint.Zero, null);
        if (handle == nint.Zero) return nint.Zero;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, pointer, (uint)length))
            {
                CloseHandle(handle);
                return nint.Zero;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(nint hJob, uint infoClass, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}

internal static class AppIconHelper
{
    // Absolute path, because CreateProcess resolves a bare name against the
    // calling application's directory first: a powershell.exe dropped next to
    // the exe would otherwise be launched instead of the system one.
    private static readonly string WindowsPowerShellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    const uint IMAGE_ICON = 1;
    const uint LR_LOADFROMFILE = 0x00000010;
    const uint LR_DEFAULTSIZE = 0x00000040;
    const uint WM_SETICON = 0x0080;
    const nint ICON_SMALL = 0;
    const nint ICON_BIG = 1;
    const ushort VT_LPWSTR = 31;
    const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint LoadImage(nint hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    static extern nint SendMessageTimeout(nint hWnd, uint Msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

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
        // The native union is two pointers wide (24 bytes in total on x64, 16 on
        // x86). Without this, PropVariantClear zeroes 8 bytes past the struct -
        // i.e. whatever sits next to it on the stack.
        public nint padding;
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
                    FileName = WindowsPowerShellPath,
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

        // A loose Assets\generate-app-icon.ps1 used to be executed here when the
        // icon was missing. Released builds are single-file and never ship that
        // script, so any copy sitting next to the exe was put there after
        // download - in a shared directory, by anyone holding create-file rights.
        // Running it handed that person code execution as whoever launched the
        // app. The icon is generated at build time (see the csproj target) and
        // falls back to the embedded resource below, so nothing is lost.

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

    private static string? resolvedIconPath;

    /// <summary>The icon file, resolved once; resolving touches the file system and may extract files.</summary>
    public static string IconPath => resolvedIconPath ??= GetOrCreateIconPath();

    public static void EnsureAppIconLoaded()
    {
        if (appIconHandle != nint.Zero) return;
        var iconPath = IconPath;
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

        // 2. Must match Prime Video or Amazon in the title. Checked before opening a
        // process handle: this runs every poll for every Chromium-family window.
        if (!titleStr.Contains("Prime Video", StringComparison.OrdinalIgnoreCase) && !titleStr.Contains("Amazon", StringComparison.OrdinalIgnoreCase))
            return false;

        // Only msedge pids are ever cached, so a hit needs no process handle.
        if (DedicatedPidCache.TryGetValue(windowPid, out var isDedicated))
            return isDedicated;

        try
        {
            using var proc = Process.GetProcessById((int)windowPid);
            // 3. MUST be msedge.exe (excludes Antigravity IDE, VS Code, Cursor, Chrome, Electron apps, etc.)
            if (!proc.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                return false;

            bool verified = false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WindowsPowerShellPath,
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

        var iconPath = IconPath;
        var exePath = Environment.ProcessPath ?? "";
        var storeGuid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        var pkeyAumid = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        var pkeyIcon = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 3);
        var pkeyCmd = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 2);
        var pkeyDisplayName = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 4);

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
                    // Bounded: a plain SendMessage to a hung Edge UI thread would
                    // freeze the poll loop, and with it injection and ad blocking.
                    SendMessageTimeout(hWnd, WM_SETICON, ICON_SMALL, appIconHandle, SMTO_ABORTIFHUNG, 500, out _);
                    SendMessageTimeout(hWnd, WM_SETICON, ICON_BIG, appIconHandle, SMTO_ABORTIFHUNG, 500, out _);

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

                                    // Windows ignores the relaunch command unless its
                                    // display name is set alongside it.
                                    var pvName = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni("Prime Video Enhancer") };
                                    propStore.SetValue(ref pkeyDisplayName, ref pvName);
                                    PropVariantClear(ref pvName);
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
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
    public string? WebSocketDebuggerUrl { get; set; }
}
