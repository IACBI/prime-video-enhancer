using System.Net.WebSockets;
using System.Text;
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

    public static async Task<string?> ReceiveTextMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var messageStream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text) continue;
            if (result.Count > 0) messageStream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return messageStream.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);
    }
}
