import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:musiclounge_app/main.dart';

void main() {
  testWidgets('App boots to the login screen when logged out', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues({});
    await tester.pumpWidget(const MusicLoungeApp());
    await tester.pumpAndSettle();

    expect(find.text('MusicLounge'), findsOneWidget);
    expect(find.byType(TextFormField), findsNWidgets(2));
  });
}
