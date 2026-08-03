import 'package:flutter_test/flutter_test.dart';

import 'package:prime_video_enhancer_mobile/main.dart';

void main() {
  test('public app widgets can be instantiated', () {
    expect(const PrimeVideoEnhancerApp(), isA<PrimeVideoEnhancerApp>());
    expect(const PrimeVideoWebScreen(), isA<PrimeVideoWebScreen>());
  });
}
