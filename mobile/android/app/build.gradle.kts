plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "com.iacbi.primevideoenhancer.prime_video_enhancer_mobile"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.iacbi.primevideoenhancer.prime_video_enhancer_mobile"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    // The APK is downloaded from GitHub, not delivered by Play, so download size
    // is what users feel. Uncompressed native libraries (the AGP default) made
    // it ~44 MB; compressed it is about half, at the cost of extracting them
    // once at install.
    packaging {
        jniLibs {
            useLegacyPackaging = true
        }
    }

    // Release signing material comes from the environment (CI secrets); the
    // keystore is never committed. Android accepts an update only when the
    // signing certificate matches the installed one, so a debug key - which the
    // Android Gradle Plugin regenerates on every fresh machine - makes every
    // release unable to update the one before it, and leaves users with nothing
    // to verify a downloaded APK against. Published builds must use the stable
    // key; the debug fallback below exists only for local `flutter run --release`.
    val releaseKeystorePath = System.getenv("PVSC_RELEASE_KEYSTORE")
        ?.takeIf { it.isNotBlank() }
        ?.also {
            // Asked for a release key but it is not there: fail rather than fall
            // back to the debug key, which is the outcome this block exists to stop.
            if (!file(it).exists()) {
                throw GradleException("PVSC_RELEASE_KEYSTORE points to a missing file: $it")
            }
        }

    signingConfigs {
        if (releaseKeystorePath != null) {
            create("release") {
                storeFile = file(releaseKeystorePath)
                storePassword = System.getenv("PVSC_RELEASE_KEYSTORE_PASSWORD")
                keyAlias = System.getenv("PVSC_RELEASE_KEY_ALIAS")
                keyPassword = System.getenv("PVSC_RELEASE_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            signingConfig = if (releaseKeystorePath != null) {
                signingConfigs.getByName("release")
            } else {
                logger.warn(
                    "PVSC: no release keystore configured - signing with the debug key. " +
                        "This build must not be published."
                )
                signingConfigs.getByName("debug")
            }
        }
    }
}

flutter {
    source = "../.."
}
