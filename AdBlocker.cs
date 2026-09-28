internal enum AdRequestAction
{
    Continue,
    Block,
    FulfillEmptyVast
}

internal static class AdBlocker
{
    // Host-anchored ad/telemetry URL glob patterns for Fetch.enable. Every entry
    // names an ad or tracking host. Wildcards are broad (e.g. "*unagi*.amazon.com*"
    // also matches unagi.amazon.com.tr) to cover regional edge hosts without a new
    // entry per region; IsAdRequest must classify everything these pause, or the
    // request is paused for nothing and continued.
    public static readonly string[] SafeBlockPatterns = new[]
    {
        "*amazon-adsystem.com*",
        "*unagi*.amazon.com*",
        "*aan.amazon.co*",
        "*fls-*.amazon.com*",
        "*device-metrics*.amazon.com*",
        "*mads*.amazon.com*",
        "*m.media-amazon.com/images/G/01/csm/*",
        "*a2z.com/telemetry*",
        "*a2z.com/gp/uedata*",
        "*completion.amazon.com/api/2017/suggestions*",
        "*doubleclick.net*",
        "*googlesyndication.com*",
        "*googleadservices.com*",
        "*google-analytics.com*",
        "*googletagmanager.com*",
        "*googletagservices.com*",
        "*fwmrm.net*",
        "*flashtalking.com*",
        "*innovid.com*",
        "*scorecardresearch.com*",
        "*moatads.com*",
        "*serving-sys.com*",
        "*adsrvr.org*",
        "*adnxs.com*",
        "*rubiconproject.com*",
        "*pubmatic.com*",
        "*openx.net*",
        "*casalemedia.com*",
        "*advertising.com*",
        "*tapad.com*",
        "*spotxchange.com*",
        "*spotx.tv*",
        "*springserve.com*",
        "*tremorhub.com*",
        "*yieldmo.com*",
        "*ad-delivery.net*",
        "*adtech.de*",
        "*smartadserver.com*",
        "*imrworldwide.com*",
        "*quantserve.com*",
        "*quantcount.com*",
        "*amazon.com/api/ads/*",
        "*amazon.com/api/telemetry/*"
    };

    // Generic path-shaped globs (no host component). These are ONLY safe as
    // Fetch.enable interception patterns, because a paused request still goes
    // through IsAdRequest — which scopes path heuristics to first-party Amazon
    // hosts — before anything is blocked; a non-ad match is simply continued.
    // Handing these to Network.setBlockedURLs (as was done before) hard-blocked
    // ANY host whose URL contained e.g. "/interstitial" or "/VAST", which can
    // kill legitimate video CDN segment requests and stall/black-screen playback.
    static readonly string[] GenericPathGlobs = new[]
    {
        "*/vast/*",
        "*/vpaid/*",
        "*/vast.xml*",
        "*/VAST*",
        "*/ad-manifest*",
        "*/interstitial*",
        "*csm/csa*"
    };

    // Interception patterns for Fetch.enable: everything in SafeBlockPatterns
    // plus the generic path globs described above.
    public static readonly string[] Patterns =
        SafeBlockPatterns.Concat(GenericPathGlobs).ToArray();

    static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon-adsystem.com", "unagi.amazon.com", "unagi-na.amazon.com",
        "aan.amazon.com", "mads.amazon.com", "mads-eu.amazon.com",
        "device-metrics-us.amazon.com", "device-metrics-us-2.amazon.com",
        "fls-na.amazon.com", "fls-eu.amazon.com", "fls-fe.amazon.com",
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "google-analytics.com", "googletagmanager.com", "googletagservices.com",
        "fwmrm.net", "flashtalking.com", "innovid.com",
        "scorecardresearch.com", "moatads.com", "serving-sys.com",
        "adsrvr.org", "adnxs.com", "rubiconproject.com",
        "pubmatic.com", "openx.net", "casalemedia.com",
        "advertising.com", "tapad.com", "spotxchange.com",
        "spotx.tv", "springserve.com", "tremorhub.com", "yieldmo.com",
        "ad-delivery.net", "adtech.de", "smartadserver.com",
        "imrworldwide.com", "quantserve.com", "quantcount.com"
    };

    static readonly string[] PathPatterns = new[]
    {
        "/vast/", "/vpaid/", "/vast.xml", "/VAST", "/ad-manifest", "/interstitial",
        "/aax2/", "/e/dtb/", "/telemetry", "/gp/uedata", "/csm/", "/api/ads/",
        "/api/2017/suggestions"
    };

    // Hosts on which the generic PathPatterns above are allowed to match. Prime
    // Video serves its VAST/telemetry/metrics endpoints from first-party Amazon
    // infrastructure, so scoping the path heuristics here keeps them effective
    // while guaranteeing they can never hard-fail a request to a third-party
    // video CDN whose segment/license URLs merely happen to contain a string
    // like "/interstitial" or "/csm/" — failing such a request with
    // BlockedByClient kills real playback (black screen / stalled stream).
    static readonly string[] PathPatternHosts = new[]
    {
        "amazon.com", "primevideo.com", "media-amazon.com", "a2z.com",
        "amazon.co.uk", "amazon.de", "amazon.co.jp", "amazon.in", "amazon.com.br",
        "amazon.com.mx", "amazon.es", "amazon.it", "amazon.fr", "amazon.ca",
        "amazon.com.au", "amazon.nl", "amazon.se", "amazon.com.tr", "amazon-adsystem.com"
    };

    static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    // Amazon's retail domains in every region. The ad and telemetry hosts use the
    // same first labels on all of them (unagi.amazon.com.tr, fls-eu.amazon.co.uk),
    // and the Fetch globs pause those too.
    static readonly string[] AmazonRetailDomains =
        PathPatternHosts.Where(domain => domain.StartsWith("amazon.", StringComparison.Ordinal)).ToArray();

    static bool IsAmazonAdHost(string host)
    {
        if (HostMatches(host, "amazon-adsystem.com")) return true;
        if (host.Equals("aan.amazon.co", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("aan.amazon.co.", StringComparison.OrdinalIgnoreCase)) return true;
        if (!AmazonRetailDomains.Any(domain => HostMatches(host, domain))) return false;

        var firstLabel = host.Split('.')[0];
        return firstLabel.Equals("aan", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("unagi", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("fls-", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("device-metrics", StringComparison.OrdinalIgnoreCase) ||
            firstLabel.StartsWith("mads", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAdRequest(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            if (IsAmazonAdHost(host)) return true;
            foreach (var domain in Domains)
            {
                if (HostMatches(host, domain))
                    return true;
            }
            // Generic path heuristics only apply to first-party Amazon hosts; see
            // PathPatternHosts for why.
            var pathHeuristicsApply = false;
            foreach (var trusted in PathPatternHosts)
            {
                if (HostMatches(host, trusted))
                {
                    pathHeuristicsApply = true;
                    break;
                }
            }
            if (!pathHeuristicsApply) return false;
            var pathAndQuery = uri.PathAndQuery;
            foreach (var pattern in PathPatterns)
            {
                if (pathAndQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    public static AdRequestAction ClassifyRequest(string url)
    {
        if (!IsAdRequest(url)) return AdRequestAction.Continue;
        if (url.Contains("/vast", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/vpaid", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("vast.xml", StringComparison.OrdinalIgnoreCase))
        {
            return AdRequestAction.FulfillEmptyVast;
        }
        return AdRequestAction.Block;
    }
}
