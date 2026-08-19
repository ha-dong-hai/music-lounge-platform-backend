import 'package:flutter/material.dart';

import '../features/auth/login_screen.dart';
import 'navigation.dart';
import 'session.dart';

/// Clears the session and returns to the login screen. Callable from
/// anywhere — widgets pass no arguments; ApiClient's 401 handler uses the
/// same function via [rootNavigatorKey] instead of a BuildContext.
Future<void> logout() async {
  await Session.instance.clear();
  rootNavigatorKey.currentState?.pushAndRemoveUntil(
    MaterialPageRoute(builder: (_) => const LoginScreen()),
    (route) => false,
  );
}
