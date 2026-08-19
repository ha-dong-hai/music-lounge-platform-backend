import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../models/fnb.dart';
import 'fnb_order_detail_screen.dart';

class FnbMenuScreen extends StatefulWidget {
  final int loungeId;
  final String loungeName;
  const FnbMenuScreen({super.key, required this.loungeId, required this.loungeName});

  @override
  State<FnbMenuScreen> createState() => _FnbMenuScreenState();
}

class _FnbMenuScreenState extends State<FnbMenuScreen> {
  late Future<List<FnbMenuItem>> _future;
  final Map<int, int> _cart = {}; // menuItemId -> quantity
  Map<int, FnbMenuItem> _itemsById = {};
  bool _placing = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<FnbMenuItem>> _load() async {
    final menusData = await ApiClient.instance
        .get('/fnb-menus', query: {'loungeId': widget.loungeId, 'activeOnly': true});
    final menus =
        (menusData as List<dynamic>).map((e) => FnbMenu.fromJson(e as Map<String, dynamic>)).toList();
    final itemLists = await Future.wait(menus.map((menu) async {
      final data = await ApiClient.instance
          .get('/fnb-menu-items', query: {'menuId': menu.id, 'availableOnly': true});
      return (data as List<dynamic>)
          .map((e) => FnbMenuItem.fromJson(e as Map<String, dynamic>))
          .toList();
    }));
    final items = itemLists.expand((e) => e).toList();
    _itemsById = {for (final item in items) item.id: item};
    return items;
  }

  int get _cartCount => _cart.values.fold(0, (a, b) => a + b);

  double get _cartTotal =>
      _cart.entries.fold(0.0, (sum, e) => sum + (_itemsById[e.key]?.price ?? 0) * e.value);

  void _addItem(FnbMenuItem item) {
    setState(() => _cart[item.id] = (_cart[item.id] ?? 0) + 1);
  }

  void _removeItem(FnbMenuItem item) {
    setState(() {
      final current = _cart[item.id] ?? 0;
      if (current <= 1) {
        _cart.remove(item.id);
      } else {
        _cart[item.id] = current - 1;
      }
    });
  }

  Future<void> _openCart() async {
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _CartSheet(
        cart: _cart,
        itemsById: _itemsById,
        onIncrement: _addItem,
        onDecrement: _removeItem,
      ),
    );
    if (confirmed == true) {
      await _placeOrder();
    }
  }

  Future<void> _placeOrder() async {
    if (_cart.isEmpty || _placing) return;
    setState(() => _placing = true);
    try {
      final items = _cart.entries.map((e) => {'menuItemId': e.key, 'quantity': e.value}).toList();
      final result = await ApiClient.instance.post('/fnb-orders', body: {
        'loungeId': widget.loungeId,
        'paymentMethod': 'Cash',
        'items': items,
      });
      final orderId = (result as num).toInt();
      if (!mounted) return;
      setState(() => _cart.clear());
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => FnbOrderDetailScreen(orderId: orderId)),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Không thể đặt hàng. Vui lòng thử lại.')));
    } finally {
      if (mounted) setState(() => _placing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.loungeName)),
      body: FutureBuilder<List<FnbMenuItem>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Không tải được thực đơn: ${snapshot.error}'));
          }
          final items = snapshot.data!;
          if (items.isEmpty) {
            return const Center(child: Text('Phòng trà này chưa có thực đơn.'));
          }
          final byCategory = <String, List<FnbMenuItem>>{};
          for (final item in items) {
            byCategory.putIfAbsent(item.category, () => []).add(item);
          }
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
            children: [
              for (final entry in byCategory.entries) ...[
                Padding(
                  padding: const EdgeInsets.only(bottom: 8, top: 8),
                  child: Text(entry.key,
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(fontWeight: FontWeight.bold)),
                ),
                for (final item in entry.value)
                  _MenuItemTile(
                    item: item,
                    quantity: _cart[item.id] ?? 0,
                    onAdd: () => _addItem(item),
                    onRemove: () => _removeItem(item),
                  ),
                const SizedBox(height: 8),
              ],
            ],
          );
        },
      ),
      bottomNavigationBar: _cartCount > 0
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton(
                  onPressed: _placing ? null : _openCart,
                  child: Text('Giỏ hàng · $_cartCount món · ${formatCurrency(_cartTotal)}'),
                ),
              ),
            )
          : null,
    );
  }
}

class _MenuItemTile extends StatelessWidget {
  final FnbMenuItem item;
  final int quantity;
  final VoidCallback onAdd;
  final VoidCallback onRemove;

  const _MenuItemTile({
    required this.item,
    required this.quantity,
    required this.onAdd,
    required this.onRemove,
  });

  @override
  Widget build(BuildContext context) {
    final imageUrl = ApiClient.resolveImageUrl(item.imageUrl);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(10),
              child: imageUrl != null
                  ? Image.network(imageUrl,
                      width: 56, height: 56, fit: BoxFit.cover,
                      errorBuilder: (context, error, stackTrace) => _placeholder())
                  : _placeholder(),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.name, style: Theme.of(context).textTheme.titleSmall),
                  if (item.description != null)
                    Text(item.description!,
                        style: Theme.of(context).textTheme.bodySmall,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 4),
                  Text(formatCurrency(item.price),
                      style:
                          Theme.of(context).textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                ],
              ),
            ),
            if (!item.isAvailable)
              const Padding(
                padding: EdgeInsets.only(left: 8),
                child: Text('Tạm hết', style: TextStyle(color: Colors.redAccent, fontSize: 12)),
              )
            else if (quantity == 0)
              IconButton.filled(onPressed: onAdd, icon: const Icon(Icons.add))
            else
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(onPressed: onRemove, icon: const Icon(Icons.remove_circle_outline)),
                  Text('$quantity', style: Theme.of(context).textTheme.titleMedium),
                  IconButton(onPressed: onAdd, icon: const Icon(Icons.add_circle_outline)),
                ],
              ),
          ],
        ),
      ),
    );
  }

  Widget _placeholder() => Container(
        width: 56,
        height: 56,
        color: const Color(0xFFEDE7DD),
        child: const Icon(Icons.fastfood_outlined, size: 20),
      );
}

class _CartSheet extends StatefulWidget {
  final Map<int, int> cart;
  final Map<int, FnbMenuItem> itemsById;
  final void Function(FnbMenuItem) onIncrement;
  final void Function(FnbMenuItem) onDecrement;

  const _CartSheet({
    required this.cart,
    required this.itemsById,
    required this.onIncrement,
    required this.onDecrement,
  });

  @override
  State<_CartSheet> createState() => _CartSheetState();
}

class _CartSheetState extends State<_CartSheet> {
  void _increment(FnbMenuItem item) {
    widget.onIncrement(item);
    setState(() {});
  }

  void _decrement(FnbMenuItem item) {
    widget.onDecrement(item);
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final entries = widget.cart.entries.where((e) => e.value > 0).toList();
    final total =
        entries.fold<double>(0, (sum, e) => sum + (widget.itemsById[e.key]?.price ?? 0) * e.value);
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: 16 + MediaQuery.of(context).viewInsets.bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Giỏ hàng', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            if (entries.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Text('Giỏ hàng trống', textAlign: TextAlign.center),
              )
            else
              ConstrainedBox(
                constraints: const BoxConstraints(maxHeight: 320),
                child: ListView.builder(
                  shrinkWrap: true,
                  itemCount: entries.length,
                  itemBuilder: (context, i) {
                    final item = widget.itemsById[entries[i].key]!;
                    final quantity = entries[i].value;
                    return Padding(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      child: Row(
                        children: [
                          Expanded(child: Text(item.name)),
                          IconButton(
                            onPressed: () => _decrement(item),
                            icon: const Icon(Icons.remove_circle_outline),
                          ),
                          Text('$quantity'),
                          IconButton(
                            onPressed: () => _increment(item),
                            icon: const Icon(Icons.add_circle_outline),
                          ),
                          SizedBox(
                            width: 80,
                            child: Text(formatCurrency(item.price * quantity), textAlign: TextAlign.right),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ),
            const Divider(),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Tổng cộng', style: Theme.of(context).textTheme.titleMedium),
                Text(formatCurrency(total),
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: entries.isEmpty ? null : () => Navigator.of(context).pop(true),
              child: const Text('Đặt hàng (thanh toán tiền mặt tại quầy)'),
            ),
          ],
        ),
      ),
    );
  }
}
