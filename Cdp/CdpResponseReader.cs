using System.Text.Json;

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
}
