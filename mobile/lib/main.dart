import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
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
  InAppWebViewController? _webViewController;
  String? _injectedJsCode;
  bool _isLoading = true;
  double _loadingProgress = 0;

  // Known Amazon Ad and Telemetry Host Patterns for Network-level Blocking
  final List<RegExp> _adHostPatterns = [
    RegExp(r'amazon-adsystem\.com', caseSensitive: false),
    RegExp(r'aan\.amazon\.co', caseSensitive: false),
    RegExp(r'/unagi/', caseSensitive: false),
    RegExp(r'/device-metrics/', caseSensitive: false),
    RegExp(r'/mads/', caseSensitive: false),
    RegExp(r'/fls-na\.amazon\.com', caseSensitive: false),
    RegExp(r'/fls-eu\.amazon\.com', caseSensitive: false),
    RegExp(r'/fls-fe\.amazon\.com', caseSensitive: false),
  ];

  @override
  void initState() {
    super.initState();
    _loadJsAsset();
  }

  Future<void> _loadJsAsset() async {
    try {
      final jsString = await rootBundle.loadString('assets/speed-control.js');
      setState(() {
        _injectedJsCode = jsString;
      });
    } catch (e) {
      debugPrint('[PVSC-Mobile] Error loading speed-control.js asset: $e');
    }
  }

  Future<void> _injectControllerScript() async {
    if (_webViewController == null || _injectedJsCode == null) return;
    try {
      await _webViewController!.evaluateJavascript(source: _injectedJsCode!);
      debugPrint('[PVSC-Mobile] Injected speed-control.js successfully');
    } catch (e) {
      debugPrint('[PVSC-Mobile] Script injection error: $e');
    }
  }

  bool _isAdRequest(String url) {
    for (final pattern in _adHostPatterns) {
      if (pattern.hasMatch(url)) {
        return true;
      }
    }
    return false;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: SafeArea(
        child: Stack(
          children: [
            InAppWebView(
              initialUrlRequest: URLRequest(
                url: WebUri('https://www.primevideo.com'),
              ),
              initialSettings: InAppWebViewSettings(
                mediaPlaybackRequiresUserGesture: false,
                allowsInlineMediaPlayback: true,
                useShouldInterceptRequest: true,
                javaScriptEnabled: true,
                domStorageEnabled: true,
                databaseEnabled: true,
                supportMultipleWindows: false,
                userAgent:
                    'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
              ),
              onWebViewCreated: (controller) {
                _webViewController = controller;
              },
              onLoadStart: (controller, url) {
                setState(() {
                  _isLoading = true;
                });
              },
              onProgressChanged: (controller, progress) {
                setState(() {
                  _loadingProgress = progress / 100.0;
                });
                if (progress > 60) {
                  _injectControllerScript();
                }
              },
              onLoadStop: (controller, url) async {
                setState(() {
                  _isLoading = false;
                });
                await _injectControllerScript();
              },
              shouldInterceptRequest: (controller, request) async {
                final url = request.url.toString();
                if (_isAdRequest(url)) {
                  debugPrint('[PVSC-Mobile] Blocked Ad Request: $url');
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
                return null;
              },
              onEnterFullscreen: (controller) {
                SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
              },
              onExitFullscreen: (controller) {
                SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
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
    );
  }
}
