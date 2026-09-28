using System.Text.Json;

internal static class CdpPayloads
{
    // Must match the version exported by speed-control.js. Version drift either
    // causes needless reinjection or prevents a corrected script from loading.
    private const string ScriptVersion = "3.7.0";

    // Answers two questions in one round trip: where is the tab now, and is the
    // current controller still installed in it. It reads only; the controller's
    // own timer keeps itself up to date, so nothing here needs to nudge it.
    private const string CheckInstalledExpression =
        "(() => { const control = window.__primeVideoSpeedControl; " +
        "return JSON.stringify({ href: location.href, " +
        "installed: !!(control && control.installed && control.version === '" + ScriptVersion + "') }); })()";

    public static byte[] CheckInstalled(int id) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        id,
        method = "Runtime.evaluate",
        @params = new
        {
            expression = CheckInstalledExpression,
            awaitPromise = false,
            returnByValue = true
        }
    });
}
