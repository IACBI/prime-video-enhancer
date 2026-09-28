using System.Text;

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
