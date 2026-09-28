import 'dart:collection';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';

import 'web_rules.dart';

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
  String _scriptVersion = '';
  String? _userAgent;
  InAppWebViewController? _controller;
  bool _isLoading = true;
  double _loadingProgress = 0;
  bool _isFullscreen = false;

  @override
  void initState() {
    super.initState();
    GestureBinding.instance.pointerRouter.addGlobalRoute(_onPointerEvent);
    _loadJsAsset();
  }

  @override
  void dispose() {
    GestureBinding.instance.pointerRouter.removeGlobalRoute(_onPointerEvent);
    SystemChrome.setPreferredOrientations(DeviceOrientation.values);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  /// Android only: iOS keeps WKWebView's own Safari agent. The old pinned string
  /// was sent on iOS too, where claiming Android Chrome points Prime Video at
  /// Widevine, which WKWebView does not have.
  static Future<String?> _resolveUserAgent() async {
    if (defaultTargetPlatform != TargetPlatform.android) return null;
    try {
      return browserUserAgent(await InAppWebViewController.getDefaultUserAgent());
    } catch (e) {
      debugPrint('[PVSC-Mobile] Could not read the WebView user agent: $e');
      return null;
    }
  }

  Future<void> _loadJsAsset() async {
    final userAgent = _resolveUserAgent();
    String? js;
    try {
      js = await rootBundle.loadString('assets/speed-control.js');
    } catch (e) {
      debugPrint('[PVSC-Mobile] Error loading speed-control.js asset: $e');
    }
    _userAgent = await userAgent;
    if (!mounted) return;
    // Read the version out of the script rather than repeating it here. It is
    // already duplicated across the csproj, the pubspec and the test suite, and
    // one more hand-maintained copy is one more thing to drift.
    final version = js == null
        ? null
        : RegExp(r'const VERSION = "([^"]+)"').firstMatch(js)?.group(1);
    // The WebView is not built until this completes, because
    // `initialUserScripts` is only read once, when the platform view is
    // created. Building it earlier would permanently lose the
    // AT_DOCUMENT_START injection.
    _update(() {
      _injectedJsCode = js ?? '';
      _scriptVersion = version ?? '';
    });
  }

  /// Re-runs the userscript unless the current version is already live on the
  /// document. The script itself is idempotent — it compares versions and either
  /// bails or tears the old copy down — but the guard here avoids shipping 90KB
  /// of source across the bridge on every SPA navigation.
  ///
  /// Checks the version, not just `installed`: the desktop host has always done
  /// so, and matching it means a stale copy left over from a cached document is
  /// replaced rather than kept forever.
  Future<void> _ensureScriptInstalled(InAppWebViewController controller) async {
    if (_injectedJsCode == null || _injectedJsCode!.isEmpty) return;
    try {
      // Probe first: the guard in the payload below only runs after the whole
      // source has already crossed the bridge.
      final live = await controller.evaluateJavascript(
        source:
            "window.__primeVideoSpeedControl?.version === '$_scriptVersion'",
      );
      if (live == true || live == 'true') return;
      // Still guarded: onLoadStop and onUpdateVisitedHistory can race here.
      await controller.evaluateJavascript(
        source:
            "if (window.__primeVideoSpeedControl?.version !== '$_scriptVersion') { $_injectedJsCode }",
      );
    } catch (_) {}
  }

  /// Whether the previous back press was already spent closing the enhancer
  /// menu. Reset whenever a back press does anything else, on navigation, and
  /// on a real touch — reopening the menu takes one, and a page cannot forge
  /// it, so the next back press closes the menu again as expected.
  bool _lastBackClosedMenu = false;

  /// setState for platform callbacks, which can arrive after the screen is gone.
  void _update(VoidCallback change) {
    if (mounted) setState(change);
  }

  void _onPointerEvent(PointerEvent event) {
    if (event is PointerDownEvent) _lastBackClosedMenu = false;
  }

  /// Android back: close the enhancer menu, then walk the WebView history,
  /// and only leave the app when neither applies.
  ///
  /// The menu verdict comes from `window.__primeVideoSpeedControl`, a plain
  /// page-world global with no isolation available on this plugin's Android
  /// path, so the displayed page can forge it: a hostile page that answers
  /// "I closed my menu" to every press would swallow the back button forever
  /// and leave no way out of it inside a WebView that has no address bar.
  /// Honouring the verdict at most once per real touch keeps the real
  /// behaviour (first press closes the menu, second navigates) while capping
  /// the damage a forged one can do at a single ignored press.
  Future<void> _handleBack() async {
    final controller = _controller;
    if (controller != null) {
      // A page mid-navigation can make these throw; Back must still do something.
      try {
        final closed = await controller.evaluateJavascript(
          source: 'window.__primeVideoSpeedControl?.closeMenu?.() === true',
        );
        if ((closed == true || closed == 'true') && !_lastBackClosedMenu) {
          _lastBackClosedMenu = true;
          return;
        }
      } catch (_) {}
      _lastBackClosedMenu = false;

      try {
        if (await controller.canGoBack()) {
          await controller.goBack();
          return;
        }
      } catch (_) {}
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
                        useShouldOverrideUrlLoading: true,
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
                        userAgent: _userAgent,
                      ),
                      onWebViewCreated: (controller) {
                        _controller = controller;
                      },
                      // Prime Video is Widevine-protected. Without granting
                      // PROTECTED_MEDIA_ID the EME layer cannot generate a
                      // licence request and every title fails with
                      // "Video Unavailable".
                      //
                      // Only that, though. This used to grant `request.resources`
                      // wholesale, which meant any page reachable in the WebView
                      // could take the camera, the microphone or location without
                      // a prompt — the comment above justified the DRM case, but
                      // the code never looked at what was being asked for. And only
                      // to Amazon and Prime Video: the origin is checked too, so a
                      // frame from anywhere else cannot ask for the device's DRM id.
                      onPermissionRequest: (controller, request) async {
                        final granted = isFirstPartyHost(
                                request.origin.host.toLowerCase())
                            ? request.resources
                                .where((resource) =>
                                    resource ==
                                    PermissionResourceType.PROTECTED_MEDIA_ID)
                                .toList()
                            : <PermissionResourceType>[];
                        if (granted.isEmpty) {
                          return PermissionResponse(
                            resources: request.resources,
                            action: PermissionResponseAction.DENY,
                          );
                        }
                        return PermissionResponse(
                          resources: granted,
                          action: PermissionResponseAction.GRANT,
                        );
                      },
                      // debugPrint is not stripped from release builds, so
                      // without the gate every console line from a signed-in
                      // Prime Video page lands in logcat.
                      onConsoleMessage: kDebugMode || _webViewDebug
                          ? (controller, message) {
                              debugPrint('[PVSC-Console] ${message.message}');
                            }
                          : null,
                      onLoadStart: (controller, url) {
                        _update(() {
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
                        _update(() {
                          _loadingProgress = value;
                        });
                      },
                      onLoadStop: (controller, url) async {
                        _update(() {
                          _isLoading = false;
                        });
                        _lastBackClosedMenu = false;
                        await _ensureScriptInstalled(controller);
                      },
                      // Prime Video is a client-side router, so onLoadStop does
                      // not fire when the user moves from the storefront into a
                      // title or the player. Without this the panel is missing
                      // on exactly the pages it exists for.
                      onUpdateVisitedHistory: (controller, url, isReload) {
                        _lastBackClosedMenu = false;
                        _ensureScriptInstalled(controller);
                      },
                      onReceivedError: (controller, request, error) {
                        // Nullable: not every platform reports it, and a
                        // force-unwrap would throw inside the callback.
                        if (request.isForMainFrame == false) return;
                        debugPrint('[PVSC-Mobile] Load error: ${error.description}');
                        // Otherwise the progress bar hangs at partial forever.
                        _update(() {
                          _isLoading = false;
                        });
                      },
                      shouldInterceptRequest: (controller, request) async {
                        final host = request.url.host.toLowerCase();
                        final path = request.url.path.toLowerCase();
                        if (isAdRequest(host, path)) {
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
                        if (isTelemetryRequest(host, path)) {
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
                      // The app has no address bar, so a page from anywhere but
                      // Amazon and Prime Video would look exactly like Prime Video
                      // while holding the controller and the DRM permission. Say
                      // so instead of failing silently: a sign-in that redirects
                      // somewhere unexpected is otherwise a dead end.
                      shouldOverrideUrlLoading: (controller, action) async {
                        final url = action.request.url;
                        if (url == null || isAllowedNavigation(url.uriValue)) {
                          return NavigationActionPolicy.ALLOW;
                        }
                        debugPrint('[PVSC-Mobile] Blocked navigation to $url');
                        if (mounted) {
                          ScaffoldMessenger.of(context)
                            ..hideCurrentSnackBar()
                            ..showSnackBar(SnackBar(
                              content: Text(
                                  'Blocked a link to ${url.host.isEmpty ? url.scheme : url.host}'),
                              duration: const Duration(seconds: 3),
                            ));
                        }
                        return NavigationActionPolicy.CANCEL;
                      },
                      onEnterFullscreen: (controller) {
                        _update(() => _isFullscreen = true);
                        SystemChrome.setEnabledSystemUIMode(
                            SystemUiMode.immersiveSticky);
                        SystemChrome.setPreferredOrientations([
                          DeviceOrientation.landscapeLeft,
                          DeviceOrientation.landscapeRight,
                        ]);
                      },
                      onExitFullscreen: (controller) {
                        _update(() => _isFullscreen = false);
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
