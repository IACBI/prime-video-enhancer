import 'package:flutter_test/flutter_test.dart';

import 'package:prime_video_enhancer_mobile/web_rules.dart';

void main() {
  group('isAdRequest', () {
    test('blocks third-party ad networks and Amazon ad hosts', () {
      expect(isAdRequest('sub.amazon-adsystem.com', '/x'), isTrue);
      expect(isAdRequest('securepubads.doubleclick.net', '/'), isTrue);
      expect(isAdRequest('aan.amazon.com', '/request'), isTrue);
      expect(isAdRequest('aan.amazon.co.uk', '/request'), isTrue);
      expect(isAdRequest('mads-eu.amazon.com', '/serve'), isTrue);
      expect(isAdRequest('madsx.amazon.com.tr', '/serve'), isTrue);
      expect(isAdRequest('www.primevideo.com', '/vast/ad.xml'), isTrue);
    });

    test('leaves unrelated hosts and playback traffic alone', () {
      expect(isAdRequest('mads-anything.com', '/'), isFalse);
      expect(isAdRequest('aan.amazon.example.com', '/'), isFalse);
      expect(isAdRequest('video.example.test', '/interstitial/seg.ts'), isFalse);
      expect(isAdRequest('atv-ps.amazon.com', '/cdp/catalog/getplaybackresources'), isFalse);
    });
  });

  group('isTelemetryRequest', () {
    test('catches Amazon telemetry hosts in every region', () {
      expect(isTelemetryRequest('unagi.amazon.com', '/1/events'), isTrue);
      expect(isTelemetryRequest('unagi-na.amazon.com.tr', '/'), isTrue);
      expect(isTelemetryRequest('fls-us.amazon.com', '/'), isTrue);
      expect(isTelemetryRequest('device-metrics-us-2.amazon.com', '/'), isTrue);
      expect(isTelemetryRequest('m.media-amazon.com', '/images/g/01/csm/x'), isTrue);
    });

    test('ignores look-alike hosts outside Amazon', () {
      expect(isTelemetryRequest('unagi-sushi.example', '/'), isFalse);
      expect(isTelemetryRequest('device-metrics.example.org', '/'), isFalse);
      expect(isTelemetryRequest('fls-na.evil.net', '/'), isFalse);
      expect(isTelemetryRequest('notamazon.com', '/telemetry'), isFalse);
    });
  });

  group('isAllowedNavigation', () {
    bool allowed(String url) => isAllowedNavigation(Uri.parse(url));

    test('lets Prime Video and Amazon sign-in through', () {
      expect(allowed('https://www.primevideo.com/'), isTrue);
      expect(allowed('https://app.primevideo.com/detail/x'), isTrue);
      expect(allowed('https://www.amazon.com/ap/signin'), isTrue);
      expect(allowed('https://www.amazon.co.uk/ap/signin'), isTrue);
      expect(allowed('https://www.amazon.com.tr/gp/video/storefront'), isTrue);
      expect(allowed('https://www.amazon.com.au/'), isTrue);
      expect(allowed('https://www.amazon.ae/'), isTrue);
      expect(allowed('https://na.account.amazon.com/ap/mfa'), isTrue);
      expect(allowed('http://www.amazon.com/'), isTrue);
      expect(allowed('about:blank'), isTrue);
    });

    test('refuses everything else', () {
      expect(allowed('https://example.com/'), isFalse);
      expect(allowed('https://primevideo.com.evil.example/'), isFalse);
      expect(allowed('https://amazon.com.evil.example/'), isFalse);
      expect(allowed('https://notamazon.com/'), isFalse);
      expect(allowed('https://amazon.evil.com/'), isFalse);
      expect(allowed('https://www.amazon-adsystem.com/'), isFalse);
      expect(allowed('https://evil.example/?next=https://www.primevideo.com/'), isFalse);
      expect(allowed('http://evil.example/'), isFalse);
      expect(allowed('intent://scan/#Intent;scheme=zxing;end'), isFalse);
      expect(allowed('market://details?id=com.example'), isFalse);
      expect(allowed('tel:+1234567'), isFalse);
      expect(allowed('javascript:alert(1)'), isFalse);
      expect(allowed('file:///sdcard/x.html'), isFalse);
      expect(allowed('about:srcdoc'), isFalse);
    });
  });

  test('isFirstPartyHost matches the hosts that may hold the DRM permission', () {
    expect(isFirstPartyHost('www.primevideo.com'), isTrue);
    expect(isFirstPartyHost('www.amazon.de'), isTrue);
    expect(isFirstPartyHost('m.media-amazon.com'), isFalse);
    expect(isFirstPartyHost('primevideo.com.example'), isFalse);
    expect(isFirstPartyHost(''), isFalse);
  });

  test('browserUserAgent strips only the WebView markers', () {
    const webView =
        'Mozilla/5.0 (Linux; Android 14; SM-A536B Build/UP1A.231005.007; wv) '
        'AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 '
        'Chrome/139.0.7258.143 Mobile Safari/537.36';
    expect(
      browserUserAgent(webView),
      'Mozilla/5.0 (Linux; Android 14; SM-A536B Build/UP1A.231005.007) '
      'AppleWebKit/537.36 (KHTML, like Gecko) '
      'Chrome/139.0.7258.143 Mobile Safari/537.36',
    );
  });
}
