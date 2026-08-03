var tests = new (string Name, Action Run)[]
{
    ("Prime Video target matching", TestPrimeVideoTargets),
    ("Ad request classification", TestAdRequestClassification),
    ("Safe block pattern deduplication", TestSafeBlockPatternDeduplication),
    ("Interceptor registration lifecycle", TestInterceptorRegistrationLifecycle),
    ("Injection script caching", TestInjectionScriptCaching),
    ("CDP response ID matching", TestCdpResponseIdMatching),
    ("Subtitle selector isolation", TestSubtitleSelectorIsolation),
    ("Injection script version consistency", TestScriptVersionConsistency)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} test groups passed.");
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

static void TestScriptVersionConsistency()
{
    var script = LoadEmbeddedScript();
    var checkPayload = System.Text.Encoding.UTF8.GetString(CdpPayloads.CheckInstalledScript);
    AssertTrue(script.Contains("version: \"3.6.1\"", StringComparison.Ordinal));
    AssertTrue(checkPayload.Contains("3.6.1", StringComparison.Ordinal));
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
