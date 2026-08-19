import 'package:google_sign_in/google_sign_in.dart';

/// The Web-type OAuth client from the "sign-in-52d07" Google Cloud project
/// (docs/secrets/Google_Sign-In/). Passed as `serverClientId` so the ID token this
/// app receives has the audience the backend's POST /auth/google verifier
/// expects — the Android-type OAuth client (package name + SHA-1) is
/// registered separately in the same GCP project but never referenced here.
const String _googleServerClientId =
    '613416227665-8v9cu76sqoqmismobcdg9ibfgbu0a01j.apps.googleusercontent.com';

bool _initialized = false;

Future<void> _ensureInitialized() async {
  if (_initialized) return;
  await GoogleSignIn.instance.initialize(serverClientId: _googleServerClientId);
  _initialized = true;
}

/// Thrown when Google reports a successful sign-in but no ID token comes
/// back — a configuration problem (missing SHA-1 / iOS URL scheme), not a
/// user cancel, so it must not be swallowed the same way a cancel is.
class GoogleSignInMissingTokenException implements Exception {
  const GoogleSignInMissingTokenException();
  @override
  String toString() => 'Google sign-in succeeded but returned no ID token.';
}

/// Runs the Google sign-in flow and returns the ID token to send to
/// `POST /auth/google`. Returns null only if the user cancels the flow;
/// any other failure to obtain a token throws.
Future<String?> signInWithGoogleGetIdToken() async {
  await _ensureInitialized();
  try {
    final account = await GoogleSignIn.instance.authenticate();
    final idToken = account.authentication.idToken;
    if (idToken == null) throw const GoogleSignInMissingTokenException();
    return idToken;
  } on GoogleSignInException catch (e) {
    if (e.code == GoogleSignInExceptionCode.canceled) return null;
    rethrow;
  }
}
