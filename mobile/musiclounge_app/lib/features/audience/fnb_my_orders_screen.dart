import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/status.dart';
import '../../models/fnb.dart';
import 'fnb_order_detail_screen.dart';

class FnbMyOrdersScreen extends StatefulWidget {
  const FnbMyOrdersScreen({super.key});

  @override
  State<FnbMyOrdersScreen> createState() => _FnbMyOrdersScreenState();
}

class _FnbMyOrdersScreenState extends State<FnbMyOrdersScreen> {
  late Future<List<FnbOrder>> _future;
  final Map<int, String> _loungeNameCache = {};

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<FnbOrder>> _load() async {
    final data =
        await ApiClient.instance.get('/fnb-orders/my', query: {'page': 1, 'pageSize': 50});
    final items = (data as Map<String, dynamic>)['items'] as List<dynamic>;
    final orders = items.map((e) => FnbOrder.fromJson(e as Map<String, dynamic>)).toList();
    orders.sort((a, b) => b.createdAt.compareTo(a.createdAt));
    // Resolve every venue name once, up front, so the list below can render
    // synchronously from cache instead of nesting a FutureBuilder per row
    // (which would re-fire on every rebuild and flicker).
    final uniqueLoungeIds = orders.map((o) => o.loungeId).toSet();
    await Future.wait(uniqueLoungeIds.map(_fetchLoungeName));
    return orders;
  }

  Future<void> _refresh() async {
    final future = _load();
    setState(() => _future = future);
    // FutureBuilder already surfaces a failure via the reassigned _future;
    // swallow it here so RefreshIndicator doesn't also report it as an
    // unhandled exception.
    try {
      await future;
    } catch (_) {
      // Handled by the FutureBuilder above.
    }
  }

  Future<void> _fetchLoungeName(int loungeId) async {
    if (_loungeNameCache.containsKey(loungeId)) return;
    try {
      final data = await ApiClient.instance.get('/lounges/$loungeId');
      _loungeNameCache[loungeId] =
          (data as Map<String, dynamic>)['name'] as String? ?? 'Venue #$loungeId';
    } catch (_) {
      _loungeNameCache[loungeId] = 'Venue #$loungeId';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Đơn của tôi')),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: FutureBuilder<List<FnbOrder>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return ListView(children: [
                const SizedBox(height: 100),
                Center(child: Text('Không tải được đơn hàng: ${snapshot.error}')),
              ]);
            }
            final orders = snapshot.data!;
            if (orders.isEmpty) {
              return ListView(children: const [
                SizedBox(height: 120),
                Center(child: Text('Bạn chưa đặt đơn nào.')),
              ]);
            }
            return ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: orders.length,
              separatorBuilder: (context, index) => const SizedBox(height: 12),
              itemBuilder: (context, i) {
                final order = orders[i];
                final color = StatusColors.forFnbOrder(order.status);
                return Card(
                  child: InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => FnbOrderDetailScreen(orderId: order.id)),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  _loungeNameCache[order.loungeId] ?? 'Venue #${order.loungeId}',
                                  style: Theme.of(context).textTheme.titleMedium,
                                ),
                              ),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: color.withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(999),
                                ),
                                child: Text(
                                  StatusColors.labelForFnbOrder(order.status),
                                  style:
                                      TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 6),
                          Text(
                              'Đơn #${order.id} · ${order.items.length} món · ${formatCurrency(order.totalAmount)}'),
                          const SizedBox(height: 4),
                          Text(formatDateTime(order.createdAt),
                              style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ),
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}
