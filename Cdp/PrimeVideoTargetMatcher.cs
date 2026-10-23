internal static class PrimeVideoTargetMatcher
{
    private static readonly HashSet<string> AmazonVideoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon.com", "amazon.com.tr", "amazon.co.uk", "amazon.de",
        "amazon.fr", "amazon.it", "amazon.es"
    };

    public static bool IsMatch(DebugTarget target) =>
        string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase) &&
        IsPrimeVideoUrl(target.Url);

    /// <summary>Whether an address is a Prime Video page the controller may run on.</summary>
    public static bool IsPrimeVideoUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
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

    private static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
