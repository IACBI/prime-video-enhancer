/// Request filtering and user-agent rules for the Prime Video WebView.
///
/// Kept apart from the widget so they can be unit-tested, and kept in step with
/// `AdBlocker` in the desktop `Program.cs`.
library;

/// Third-party ad and tracking domains, matched on the registrable suffix.
const _adDomainSuffixes = [
  'amazon-adsystem.com',
  'doubleclick.net',
  'googlesyndication.com',
  'googleadservices.com',
  'google-analytics.com',
  'googletagmanager.com',
  'googletagservices.com',
  'fwmrm.net',
  'flashtalking.com',
  'innovid.com',
  'scorecardresearch.com',
  'moatads.com',
  'serving-sys.com',
  'adsrvr.org',
  'adnxs.com',
  'rubiconproject.com',
  'pubmatic.com',
  'openx.net',
  'casalemedia.com',
  'advertising.com',
  'tapad.com',
  'spotxchange.com',
  'spotx.tv',
  'springserve.com',
  'tremorhub.com',
  'yieldmo.com',
  'ad-delivery.net',
  'adtech.de',
  'smartadserver.com',
  'imrworldwide.com',
  'quantserve.com',
  'quantcount.com',
];

/// First labels of Amazon's own ad and telemetry hosts. Only honoured on an
/// Amazon retail domain: a bare prefix match also caught unrelated hosts such
/// as `unagi-sushi.example`, and missed regional ones such as `fls-us.amazon.com`.
const _adHostLabelPrefixes = ['mads'];
const _telemetryHostLabelPrefixes = ['unagi', 'device-metrics', 'fls-'];

/// Path fragments that identify an ad or telemetry endpoint.
///
/// Only applied on first-party hosts. A bare path match would also hit a
/// third-party video CDN whose segment or licence URLs happen to contain
/// something like `/interstitial` or `/csm/`, and failing one of those stalls
/// playback outright.
const _adPathFragments = [
  '/vast/',
  '/vpaid/',
  '/vast.xml',
  '/ad-manifest',
  '/interstitial',
  '/aax2/',
  '/e/dtb/',
  '/api/ads/',
];

const _telemetryPathFragments = [
  '/telemetry',
  '/gp/uedata',
  '/csm/',
  '/api/2017/suggestions',
];

const _firstPartyHostSuffixes = [
  'amazon.com',
  'primevideo.com',
  'media-amazon.com',
  'a2z.com',
  'amazon.co.uk',
  'amazon.de',
  'amazon.co.jp',
  'amazon.in',
  'amazon.com.br',
  'amazon.com.mx',
  'amazon.es',
  'amazon.it',
  'amazon.fr',
  'amazon.ca',
  'amazon.com.au',
  'amazon.nl',
  'amazon.se',
  'amazon.com.tr',
];

final _amazonRetailSuffixes =
    _firstPartyHostSuffixes.where((suffix) => suffix.startsWith('amazon.')).toList();

bool _matchesSuffix(String host, String suffix) =>
    host == suffix || host.endsWith('.$suffix');

bool _isFirstParty(String host) =>
    _firstPartyHostSuffixes.any((suffix) => _matchesSuffix(host, suffix));

bool _isAmazonRetail(String host) =>
    _amazonRetailSuffixes.any((suffix) => _matchesSuffix(host, suffix));

String _firstLabel(String host) => host.split('.').first;

/// Ad endpoints, which expect a VAST document in reply. [host] and [path] must
/// already be lower-case.
///
/// Host is checked before path: Prime Video's playback and licence traffic goes
/// to atv-ps.amazon.com and the CloudFront CDNs, so a bare substring match over
/// the whole URL risks blocking a path segment those share.
bool isAdRequest(String host, String path) {
  if (_adDomainSuffixes.any((suffix) => _matchesSuffix(host, suffix))) {
    return true;
  }
  if (host == 'aan.amazon.co' || host.startsWith('aan.amazon.co.')) return true;
  if (_isAmazonRetail(host)) {
    final label = _firstLabel(host);
    if (label == 'aan' || _adHostLabelPrefixes.any(label.startsWith)) return true;
  }
  return _isFirstParty(host) && _adPathFragments.any(path.contains);
}

/// Telemetry endpoints, which expect nothing in particular. [host] and [path]
/// must already be lower-case.
bool isTelemetryRequest(String host, String path) {
  if (_isAmazonRetail(host) &&
      _telemetryHostLabelPrefixes.any(_firstLabel(host).startsWith)) {
    return true;
  }
  return _isFirstParty(host) && _telemetryPathFragments.any(path.contains);
}

/// Hosts that make up Prime Video and Amazon's sign-in: `primevideo.com` and
/// `amazon` under any country suffix (`amazon.com`, `amazon.de`, `amazon.co.uk`,
/// `amazon.com.tr`, `amazon.ae`, ...). A pattern rather than a list, because a
/// marketplace missing from a list would lock its customers out of signing in.
final _amazonHost = RegExp(r'(^|\.)amazon\.(?:[a-z]{2,3}|com?\.[a-z]{2})$');

/// Whether [host] (lower-case) is Prime Video or Amazon itself.
bool isFirstPartyHost(String host) =>
    _matchesSuffix(host, 'primevideo.com') || _amazonHost.hasMatch(host);

/// Whether the WebView may load [uri] as the page it is showing.
///
/// The app has no address bar, so anything else that got loaded would be
/// indistinguishable from Prime Video - and would receive the controller and be
/// eligible for the DRM permission. Only Amazon and Prime Video pages qualify,
/// plus `about:blank`; every other scheme (`intent:`, `market:`, `tel:`, ...) is
/// refused. Plain `http` is allowed for those hosts because a redirect may pass
/// through it on the way to `https`.
bool isAllowedNavigation(Uri uri) {
  if (uri.scheme == 'about') return uri.toString() == 'about:blank';
  if (uri.scheme != 'https' && uri.scheme != 'http') return false;
  return isFirstPartyHost(uri.host.toLowerCase());
}

/// The notice shown when a link is refused, in the device's language (Turkish or
/// English, like the controller's panel). [target] is the host, or the scheme for
/// links that have none, such as `intent:`.
String blockedLinkMessage(String target, String languageCode) =>
    languageCode == 'tr'
        ? '$target bağlantısı engellendi'
        : 'Blocked a link to $target';

/// Turns the Android WebView's own user agent into the plain Chrome one.
///
/// The app used to pin a Chrome 120 string, which aged into a browser Prime
/// Video may treat as outdated. Deriving it keeps the version the engine
/// actually is; only the two WebView markers are removed, since pages use them
/// to serve an embedded-browser experience.
String browserUserAgent(String webViewUserAgent) => webViewUserAgent
    .replaceAll('; wv)', ')')
    .replaceAll(RegExp(r'Version/\d+(\.\d+)* '), '')
    .trim();
