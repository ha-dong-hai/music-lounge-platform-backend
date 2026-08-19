import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'logout.dart';
import 'session.dart';

/// Thrown for any non-2xx API response, with a message already resolved
/// from the backend's `{ success, data, message, errors }` envelope.
class ApiException implements Exception {
  final String message;
  final int statusCode;
  ApiException(this.message, this.statusCode);

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient._();
  static final ApiClient instance = ApiClient._();

  static const Duration _timeout = Duration(seconds: 15);

  /// Production API. See docs/api/27-api-cheatsheet.md.
  static const String host = 'https://musiclounge-api.azurewebsites.net';
  static const String baseUrl = '$host/api/v1';

  bool _handlingUnauthorized = false;

  /// Resolves a relative "/uploads/..." path (as returned by list/detail
  /// endpoints) into an absolute, loadable image URL.
  static String? resolveImageUrl(String? path) {
    if (path == null || path.isEmpty) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    return '$host$path';
  }

  Future<Map<String, String>> _headers({bool json = true}) async {
    final headers = <String, String>{};
    if (json) headers['Content-Type'] = 'application/json';
    final token = await Session.instance.getToken();
    if (token != null) headers['Authorization'] = 'Bearer $token';
    return headers;
  }

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final cleanQuery = <String, String>{};
    query?.forEach((key, value) {
      if (value != null) cleanQuery[key] = value.toString();
    });
    return Uri.parse('$baseUrl$path')
        .replace(queryParameters: cleanQuery.isEmpty ? null : cleanQuery);
  }

  /// [wasAuthenticated] marks whether this request carried a bearer token —
  /// a 401 on an authenticated request means the session expired and should
  /// force a logout; a 401 on an unauthenticated one (login/register) just
  /// means bad credentials and must not touch the session.
  dynamic _decode(http.Response resp, {required bool wasAuthenticated}) {
    Map<String, dynamic>? body;
    if (resp.body.isNotEmpty) {
      try {
        final decoded = jsonDecode(resp.body);
        if (decoded is Map<String, dynamic>) body = decoded;
      } catch (_) {
        // Non-JSON body (e.g. 204 No Content) — leave body null.
      }
    }
    if (resp.statusCode >= 200 && resp.statusCode < 300) {
      return body?['data'];
    }
    if (resp.statusCode == 401 && wasAuthenticated) {
      unawaited(_handleUnauthorized());
    }
    throw ApiException(_extractErrorMessage(body, resp.statusCode), resp.statusCode);
  }

  Future<void> _handleUnauthorized() async {
    if (_handlingUnauthorized) return;
    _handlingUnauthorized = true;
    try {
      await logout();
    } finally {
      _handlingUnauthorized = false;
    }
  }

  String _extractErrorMessage(Map<String, dynamic>? body, int statusCode) {
    final message = body?['message'];
    if (message is String && message.isNotEmpty) return message;
    final errors = body?['errors'];
    if (errors is Map) {
      final firstList = errors.values.whereType<List>().firstOrNull;
      final firstMessage = firstList?.whereType<String>().firstOrNull;
      if (firstMessage != null) return firstMessage;
    }
    return 'Đã có lỗi xảy ra (mã $statusCode)';
  }

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) async {
    final headers = await _headers(json: false);
    final resp = await http.get(_uri(path, query), headers: headers).timeout(_timeout);
    return _decode(resp, wasAuthenticated: headers.containsKey('Authorization'));
  }

  Future<dynamic> post(String path, {Object? body}) async {
    final headers = await _headers();
    final resp = await http
        .post(_uri(path), headers: headers, body: body == null ? null : jsonEncode(body))
        .timeout(_timeout);
    return _decode(resp, wasAuthenticated: headers.containsKey('Authorization'));
  }

  Future<dynamic> put(String path, {Object? body}) async {
    final headers = await _headers();
    final resp = await http
        .put(_uri(path), headers: headers, body: body == null ? null : jsonEncode(body))
        .timeout(_timeout);
    return _decode(resp, wasAuthenticated: headers.containsKey('Authorization'));
  }
}

extension _FirstOrNull<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
