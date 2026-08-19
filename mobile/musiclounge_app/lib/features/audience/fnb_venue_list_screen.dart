import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../models/lounge.dart';
import 'fnb_menu_screen.dart';

class FnbVenueListScreen extends StatefulWidget {
  const FnbVenueListScreen({super.key});

  @override
  State<FnbVenueListScreen> createState() => _FnbVenueListScreenState();
}

class _FnbVenueListScreenState extends State<FnbVenueListScreen> {
  late Future<List<LoungeSummary>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<LoungeSummary>> _load() async {
    final data = await ApiClient.instance.get('/lounges', query: {'page': 1, 'pageSize': 50});
    final items = (data as Map<String, dynamic>)['items'] as List<dynamic>;
    return items.map((e) => LoungeSummary.fromJson(e as Map<String, dynamic>)).toList();
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
    return RefreshIndicator(
      onRefresh: _refresh,
      child: FutureBuilder<List<LoungeSummary>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return ListView(children: [
              const SizedBox(height: 120),
              Center(child: Text('Không tải được danh sách phòng trà: ${snapshot.error}')),
            ]);
          }
          final lounges = snapshot.data!;
          if (lounges.isEmpty) {
            return ListView(children: const [
              SizedBox(height: 120),
              Center(child: Text('Chưa có phòng trà nào.')),
            ]);
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: lounges.length,
            separatorBuilder: (context, index) => const SizedBox(height: 12),
            itemBuilder: (context, i) {
              final lounge = lounges[i];
              final imageUrl = ApiClient.resolveImageUrl(lounge.primaryImageUrl);
              return Card(
                child: InkWell(
                  borderRadius: BorderRadius.circular(16),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => FnbMenuScreen(loungeId: lounge.id, loungeName: lounge.name),
                    ),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Row(
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(12),
                          child: imageUrl != null
                              ? Image.network(
                                  imageUrl,
                                  width: 64,
                                  height: 64,
                                  fit: BoxFit.cover,
                                  errorBuilder: (context, error, stackTrace) => _placeholderIcon(),
                                )
                              : _placeholderIcon(),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(lounge.name, style: Theme.of(context).textTheme.titleMedium),
                              const SizedBox(height: 4),
                              Text(lounge.address, style: Theme.of(context).textTheme.bodySmall),
                            ],
                          ),
                        ),
                        const Icon(Icons.chevron_right),
                      ],
                    ),
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }

  Widget _placeholderIcon() {
    return Container(
      width: 64,
      height: 64,
      color: const Color(0xFFEDE7DD),
      child: const Icon(Icons.storefront_outlined),
    );
  }
}
