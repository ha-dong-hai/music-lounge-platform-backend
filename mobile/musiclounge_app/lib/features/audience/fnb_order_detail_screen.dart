import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/status.dart';
import '../../models/fnb.dart';

class FnbOrderDetailScreen extends StatefulWidget {
  final int orderId;
  const FnbOrderDetailScreen({super.key, required this.orderId});

  @override
  State<FnbOrderDetailScreen> createState() => _FnbOrderDetailScreenState();
}

class _FnbOrderDetailScreenState extends State<FnbOrderDetailScreen> {
  late Future<FnbOrder> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<FnbOrder> _load() async {
    final data = await ApiClient.instance.get('/fnb-orders/${widget.orderId}');
    return FnbOrder.fromJson(data as Map<String, dynamic>);
  }

  Future<void> _refresh() async {
    final future = _load();
    setState(() => _future = future);
    try {
      await future;
    } catch (_) {
      // Handled by the FutureBuilder above.
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Đơn #${widget.orderId}'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), tooltip: 'Làm mới', onPressed: _refresh),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: FutureBuilder<FnbOrder>(
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
            final order = snapshot.data!;
            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                Text(
                  'Trạng thái đơn hàng không tự cập nhật — kéo xuống để làm mới.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(color: Colors.black54),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                _StatusStepper(status: order.status),
                const SizedBox(height: 24),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        for (final item in order.items)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 4),
                            child: Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    '${item.menuItemName} x${item.quantity}',
                                    style: item.cancelled
                                        ? const TextStyle(
                                            decoration: TextDecoration.lineThrough,
                                            color: Colors.black45)
                                        : null,
                                  ),
                                ),
                                Text(formatCurrency(item.unitPrice * item.quantity)),
                              ],
                            ),
                          ),
                        const Divider(height: 24),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text('Tổng cộng', style: Theme.of(context).textTheme.titleMedium),
                            Text(formatCurrency(order.totalAmount),
                                style: Theme.of(context)
                                    .textTheme
                                    .titleMedium
                                    ?.copyWith(fontWeight: FontWeight.bold)),
                          ],
                        ),
                        if (order.tableNote != null) ...[
                          const SizedBox(height: 8),
                          Text('Bàn: ${order.tableNote}'),
                        ],
                        if (order.note != null) ...[
                          const SizedBox(height: 4),
                          Text('Ghi chú: ${order.note}'),
                        ],
                        const SizedBox(height: 8),
                        Text('Đặt lúc: ${formatDateTime(order.createdAt)}',
                            style: Theme.of(context).textTheme.bodySmall),
                      ],
                    ),
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _StatusStepper extends StatelessWidget {
  final String status;
  const _StatusStepper({required this.status});

  @override
  Widget build(BuildContext context) {
    if (status == 'Cancelled') {
      return Card(
        color: const Color(0xFFFFDAD6),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: const [
              Icon(Icons.cancel_outlined, color: Color(0xFFBA1A1A)),
              SizedBox(width: 12),
              Text('Đơn hàng đã bị huỷ',
                  style: TextStyle(color: Color(0xFFBA1A1A), fontWeight: FontWeight.w600)),
            ],
          ),
        ),
      );
    }
    final steps = FnbOrder.statusSequence;
    final currentIndex = steps.indexOf(status);
    return Row(
      children: [
        for (int i = 0; i < steps.length; i++) ...[
          Expanded(
            flex: 3,
            child: Column(
              children: [
                CircleAvatar(
                  radius: 16,
                  backgroundColor:
                      i <= currentIndex ? StatusColors.forFnbOrder(steps[i]) : const Color(0xFFE0E0E0),
                  child: Icon(
                    i < currentIndex ? Icons.check : Icons.circle,
                    size: i < currentIndex ? 18 : 10,
                    color: Colors.white,
                  ),
                ),
                const SizedBox(height: 6),
                Text(StatusColors.labelForFnbOrder(steps[i]),
                    style: const TextStyle(fontSize: 11), textAlign: TextAlign.center),
              ],
            ),
          ),
          if (i < steps.length - 1)
            Expanded(
              flex: 1,
              child: Container(
                height: 2,
                color: i < currentIndex ? StatusColors.forFnbOrder(steps[i]) : const Color(0xFFE0E0E0),
              ),
            ),
        ],
      ],
    );
  }
}
