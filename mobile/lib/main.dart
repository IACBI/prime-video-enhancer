import 'dart:collection';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';

/// Set at build time (`--dart-define=PVSC_WEBVIEW_DEBUG=true`) to expose the
/// WebView to `chrome://inspect`. Off by default: it makes the page contents of
/// a signed-in Prime Video session readable by any app-debuggable tooling.
const bool _webViewDebug =
    bool.fromEnvironment('PVSC_WEBVIEW_DEBUG', defaultValue: false);

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  if (_webViewDebug) {
    InAppWebViewController.setWebContentsDebuggingEnabled(true);
  }
  runApp(const PrimeVideoEnhancerApp());
}

class PrimeVideoEnhancerApp extends StatelessWidget {
  const PrimeVideoEnhancerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Prime Video Enhancer',
      debugShowCheckedModeBanner: false,
      theme: ThemeData.dark().copyWith(
        scaffoldBackgroundColor: Colors.black,
        colorScheme: const ColorScheme.dark(
          primary: Color(0xFF00A8E1),
          secondary: Color(0xFFFFCC00),
        ),
      ),
      home: const PrimeVideoWebScreen(),
    );
  }
}

class PrimeVideoWebScreen extends StatefulWidget {
  const PrimeVideoWebScreen({super.key});

  @override
  State<PrimeVideoWebScreen> createState() => _PrimeVideoWebScreenState();
}

class _PrimeVideoWebScreenState extends State<PrimeVideoWebScreen> {
  String? _injectedJsCode;
  InAppWebViewController? _controller;
  bool _isLoading = true;
  double _loadingProgress = 0;
  bool _isFullscreen = false;

  static const _telemetryHostPrefixes = [
    'unagi',
    'device-metrics',
    'fls-na.',
    'fls-eu.',
    'fls-fe.',
  ];

  @override
  void initState() {
    super.initState();
    _loadJsAsset();
  }

  @override
  void dispose() {
    SystemChrome.setPreferredOrientations(DeviceOrientation.values);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  Future<void> _loadJsAsset() async {
    String? js;
    try {
      js = await rootBundle.loadString('assets/speed-control.js');
    } catch (e) {
      debugPrint('[PVSC-Mobile] Error loading speed-control.js asset: $e');
    }
    if (!mounted) return;
    // The WebView is not built until this completes, because
    // `initialUserScripts` is only read once, when the platform view is
    // created. Building it earlier would permanently lose the
    // AT_DOCUMENT_START injection.
    setState(() {
      _injectedJsCode = js ?? '';
    });
  }

  /// Ad endpoints, which expect a VAST document in reply.
  ///
  /// Matched on host rather than on the URL as a whole: Prime Video's playback
  /// and licence traffic goes to atv-ps.amazon.com and the CloudFront CDNs, and
  /// a substring match risks blocking a path segment those requests share.
  bool _isAdHost(String host) {
    return host.endsWith('amazon-adsystem.com') || host.startsWith('aan.amazon.');
  }

  /// Telemetry endpoints, which expect nothing in particular.
  bool _isTelemetryHost(String host) {
    return _telemetryHostPrefixes.any(host.startsWith);
  }

  /// Re-runs the userscript unless it is already live on the current document.
  /// The script itself is idempotent — it checks `installed` and bails — but
  /// the guard here avoids shipping 60KB of source across the bridge each time.
  Future<void> _ensureScriptInstalled(InAppWebViewController controller) async {
    if (_injectedJsCode == null || _injectedJsCode!.isEmpty) return;
    try {
      await controller.evaluateJavascript(
        source:
            "if (!window.__primeVideoSpeedControl?.installed) { $_injectedJsCode }",
      );
    } catch (_) {}
  }

  /// Android back: close the enhancer menu, then walk the WebView history,
  /// and only leave the app when neither applies.
  Future<void> _handleBack() async {
    final controller = _controller;
    if (controller != null) {
      final closed = await controller.evaluateJavascript(
        source: 'window.__primeVideoSpeedControl?.closeMenu?.() === true',
      );
      if (closed == true || closed == 'true') return;

      if (await controller.canGoBack()) {
        await controller.goBack();
        return;
      }
    }

    // This screen is the root route, so Navigator.pop would be a no-op.
    await SystemNavigator.pop();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop) _handleBack();
      },
      child: Scaffold(
        backgroundColor: Colors.black,
        body: SafeArea(
          top: !_isFullscreen,
          bottom: !_isFullscreen,
          child: _injectedJsCode == null
              ? const Center(
                  child: CircularProgressIndicator(color: Color(0xFF00A8E1)),
                )
              : Stack(
                  children: [
                    InAppWebView(
                      initialUrlRequest: URLRequest(
                        url: WebUri('https://www.primevideo.com'),
                      ),
                      initialUserScripts: _injectedJsCode!.isEmpty
                          ? null
                          : UnmodifiableListView([
                              UserScript(
                                source: _injectedJsCode!,
                                injectionTime:
                                    UserScriptInjectionTime.AT_DOCUMENT_START,
                              ),
                            ]),
                      initialSettings: InAppWebViewSettings(
                        mediaPlaybackRequiresUserGesture: false,
                        allowsInlineMediaPlayback: true,
                        useShouldInterceptRequest: true,
                        javaScriptEnabled: true,
                        domStorageEnabled: true,
                        databaseEnabled: true,
                        cacheEnabled: true,
                        clearCache: false,
                        thirdPartyCookiesEnabled: true,
                        hardwareAcceleration: true,
                        supportZoom: false,
                        builtInZoomControls: false,
                        // Compatibility rather than NEVER_ALLOW: a single http
                        // subresource anywhere in Amazon's stack would
                        // otherwise be hard-blocked on a site we don't control.
                        mixedContentMode:
                            MixedContentMode.MIXED_CONTENT_COMPATIBILITY_MODE,
                        allowFileAccessFromFileURLs: false,
                        allowUniversalAccessFromFileURLs: false,
                        supportMultipleWindows: false,
                        userAgent:
                            'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
                      ),
                      onWebViewCreated: (controller) {
                        _controller = controller;
                      },
                      // Prime Video is Widevine-protected. Without granting
                      // PROTECTED_MEDIA_ID the EME layer cannot generate a
                      // licence request and every title fails with
                      // "Video Unavailable".
                      onPermissionRequest: (controller, request) async {
                        return PermissionResponse(
                          resources: request.resources,
                          action: PermissionResponseAction.GRANT,
                        );
                      },
                      onConsoleMessage: (controller, message) {
                        debugPrint('[PVSC-Console] ${message.message}');
                      },
                      onLoadStart: (controller, url) {
                        setState(() {
                          _isLoading = true;
                        });
                      },
                      onProgressChanged: (controller, progress) {
                        final value = progress / 100.0;
                        // Rebuilding on every tick is wasted work on a
                        // low-end device; the bar only needs coarse steps.
                        if ((value - _loadingProgress).abs() < 0.05 &&
                            value < 1.0) {
                          return;
                        }
                        setState(() {
                          _loadingProgress = value;
                        });
                      },
                      onLoadStop: (controller, url) async {
                        setState(() {
                          _isLoading = false;
                        });
                        await _ensureScriptInstalled(controller);
                      },
                      // Prime Video is a client-side router, so onLoadStop does
                      // not fire when the user moves from the storefront into a
                      // title or the player. Without this the panel is missing
                      // on exactly the pages it exists for.
                      onUpdateVisitedHistory: (controller, url, isReload) {
                        _ensureScriptInstalled(controller);
                      },
                      onReceivedError: (controller, request, error) {
                        if (!request.isForMainFrame!) return;
                        debugPrint('[PVSC-Mobile] Load error: ${error.description}');
                        // Otherwise the progress bar hangs at partial forever.
                        setState(() {
                          _isLoading = false;
                        });
                      },
                      shouldInterceptRequest: (controller, request) async {
                        final host = request.url.host.toLowerCase();
                        if (_isAdHost(host)) {
                          return WebResourceResponse(
                            contentType: 'application/xml',
                            contentEncoding: 'utf-8',
                            data: Uint8List.fromList(
                              utf8.encode('<VAST version="3.0"></VAST>'),
                            ),
                            statusCode: 200,
                            reasonPhrase: 'OK',
                          );
                        }
                        if (_isTelemetryHost(host)) {
                          // An empty 204 rather than the VAST body: a caller
                          // expecting JSON would throw on XML, and a fake
                          // success is harder for Amazon's player to recover
                          // from than a plain empty response.
                          return WebResourceResponse(
                            contentType: 'text/plain',
                            contentEncoding: 'utf-8',
                            data: Uint8List(0),
                            statusCode: 204,
                            reasonPhrase: 'No Content',
                          );
                        }
                        return null;
                      },
                      onEnterFullscreen: (controller) {
                        setState(() => _isFullscreen = true);
                        SystemChrome.setEnabledSystemUIMode(
                            SystemUiMode.immersiveSticky);
                        SystemChrome.setPreferredOrientations([
                          DeviceOrientation.landscapeLeft,
                          DeviceOrientation.landscapeRight,
                        ]);
                      },
                      onExitFullscreen: (controller) {
                        setState(() => _isFullscreen = false);
                        SystemChrome.setEnabledSystemUIMode(
                            SystemUiMode.edgeToEdge);
                        SystemChrome.setPreferredOrientations(
                            DeviceOrientation.values);
                      },
                    ),
                    if (_isLoading && _loadingProgress < 1.0)
                      Positioned(
                        top: 0,
                        left: 0,
                        right: 0,
                        child: LinearProgressIndicator(
                          value: _loadingProgress,
                          backgroundColor: Colors.transparent,
                          color: const Color(0xFF00A8E1),
                          minHeight: 3,
                        ),
                      ),
                  ],
                ),
        ),
      ),
    );
  }
}
