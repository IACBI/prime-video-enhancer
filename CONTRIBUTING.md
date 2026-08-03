# Contributing

Thank you for helping improve Prime Video Speed & Subtitle Controller. Small, focused changes with clear validation are easiest to review and safest for users.

Please also follow the repository [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- Search existing issues and releases before reporting a duplicate problem.
- Open an issue first for large features, behaviour changes, or UI redesigns.
- Do not report vulnerabilities, credentials, cookies, or account data in a public issue; follow [SECURITY.md](SECURITY.md) instead.

## Local setup

### Windows desktop app

```powershell
dotnet build -c Release
dotnet run -c Release
```

### Mobile app

```powershell
cd mobile
flutter pub get
flutter analyze
flutter test
```

## Required checks

Run the checks relevant to the files you changed:

```powershell
dotnet run --project .\PrimeVideoSpeedApp.Tests\PrimeVideoSpeedApp.Tests.csproj -c Release
node --check .\speed-control.js

cd mobile
flutter analyze
flutter test
flutter build apk --release
```

The optional `PrimeVideoSpeedApp.Tests/browser-smoke.js` test needs a running local desktop session. Do not use a real account or personal data in screenshots, logs, or issue attachments.

## Controller changes

The desktop controller at `speed-control.js` is mirrored at `mobile/assets/speed-control.js`. Changes to one must be applied to the other. Keep these values aligned in the same pull request:

- the version exported by both controller copies;
- `CdpPayloads.ScriptVersion` in `Program.cs`;
- the version expectations in desktop and browser-smoke tests.

## Pull requests

- Keep the diff focused and explain the user-facing effect.
- Update documentation, release notes, and tests when behaviour changes.
- Avoid unrelated formatting or dependency upgrades.
- Confirm that generated output, local profiles, keys, and release artifacts are not included in the commit.

By contributing, you agree that your contribution may be distributed under the [MIT License](LICENSE).
