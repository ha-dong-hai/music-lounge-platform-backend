import 'package:shared_preferences/shared_preferences.dart';

/// Persists the logged-in user's auth token and role across app restarts.
class Session {
  Session._();
  static final Session instance = Session._();

  static const _kToken = 'token';
  static const _kRole = 'role';
  static const _kFullName = 'fullName';
  static const _kUserId = 'userId';
  static const _kEmail = 'email';

  Future<void> save({
    required String token,
    required String role,
    required String fullName,
    required String userId,
    required String email,
  }) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kToken, token);
    await prefs.setString(_kRole, role);
    await prefs.setString(_kFullName, fullName);
    await prefs.setString(_kUserId, userId);
    await prefs.setString(_kEmail, email);
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();
  }

  Future<String?> getToken() async =>
      (await SharedPreferences.getInstance()).getString(_kToken);

  Future<String?> getRole() async =>
      (await SharedPreferences.getInstance()).getString(_kRole);

  Future<String?> getFullName() async =>
      (await SharedPreferences.getInstance()).getString(_kFullName);

  Future<bool> isLoggedIn() async => (await getToken()) != null;
}
