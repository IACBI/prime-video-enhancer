using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

internal sealed class SkipTestException(string reason) : Exception(reason);

/// <summary>
/// The helper's browser-facing parts, run against a real headless browser: the
/// per-tab session, tab discovery, and the ad-request handling. None of it needs
/// Prime Video; the helper's rules for "which page counts" are injected.
/// </summary>
internal static class IntegrationTests
{
    private static readonly Lazy<Task<HeadlessBrowser?>> SharedBrowser = new(async () =>
        HeadlessBrowser.FindExecutable() is { } executable ? await HeadlessBrowser.StartAsync(executable) : null);

    public static async Task ShutDownAsync()
    {
        if (SharedBrowser.IsValueCreated && await SharedBrowser.Value is { } browser)
        {
            await browser.DisposeAsync();
        }
    }

    private static async Task<HeadlessBrowser> BrowserAsync() =>
        await SharedBrowser.Value ?? throw new SkipTestException("no Chromium-based browser found (set PVSC_BROWSER)");

    private static string LoadScript()
    {
        using var stream = typeof(DebugTarget).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.speed-control.js")
            ?? throw new InvalidOperationException("Embedded speed-control.js was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static TargetSessionOptions Options(string baseUrl, TimeSpan? safetyInterval = null) => new()
    {
        Script = LoadScript,
        IsPrimeVideoUrl = url => url.StartsWith(baseUrl + "/video", StringComparison.Ordinal),
        SafetyCheckInterval = safetyInterval ?? TimeSpan.FromMinutes(5)
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Task<bool> IsInstalledAsync(TestTab tab) =>
        tab.Client.EvaluateAsync("String(!!window.__primeVideoSpeedControl?.installed)").ContinueWith(task => task.Result == "true");

    private static Task WaitForControllerAsync(TestTab tab, int timeoutMs = 8000) =>
        TestWait.UntilAsync(() => IsInstalledAsync(tab), "the controller to be installed", timeoutMs);

    /// <summary>Ends a session started with a token and waits for it to unwind.</summary>
    private static async Task StopAsync(CancellationTokenSource cancellation, Task session)
    {
        cancellation.Cancel();
        try
        {
            await session;
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
        {
        }
    }

    public static void TestDebugPortAvoidsBusyPorts()
    {
        var busy = new TcpListener(IPAddress.Loopback, 0);
        busy.Start();
        var port = ((IPEndPoint)busy.LocalEndpoint).Port;
        try
        {
            Check(!DebugPort.IsFree(port), "a port with a listener was reported free");
            var chosen = DebugPort.Choose(port);
            Check(chosen != port && chosen > 0, "a busy preferred port was returned");
            Check(DebugPort.IsFree(chosen), "the substitute port is not free");
        }
        finally
        {
            busy.Stop();
        }

        Check(DebugPort.Choose(port) == port, "a free preferred port was not kept");
    }

    public static async Task TestSessionInjectsAndFollowsNavigation()
    {
        var browser = await BrowserAsync();
        using var server = new FixtureServer();
        await using var tab = await browser.NewTabAsync();
        using var cancellation = new CancellationTokenSource();
        var session = TargetSession.RunAsync(tab.WebSocketUrl, "about:blank", Options(server.BaseUrl), cancellation.Token);
        try
        {
            // The safety check is minutes away, so everything below is the
            // navigation events doing the work.
            await tab.NavigateAsync(server.BaseUrl + "/video/a");
            await WaitForControllerAsync(tab);
            Check(await tab.Client.EvaluateAsync("String(document.querySelectorAll('#pvsc-root').length)") == "1", "expected one panel");

            await tab.Client.EvaluateAsync("window.__marker = 1");
            await tab.NavigateAsync(server.BaseUrl + "/video/b");
            await WaitForControllerAsync(tab);
            Check(await tab.Client.EvaluateAsync("typeof window.__marker") == "undefined", "the second page reused the first page's context");

            // Not a Prime Video address: the controller must not be put there.
            await tab.NavigateAsync(server.BaseUrl + "/elsewhere");
            await Task.Delay(1500);
            Check(await tab.Client.EvaluateAsync("typeof window.__primeVideoSpeedControl") == "undefined", "the controller was injected into a page that is not Prime Video");

            await tab.NavigateAsync(server.BaseUrl + "/video/c");
            await WaitForControllerAsync(tab);
        }
        finally
        {
            await StopAsync(cancellation, session);
        }
    }

    public static async Task TestSessionRestoresAWipedController()
    {
        var browser = await BrowserAsync();
        using var server = new FixtureServer();
        await using var tab = await browser.NewTabAsync(server.BaseUrl + "/video/wipe");
        using var cancellation = new CancellationTokenSource();
        // The session starts on an already-loaded page, so the first check is what
        // installs the controller, and the short interval is what repairs it.
        var session = TargetSession.RunAsync(tab.WebSocketUrl, server.BaseUrl + "/video/wipe", Options(server.BaseUrl, TimeSpan.FromMilliseconds(400)), cancellation.Token);
        try
        {
            await WaitForControllerAsync(tab);
            await tab.Client.EvaluateAsync("window.__primeVideoSpeedControl.destroy()");
            Check(!await IsInstalledAsync(tab), "destroy() did not remove the controller");
            await WaitForControllerAsync(tab, timeoutMs: 6000);
        }
        finally
        {
            await StopAsync(cancellation, session);
        }
    }

    public static async Task TestSessionAnswersAdRequests()
    {
        var browser = await BrowserAsync();
        using var server = new FixtureServer();
        await using var tab = await browser.NewTabAsync();
        using var cancellation = new CancellationTokenSource();
        var options = new TargetSessionOptions
        {
            Script = LoadScript,
            IsPrimeVideoUrl = _ => false,
            FetchPatterns = ["*/vast/*", "*/blocked/*"],
            Classify = url => url.Contains("/vast/") ? AdRequestAction.FulfillEmptyVast
                : url.Contains("/blocked/") ? AdRequestAction.Block
                : AdRequestAction.Continue
        };
        var session = TargetSession.RunAsync(tab.WebSocketUrl, "about:blank", options, cancellation.Token);
        try
        {
            // Give the session time to switch interception on before the page asks.
            await Task.Delay(500);
            await tab.NavigateAsync(server.BaseUrl + "/other");

            var outcome = await tab.Client.EvaluateAsync(@"(async () => {
                const vast = await (await fetch('/vast/ad.xml')).text();
                let blocked;
                try { await fetch('/blocked/track'); blocked = 'loaded'; } catch { blocked = 'failed'; }
                const ok = await (await fetch('/ok/data.txt')).text();
                return JSON.stringify({ vast, blocked, ok });
            })()");
            using var document = JsonDocument.Parse(outcome!);
            var root = document.RootElement;

            Check(root.GetProperty("vast").GetString()!.Contains("<VAST version=\"3.0\"/>"), "an ad call did not get the empty VAST document");
            Check(root.GetProperty("blocked").GetString() == "failed", "a blocked request was let through");
            Check(root.GetProperty("ok").GetString() == "fine", "an ordinary request did not get through untouched");
            Check(server.HitsFor("/vast/ad.xml") == 0, "the ad call reached the network");
            Check(server.HitsFor("/blocked/track") == 0, "the blocked request reached the network");
            Check(server.HitsFor("/ok/data.txt") == 1, "the ordinary request did not reach the network exactly once");
        }
        finally
        {
            await StopAsync(cancellation, session);
        }
    }

    public static async Task TestWatcherStartsOneSessionPerMatchingTab()
    {
        var browser = await BrowserAsync();
        using var server = new FixtureServer();
        var watcher = new TargetWatcher(browser.Http, browser.Port, Options(server.BaseUrl));

        await using var matching = await browser.NewTabAsync(server.BaseUrl + "/video/watched");
        await using var other = await browser.NewTabAsync(server.BaseUrl + "/elsewhere");
        await TestWait.UntilAsync(async () => await matching.Client.EvaluateAsync("document.readyState") == "complete", "the tab to load");

        // A tab that has just been opened may not have reported its address yet.
        await TestWait.UntilAsync(() => watcher.PollAsync(), "the watcher to see the Prime Video tab");
        await watcher.PollAsync();
        Check(watcher.RunningSessions == 1, $"expected exactly one session, found {watcher.RunningSessions}");

        await WaitForControllerAsync(matching);
        Check(await other.Client.EvaluateAsync("typeof window.__primeVideoSpeedControl") == "undefined", "a tab that is not Prime Video was given the controller");

        await browser.CloseTabAsync(matching.Id);
        await TestWait.UntilAsync(() => Task.FromResult(watcher.RunningSessions == 0), "the session to end with its tab", 8000);
    }
}
