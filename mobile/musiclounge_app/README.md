# MusicLounge Mobile (Android)

Flutter app for the two roles that need a phone in hand at the venue:

- **Staff** — scan a ticket's QR at the door and check it in. Nothing else (no
  walk-in sales, no F&B order board) — this is intentionally narrower than
  [docs/stitch/stitch-brief-staff-mobile.md](../../docs/stitch/stitch-brief-staff-mobile.md).
- **Audience** — view your tickets (with QR for entry) and order food/drinks
  at the venue.

One app, one login screen; the role in the login response decides which UI
you land on (`lib/core/role_router.dart`). Any other role (Owner/Admin) is
out of scope and just gets bounced back to login.

## Run it

```
flutter pub get
flutter run                 # needs an Android emulator or a USB-debugging device
flutter build apk --debug   # or: --release, once you add a real signing config
```

Talks to the production API (`https://musiclounge-api.azurewebsites.net`) by
default — see `lib/core/api_client.dart` if you need to point it at a local
backend (`10.0.2.2` for the Android emulator's localhost).

## Google Sign-In

Login screen has an email/password form plus a Google button
(`lib/core/google_auth_service.dart`), both hitting the existing backend
`/auth/login` / `/auth/google` endpoints. Two separate OAuth client
registrations are involved, both under the `sign-in-52d07` Google Cloud
project (see `docs/secrets/Google_Sign-In/`):

- **Web client** `613416227665-8v9cu76sqoqmismobcdg9ibfgbu0a01j...` — the one
  in the `client_secret_*.json` file. Passed as `serverClientId` in
  `google_auth_service.dart` so the ID token this app gets back has the
  audience the backend's verifier expects. This is the only ID referenced in
  code.
- **Android client** `613416227665-5dj4eo2ulglvqacks6snrbdv1ahiin74...` —
  registered separately in Cloud Console (Credentials → Create Credentials →
  Android), tied to package name `com.musiclounge.musiclounge_app` + the
  debug keystore's SHA-1 fingerprint. Never referenced in code — it's purely
  how Google's Credential Manager verifies the calling app is legitimate.
  **When you build a signed release APK**, you'll need to register that
  release keystore's SHA-1 here too (debug and release keystores have
  different fingerprints), or Google Sign-In will fail on release builds
  with a `clientConfigurationError`.

## Windows note

`android/gradle.properties` disables Kotlin incremental compilation
(`kotlin.incremental=false`). Without it, `mobile_scanner`'s Kotlin compile
step crashes on Windows whenever the project drive (here, `J:`) differs from
the Pub cache / Gradle cache drive (`C:`) — the incremental compiler can't
express a relative path across drive roots. Rebuilds are a bit slower as a
result; app behavior is unaffected.

## iOS later

Same Dart codebase — `flutter build ios` from a Mac with Xcode installed. No
rewrite needed; you'd only need a Mac and (to publish or install on a real
device) an Apple Developer account.
