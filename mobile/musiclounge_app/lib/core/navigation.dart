import 'package:flutter/material.dart';

/// Lets non-widget code (ApiClient's 401 handler) navigate without a
/// BuildContext. Set as MaterialApp's `navigatorKey` in main.dart.
final GlobalKey<NavigatorState> rootNavigatorKey = GlobalKey<NavigatorState>();
