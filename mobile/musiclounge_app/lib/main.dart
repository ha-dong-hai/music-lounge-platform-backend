import 'package:flutter/material.dart';

import 'core/navigation.dart';
import 'core/role_router.dart';
import 'core/session.dart';
import 'core/theme.dart';

void main() {
  runApp(const MusicLoungeApp());
}

class MusicLoungeApp extends StatelessWidget {
  const MusicLoungeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      navigatorKey: rootNavigatorKey,
      title: 'MusicLounge',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home: const RootGate(),
    );
  }
}

/// Decides which screen to show at app start based on the persisted
/// session's role — Staff gets the check-in scanner, Audience gets tickets
/// + F&B ordering. See core/role_router.dart for the role -> screen map.
class RootGate extends StatefulWidget {
  const RootGate({super.key});

  @override
  State<RootGate> createState() => _RootGateState();
}

class _RootGateState extends State<RootGate> {
  late final Future<Widget> _resolved = _resolve();

  Future<Widget> _resolve() async {
    final role = await Session.instance.getRole();
    return screenForRole(role);
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Widget>(
      future: _resolved,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Scaffold(body: Center(child: CircularProgressIndicator()));
        }
        return snapshot.data!;
      },
    );
  }
}
