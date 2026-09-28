internal static class PrimeVideoTargetMatcher
{
    private static readonly HashSet<string> AmazonVideoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon.com", "amazon.com.tr", "amazon.co.uk", "amazon.de",
        "amazon.fr", "amazon.it", "amazon.es"
    };

    public static bool IsMatch(DebugTarget target)
    {
        if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(target.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (HostMatches(uri.Host, "primevideo.com"))
        {
            return true;
        }

        var isAmazonVideoPath = uri.AbsolutePath.Equals("/gp/video", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.StartsWith("/gp/video/", StringComparison.OrdinalIgnoreCase);
        return isAmazonVideoPath && AmazonVideoHosts.Any(host => HostMatches(uri.Host, host));
    }

    // The target list is fetched over an unauthenticated loopback port, so
    // whatever answers there decides where the helper connects next. If some
    // other local process holds the port, its list could name any WebSocket
    // address and the helper would send the controller and the interception
    // commands to it. Real Edge always names its own endpoint.
    public static string? TryGetLocalDebuggerUrl(DebugTarget target, int port) =>
        Uri.TryCreate(target.WebSocketDebuggerUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeWs &&
        uri.IsLoopback &&
        uri.Port == port
            ? target.WebSocketDebuggerUrl
            : null;

    private static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
