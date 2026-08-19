import 'package:flutter/material.dart';

import '../../core/logout.dart';
import 'fnb_my_orders_screen.dart';
import 'fnb_venue_list_screen.dart';
import 'tickets_tab.dart';

/// Audience gets exactly two things in this app: their tickets (with QR for
/// door entry) and F&B ordering at the venue. Nothing else — no show
/// browsing, no livestream, no donations (those live on the main website).
class AudienceShell extends StatefulWidget {
  const AudienceShell({super.key});

  @override
  State<AudienceShell> createState() => _AudienceShellState();
}

class _AudienceShellState extends State<AudienceShell> {
  int _index = 0;

  static const _titles = ['Vé của tôi', 'Đặt đồ ăn'];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[_index]),
        actions: [
          if (_index == 1)
            IconButton(
              icon: const Icon(Icons.receipt_long_outlined),
              tooltip: 'Đơn của tôi',
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const FnbMyOrdersScreen()),
              ),
            ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Đăng xuất',
            onPressed: logout,
          ),
        ],
      ),
      body: IndexedStack(
        index: _index,
        children: const [
          TicketsTab(),
          FnbVenueListScreen(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.confirmation_number_outlined),
            selectedIcon: Icon(Icons.confirmation_number),
            label: 'Vé của tôi',
          ),
          NavigationDestination(
            icon: Icon(Icons.restaurant_outlined),
            selectedIcon: Icon(Icons.restaurant),
            label: 'Đặt đồ ăn',
          ),
        ],
      ),
    );
  }
}
