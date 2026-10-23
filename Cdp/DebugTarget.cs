using System.Text.Json;

internal static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>One entry of <c>Target.getTargets</c>.</summary>
internal sealed class DebugTarget
{
    public string TargetId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
}
