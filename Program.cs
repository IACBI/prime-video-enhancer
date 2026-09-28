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
            if (PrimeVideoTargetMatcher.IsMatch(target) &&
                PrimeVideoTargetMatcher.TryGetLocalDebuggerUrl(target, RemoteDebuggingPort) is { } debuggerUrl)
            {
                foundPrimeVideoTarget = true;
                // Per target, so one tab that is navigating or hung does not skip
                // the others for this poll.
                try
                {
                    await InjectSpeedControl(debuggerUrl, script);
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
