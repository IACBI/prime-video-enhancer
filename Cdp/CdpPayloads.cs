using System.Text.Json;

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
