using System.Text.Json;

internal static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal sealed class DebugTarget
{
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
    public string? WebSocketDebuggerUrl { get; set; }
}
