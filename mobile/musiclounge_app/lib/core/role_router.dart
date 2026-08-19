import 'package:flutter/material.dart';

import '../features/audience/audience_shell.dart';
import '../features/auth/login_screen.dart';
import '../features/staff/checkin_screen.dart';

/// This app only serves two roles: Staff (door check-in) and Audience
/// (tickets + F&B ordering). Any other role falls back to the login screen.
Widget screenForRole(String? role) {
  switch (role) {
    case 'Staff':
      return const StaffCheckInScreen();
    case 'Audience':
      return const AudienceShell();
    default:
      return const LoginScreen();
  }
}
