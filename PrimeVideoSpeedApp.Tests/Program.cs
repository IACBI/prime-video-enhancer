using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

Func<Task> Sync(Action test) => () =>
{
    test();
    return Task.CompletedTask;
};

var tests = new (string Name, Func<Task> Run)[]
{
    ("Prime Video target matching", Sync(TestPrimeVideoTargets)),
    ("Ad request classification", Sync(TestAdRequestClassification)),
    ("Safe block pattern deduplication", Sync(TestSafeBlockPatternDeduplication)),
    ("Every paused host pattern is classified", Sync(TestPausedPatternsAreClassified)),
    ("PROPVARIANT matches the native size", Sync(TestPropVariantSize)),
    ("Interceptor registration lifecycle", Sync(TestInterceptorRegistrationLifecycle)),
    ("Injection script caching", Sync(TestInjectionScriptCaching)),
    ("CDP response ID matching", Sync(TestCdpResponseIdMatching)),
    ("Subtitle selector isolation", Sync(TestSubtitleSelectorIsolation)),
    ("Injection script version consistency", Sync(TestScriptVersionConsistency)),
    ("Mobile asset matches root script", Sync(TestMobileAssetMatchesRootScript)),
    ("DevTools pipe framing and session routing", TestPipeFramingAndSessionRouting),
    // The rest drive a real headless browser and are skipped where none is installed.
    ("Session injects the controller and follows navigation", IntegrationTests.TestSessionInjectsAndFollowsNavigation),
    ("Session restores a wiped controller", IntegrationTests.TestSessionRestoresAWipedController),
    ("Session answers ad requests", IntegrationTests.TestSessionAnswersAdRequests),
    ("Watcher starts one session per matching tab", IntegrationTests.TestWatcherStartsOneSessionPerMatchingTab)
};

var failures = 0;
var skipped = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (SkipTestException skip)
    {
        skipped++;
        Console.WriteLine($"SKIP {test.Name}: {skip.Message}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

await IntegrationTests.ShutDownAsync();

Console.WriteLine($"{tests.Length - failures - skipped}/{tests.Length} test groups passed" + (skipped > 0 ? $", {skipped} skipped." : "."));
return failures == 0 ? 0 : 1;

static void TestPrimeVideoTargets()
{
    AssertTrue(IsTarget("page", "https://www.primevideo.com/detail/example"));
    AssertTrue(IsTarget("PAGE", "https://app.primevideo.com/"));
    AssertTrue(IsTarget("page", "https://www.amazon.com/gp/video/detail/example"));
    AssertTrue(IsTarget("page", "https://www.amazon.com.tr/gp/video/storefront"));
    AssertTrue(IsTarget("page", "https://smile.amazon.co.uk/gp/video/detail/example"));

    AssertFalse(IsTarget("service_worker", "https://www.primevideo.com/"));
    AssertFalse(IsTarget("page", "https://primevideo.com.example.test/"));
    AssertFalse(IsTarget("page", "https://example.test/?next=https://primevideo.com/"));
    AssertFalse(IsTarget("page", "https://www.amazon.com/gp/videos-not-prime"));
    AssertFalse(IsTarget("page", "not-a-url"));
}

static void TestAdRequestClassification()
{
    AssertTrue(AdBlocker.IsAdRequest("https://amazon-adsystem.com/aax2/example"));
    AssertTrue(AdBlocker.IsAdRequest("https://sub.amazon-adsystem.com/content"));
    AssertTrue(AdBlocker.IsAdRequest("https://unagi-eu.amazon.com/events"));
    AssertTrue(AdBlocker.IsAdRequest("https://fls-eu.amazon.com/collect"));
    AssertTrue(AdBlocker.IsAdRequest("https://device-metrics-eu.amazon.com/metrics"));
    AssertTrue(AdBlocker.IsAdRequest("https://mads-eu.amazon.com/serve"));
    AssertTrue(AdBlocker.IsAdRequest("https://aan.amazon.co.uk/request"));
    AssertTrue(AdBlocker.IsAdRequest("https://www.primevideo.com/interstitial/example"));
    AssertTrue(AdBlocker.IsAdRequest("https://www.amazon.com/gp/uedata/example"));
    AssertTrue(AdBlocker.IsAdRequest("https://www.amazon.com/api/ads/request"));
    AssertTrue(AdBlocker.IsAdRequest("https://completion.amazon.com/api/2017/suggestions?q=test"));
    AssertTrue(AdBlocker.IsAdRequest("https://m.media-amazon.com/images/G/01/csm/beacon"));

    AssertTrue(AdBlocker.IsAdRequest("https://unagi.amazon.com.tr/1/events"));
    AssertTrue(AdBlocker.IsAdRequest("https://aan.amazon.com.tr/request"));
    AssertTrue(AdBlocker.IsAdRequest("https://fls-eu.amazon.co.uk/collect"));
    AssertTrue(AdBlocker.IsAdRequest("https://mads-eu.amazon.de/serve"));

    AssertFalse(AdBlocker.IsAdRequest("https://www.amazon.com.tr/gp/video/storefront"));
    AssertFalse(AdBlocker.IsAdRequest("https://unagi.example.test/events"));
    AssertFalse(AdBlocker.IsAdRequest("https://example.test/interstitial/video-segment.ts"));
    AssertFalse(AdBlocker.IsAdRequest("https://notamazon.com/telemetry"));
    AssertFalse(AdBlocker.IsAdRequest("not-a-url"));

    AssertEqual(AdRequestAction.FulfillEmptyVast, AdBlocker.ClassifyRequest("https://www.primevideo.com/vast/ad.xml"));
    AssertEqual(AdRequestAction.Block, AdBlocker.ClassifyRequest("https://amazon-adsystem.com/aax2/example"));
    AssertEqual(AdRequestAction.Continue, AdBlocker.ClassifyRequest("https://video.example.test/interstitial/segment.ts"));
}

static void TestSafeBlockPatternDeduplication()
{
    AssertTrue(AdBlocker.SafeBlockPatterns.Contains("*amazon-adsystem.com*"));
    AssertFalse(AdBlocker.SafeBlockPatterns.Contains("*aax-*.amazon-adsystem.com*"));
    AssertFalse(AdBlocker.SafeBlockPatterns.Contains("*amazon-adsystem.com/aax2/*"));
    AssertFalse(AdBlocker.SafeBlockPatterns.Contains("*amazon-adsystem.com/e/dtb/*"));
    AssertEqual(AdBlocker.SafeBlockPatterns.Length, AdBlocker.SafeBlockPatterns.Distinct().Count());
}

// Fetch pauses everything these globs match; a paused request that IsAdRequest
// does not recognise is continued, i.e. paused for nothing and not blocked. The
// .com.tr hosts slipped through exactly this way.
static void TestPausedPatternsAreClassified()
{
    foreach (var pattern in AdBlocker.SafeBlockPatterns)
    {
        var sample = "https://" + pattern.Trim('*').Replace("*", "x");
        if (sample.IndexOf('/', "https://".Length) < 0) sample += "/";
        if (!AdBlocker.IsAdRequest(sample))
        {
            throw new InvalidOperationException($"'{pattern}' is paused but '{sample}' is not classified as an ad.");
        }
    }
}

static void TestPropVariantSize()
{
    var expected = IntPtr.Size == 8 ? 24 : 16;
    AssertEqual(expected, System.Runtime.InteropServices.Marshal.SizeOf<AppIconHelper.PROPVARIANT>());
}

static void TestInterceptorRegistrationLifecycle()
{
    var target = $"ws://127.0.0.1/devtools/page/{Guid.NewGuid():N}";
    var successfulRegistrations = 0;
    Parallel.For(0, 100, _ =>
    {
        if (InterceptorRegistry.TryRegister(target))
        {
            Interlocked.Increment(ref successfulRegistrations);
        }
    });
    AssertEqual(1, successfulRegistrations);
    AssertFalse(InterceptorRegistry.TryRegister(target));
    AssertTrue(InterceptorRegistry.Remove(target));
    AssertFalse(InterceptorRegistry.Remove(target));
    AssertTrue(InterceptorRegistry.TryRegister(target));
    AssertTrue(InterceptorRegistry.Remove(target));
}

static void TestInjectionScriptCaching()
{
    var testDirectory = Path.Combine(Path.GetTempPath(), $"pvsc-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testDirectory);
    var scriptPath = Path.Combine(testDirectory, "speed-control.js");
    var embeddedLoadCount = 0;

    try
    {
        File.WriteAllText(scriptPath, "first");
        var cache = new InjectionScriptCache(scriptPath, () =>
        {
            embeddedLoadCount++;
            return "embedded";
        });

        var firstRead = cache.GetScript();
        var unchangedRead = cache.GetScript();
        AssertEqual("first", firstRead);
        AssertTrue(ReferenceEquals(firstRead, unchangedRead));
        AssertEqual(0, embeddedLoadCount);

        var originalWriteTime = File.GetLastWriteTimeUtc(scriptPath);
        File.WriteAllText(scriptPath, "second-version");
        File.SetLastWriteTimeUtc(scriptPath, originalWriteTime.AddSeconds(2));
        AssertEqual("second-version", cache.GetScript());

        File.Delete(scriptPath);
        AssertEqual("embedded", cache.GetScript());
        AssertEqual("embedded", cache.GetScript());
        AssertEqual(1, embeddedLoadCount);
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

static void TestCdpResponseIdMatching()
{
    AssertTrue(CdpResponseReader.IsResponseForId("{\"id\":1,\"result\":{}}", 1));
    AssertFalse(CdpResponseReader.IsResponseForId("{\"id\":10,\"result\":{}}", 1));
    AssertFalse(CdpResponseReader.IsResponseForId("{\"method\":\"Runtime.consoleAPICalled\"}", 1));
    AssertFalse(CdpResponseReader.IsResponseForId("not-json", 1));
}

static void TestSubtitleSelectorIsolation()
{
    var script = LoadEmbeddedScript();
    AssertFalse(script.Contains(".atvwebplayersdk-subtitle-container > div", StringComparison.Ordinal));
    AssertFalse(script.Contains(".atvwebplayersdk-captions-container > div", StringComparison.Ordinal));
    AssertFalse(script.Contains(".atvwebplayersdk-subtitle-container span", StringComparison.Ordinal));
    AssertFalse(script.Contains(".atvwebplayersdk-captions-container span", StringComparison.Ordinal));
    AssertTrue(script.Contains("findSubtitleTextElements(video)", StringComparison.Ordinal));
    AssertTrue(script.Contains("minimumSubtitleTop", StringComparison.Ordinal));
    AssertTrue(script.Contains("previousControl.destroy?.()", StringComparison.Ordinal));
    AssertTrue(script.Contains("lifecycleController.abort()", StringComparison.Ordinal));
    AssertTrue(script.Contains("btn.matches(AUTO_SKIP_SELECTOR)", StringComparison.Ordinal));
}

// The version lives in six files. Rather than adding a seventh literal here,
// take the script's own value as the source of truth and require every other
// copy to agree with it — browser-smoke.js had already drifted a release behind
// without anything noticing, which silently disabled that whole test.
static void TestScriptVersionConsistency()
{
    var script = LoadEmbeddedScript();
    var declared = System.Text.RegularExpressions.Regex.Match(script, "const VERSION = \"([^\"]+)\"");
    AssertTrue(declared.Success);
    var version = declared.Groups[1].Value;

    AssertTrue(script.Contains("version: VERSION", StringComparison.Ordinal));

    // Matched bare, not quoted: the payload is JSON and System.Text.Json's
    // default encoder escapes the surrounding single quotes to '.
    var checkPayload = System.Text.Encoding.UTF8.GetString(CdpPayloads.CheckInstalled(1));
    AssertTrue(checkPayload.Contains(version, StringComparison.Ordinal));

    var root = FindRepositoryRoot();
    AssertTrue(File.ReadAllText(Path.Combine(root, "PrimeVideoSpeedApp.csproj"))
        .Contains($"<Version>{version}</Version>", StringComparison.Ordinal));
    AssertTrue(File.ReadAllText(Path.Combine(root, "mobile", "pubspec.yaml"))
        .Contains($"version: {version}+", StringComparison.Ordinal));
    AssertTrue(File.ReadAllText(Path.Combine(root, "CHANGELOG.md"))
        .Contains($"## {version} ", StringComparison.Ordinal));
}

// The Flutter app loads its own copy from assets/. Nothing enforced that the two
// stayed in step, so a change made in one place could ship to only one platform.
static void TestMobileAssetMatchesRootScript()
{
    var root = FindRepositoryRoot();
    var desktop = File.ReadAllBytes(Path.Combine(root, "speed-control.js"));
    var mobile = File.ReadAllBytes(Path.Combine(root, "mobile", "assets", "speed-control.js"));
    AssertTrue(desktop.AsSpan().SequenceEqual(mobile));
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "PrimeVideoSpeedApp.csproj")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
}

static string LoadEmbeddedScript()
{
    using var stream = typeof(DebugTarget).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.speed-control.js")
        ?? throw new InvalidOperationException("Embedded speed-control.js was not found.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

static bool IsTarget(string type, string url) =>
    PrimeVideoTargetMatcher.IsMatch(new DebugTarget { Type = type, Url = url });

static void AssertTrue(bool condition)
{
    if (!condition) throw new InvalidOperationException("Expected true, received false.");
}

static void AssertFalse(bool condition) => AssertTrue(!condition);

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', received '{actual}'.");
    }
}

// The browser's end of the pipe is faked, so framing and routing are checked
// without Edge: replies split across writes, several packed into one, and
// messages carrying a session id reaching that session and nothing else.
static async Task TestPipeFramingAndSessionRouting()
{
    await TestBrowserRepliesRouteById();
    await TestRepliesAreFramedAcrossWrites();
    await TestAttachedSessionIsRouted();
    await TestClosedPipeFailsPendingCalls();
}

static async Task TestBrowserRepliesRouteById()
{
    await using var rig = new PipeRig();
    var targets = rig.Connection.SendAsync("Target.getTargets");
    var version = rig.Connection.SendAsync("Browser.getVersion");
    var ids = await ReadIdsAsync(rig.Fake, 2);

    // Answered out of order: each call must get the reply with its own id.
    await rig.Fake.ReplyAsync(ids["Browser.getVersion"], "{\"product\":\"Fake/1.0\"}");
    await rig.Fake.ReplyAsync(ids["Target.getTargets"], "{\"targetInfos\":[]}");

    AssertEqual("Fake/1.0", (await version.WaitAsync(FakeBrowser.Patience)).GetProperty("product").GetString());
    AssertTrue((await targets.WaitAsync(FakeBrowser.Patience)).TryGetProperty("targetInfos", out _));
}

static async Task TestRepliesAreFramedAcrossWrites()
{
    await using var rig = new PipeRig();

    var split = rig.Connection.SendAsync("Browser.getVersion");
    var splitId = Parse(await rig.Fake.NextAsync()).GetProperty("id").GetInt32();
    var reply = Encoding.UTF8.GetBytes($"{{\"id\":{splitId},\"result\":{{\"product\":\"Split/1.0\"}}}}\0");
    var cut = reply.Length / 2;
    await rig.Fake.WriteRawAsync(reply[..cut]);
    await Task.Delay(100);
    await rig.Fake.WriteRawAsync(reply[cut..]);
    AssertEqual("Split/1.0", (await split.WaitAsync(FakeBrowser.Patience)).GetProperty("product").GetString());

    var targets = rig.Connection.SendAsync("Target.getTargets");
    var version = rig.Connection.SendAsync("Browser.getVersion");
    var ids = await ReadIdsAsync(rig.Fake, 2);
    var packed = Encoding.UTF8.GetBytes(
        $"{{\"id\":{ids["Target.getTargets"]},\"result\":{{\"targetInfos\":[]}}}}\0" +
        $"{{\"id\":{ids["Browser.getVersion"]},\"result\":{{\"product\":\"Packed/1.0\"}}}}\0");
    await rig.Fake.WriteRawAsync(packed);

    AssertTrue((await targets.WaitAsync(FakeBrowser.Patience)).TryGetProperty("targetInfos", out _));
    AssertEqual("Packed/1.0", (await version.WaitAsync(FakeBrowser.Patience)).GetProperty("product").GetString());
}

static async Task TestAttachedSessionIsRouted()
{
    await using var rig = new PipeRig();

    var attach = rig.Connection.AttachAsync("T1");
    var attachRequest = Parse(await rig.Fake.NextAsync());
    AssertEqual("Target.attachToTarget", attachRequest.GetProperty("method").GetString());
    AssertEqual("T1", attachRequest.GetProperty("params").GetProperty("targetId").GetString());
    AssertTrue(attachRequest.GetProperty("params").GetProperty("flatten").GetBoolean());
    await rig.Fake.ReplyAsync(attachRequest.GetProperty("id").GetInt32(), "{\"sessionId\":\"S1\"}");
    var session = await attach.WaitAsync(FakeBrowser.Patience);

    // The session's message reuses the id of a pending browser command. It must
    // reach the session, and must not complete the command.
    var browserCall = rig.Connection.SendAsync("Browser.getVersion");
    var browserId = Parse(await rig.Fake.NextAsync()).GetProperty("id").GetInt32();
    await rig.Fake.WriteAsync($"{{\"sessionId\":\"S1\",\"id\":{browserId},\"result\":{{\"fromSession\":true}}}}");
    var delivered = Parse((await session.ReceiveAsync().WaitAsync(FakeBrowser.Patience))!);
    AssertEqual("S1", delivered.GetProperty("sessionId").GetString());
    AssertTrue(delivered.GetProperty("result").GetProperty("fromSession").GetBoolean());
    AssertFalse(browserCall.IsCompleted);

    await rig.Fake.ReplyAsync(browserId, "{\"product\":\"Browser/1.0\"}");
    AssertEqual("Browser/1.0", (await browserCall.WaitAsync(FakeBrowser.Patience)).GetProperty("product").GetString());

    var sending = session.SendAsync(Encoding.UTF8.GetBytes("{\"id\":7,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"1+1\"}}"));
    var sent = await rig.Fake.NextAsync();
    AssertTrue(sent.StartsWith("{\"sessionId\":\"S1\",", StringComparison.Ordinal));
    var sentRoot = Parse(sent);
    AssertEqual(7, sentRoot.GetProperty("id").GetInt32());
    AssertEqual("Runtime.evaluate", sentRoot.GetProperty("method").GetString());
    AssertEqual("1+1", sentRoot.GetProperty("params").GetProperty("expression").GetString());
    await sending.WaitAsync(FakeBrowser.Patience);

    // The browser ends the session: its receive side finishes, and disposing it
    // detaches with a browser-level request.
    await rig.Fake.WriteAsync("{\"method\":\"Target.detachedFromTarget\",\"params\":{\"sessionId\":\"S1\"}}");
    AssertTrue(await session.ReceiveAsync().WaitAsync(FakeBrowser.Patience) is null);

    var detaching = session.DisposeAsync().AsTask();
    var detach = Parse(await rig.Fake.NextAsync());
    AssertEqual("Target.detachFromTarget", detach.GetProperty("method").GetString());
    AssertEqual("S1", detach.GetProperty("params").GetProperty("sessionId").GetString());
    await rig.Fake.ReplyAsync(detach.GetProperty("id").GetInt32(), "{}");
    await detaching.WaitAsync(FakeBrowser.Patience);
}

static async Task TestClosedPipeFailsPendingCalls()
{
    await using var rig = new PipeRig();
    var pending = rig.Connection.SendAsync("Browser.getVersion");
    _ = await rig.Fake.NextAsync();

    rig.Fake.CloseOutput();
    await rig.Connection.Completion.WaitAsync(FakeBrowser.Patience);

    await AssertFailsWithIOExceptionAsync(() => pending);
    await AssertFailsWithIOExceptionAsync(() => rig.Connection.SendAsync("Browser.getVersion"));
}

static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

static async Task<Dictionary<string, int>> ReadIdsAsync(FakeBrowser fake, int count)
{
    var ids = new Dictionary<string, int>();
    for (var i = 0; i < count; i++)
    {
        var request = Parse(await fake.NextAsync());
        ids[request.GetProperty("method").GetString()!] = request.GetProperty("id").GetInt32();
    }

    return ids;
}

static async Task AssertFailsWithIOExceptionAsync(Func<Task> call)
{
    try
    {
        await call().WaitAsync(FakeBrowser.Patience);
    }
    catch (IOException)
    {
        return;
    }

    throw new InvalidOperationException("Expected an IOException, but the call completed.");
}

/// <summary>A CdpConnection wired to a FakeBrowser through two anonymous pipes.</summary>
internal sealed class PipeRig : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream toHelper = new(PipeDirection.Out);
    private readonly AnonymousPipeServerStream fromHelper = new(PipeDirection.In);

    public PipeRig()
    {
        // The client ends stay open for the lifetime of the rig; each stream
        // disposes its own end when the connection and fake are disposed.
        Connection = new CdpConnection(
            input: new AnonymousPipeClientStream(PipeDirection.In, toHelper.ClientSafePipeHandle),
            output: new AnonymousPipeClientStream(PipeDirection.Out, fromHelper.ClientSafePipeHandle));
        Fake = new FakeBrowser(input: fromHelper, output: toHelper);
    }

    public CdpConnection Connection { get; }

    public FakeBrowser Fake { get; }

    public async ValueTask DisposeAsync()
    {
        // The fake goes first: its end of the pipe closing is what lets the
        // connection's read loop finish.
        await Fake.DisposeAsync();
        await Connection.DisposeAsync();
    }
}

/// <summary>The browser's side of the pipe: records what the helper writes and writes what a test asks for.</summary>
internal sealed class FakeBrowser : IAsyncDisposable
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly Stream input;
    private readonly Stream output;
    private readonly Channel<string> requests = Channel.CreateUnbounded<string>();
    private readonly Task reader;

    public FakeBrowser(Stream input, Stream output)
    {
        this.input = input;
        this.output = output;
        reader = Task.Run(ReadRequestsAsync);
    }

    /// <summary>The next message the helper wrote, without its NUL terminator.</summary>
    public Task<string> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(Patience);

    public Task ReplyAsync(int id, string resultJson) => WriteAsync($"{{\"id\":{id},\"result\":{resultJson}}}");

    public Task WriteAsync(string json) => WriteRawAsync(Encoding.UTF8.GetBytes(json + "\0"));

    public async Task WriteRawAsync(byte[] bytes)
    {
        await output.WriteAsync(bytes);
        await output.FlushAsync();
    }

    public void CloseOutput() => output.Dispose();

    private async Task ReadRequestsAsync()
    {
        var buffer = new byte[4096];
        var message = new List<byte>();
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == 0)
                    {
                        requests.Writer.TryWrite(Encoding.UTF8.GetString(message.ToArray()));
                        message.Clear();
                    }
                    else
                    {
                        message.Add(buffer[i]);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
        finally
        {
            requests.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        output.Dispose();
        await Task.WhenAny(reader, Task.Delay(2000));
        input.Dispose();
    }
}
