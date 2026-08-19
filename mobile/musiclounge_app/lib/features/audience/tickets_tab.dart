import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/status.dart';
import '../../models/ticket.dart';
import 'ticket_detail_screen.dart';

class TicketsTab extends StatefulWidget {
  const TicketsTab({super.key});

  @override
  State<TicketsTab> createState() => _TicketsTabState();
}

class _TicketsTabState extends State<TicketsTab> {
  late Future<List<TicketSummary>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<TicketSummary>> _load() async {
    final data =
        await ApiClient.instance.get('/tickets/my', query: {'page': 1, 'pageSize': 50});
    final items = (data as Map<String, dynamic>)['items'] as List<dynamic>;
    return items.map((e) => TicketSummary.fromJson(e as Map<String, dynamic>)).toList();
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
      child: FutureBuilder<List<TicketSummary>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return ListView(children: [
              const SizedBox(height: 120),
              Center(child: Text('Không tải được vé: ${snapshot.error}', textAlign: TextAlign.center)),
            ]);
          }
          final tickets = snapshot.data!;
          if (tickets.isEmpty) {
            return ListView(
              children: const [
                SizedBox(height: 120),
                Center(child: Text('Bạn chưa có vé nào.')),
              ],
            );
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: tickets.length,
            separatorBuilder: (context, index) => const SizedBox(height: 12),
            itemBuilder: (context, i) => _TicketCard(ticket: tickets[i]),
          );
        },
      ),
    );
  }
}

class _TicketCard extends StatelessWidget {
  final TicketSummary ticket;
  const _TicketCard({required this.ticket});

  @override
  Widget build(BuildContext context) {
    final color = StatusColors.forTicket(ticket.status);
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => TicketDetailScreen(ticketId: ticket.id)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(ticket.showName, style: Theme.of(context).textTheme.titleMedium),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      StatusColors.labelForTicket(ticket.status),
                      style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text('${ticket.loungeName} · ${ticket.loungeCity}',
                  style: Theme.of(context).textTheme.bodySmall),
              const SizedBox(height: 4),
              Text(formatDateTime(ticket.showScheduledStart),
                  style: Theme.of(context).textTheme.bodySmall),
              const SizedBox(height: 4),
              Text('${ticket.tierName} · ${ticket.priceName}',
                  style: Theme.of(context).textTheme.bodyMedium),
            ],
          ),
        ),
      ),
    );
  }
}
