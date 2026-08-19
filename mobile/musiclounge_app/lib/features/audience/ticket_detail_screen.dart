import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/status.dart';
import '../../models/ticket.dart';

class TicketDetailScreen extends StatefulWidget {
  final String ticketId;
  const TicketDetailScreen({super.key, required this.ticketId});

  @override
  State<TicketDetailScreen> createState() => _TicketDetailScreenState();
}

class _TicketDetailScreenState extends State<TicketDetailScreen> {
  late Future<TicketDetail> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<TicketDetail> _load() async {
    final data = await ApiClient.instance.get('/tickets/${widget.ticketId}');
    return TicketDetail.fromJson(data as Map<String, dynamic>);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết vé')),
      body: FutureBuilder<TicketDetail>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Không tải được vé: ${snapshot.error}'));
          }
          final ticket = snapshot.data!;
          final color = StatusColors.forTicket(ticket.status);
          return SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    StatusColors.labelForTicket(ticket.status),
                    style: TextStyle(color: color, fontWeight: FontWeight.w600),
                  ),
                ),
                const SizedBox(height: 20),
                if (ticket.status == 'Confirmed' &&
                    ticket.accessType == 'Physical' &&
                    ticket.qrCode != null)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: QrImageView(data: ticket.qrCode!, size: 220),
                    ),
                  )
                else if (ticket.accessType == 'Livestream')
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 24),
                    child: Text('Vé xem trực tuyến — không cần quét mã tại cửa.',
                        textAlign: TextAlign.center),
                  )
                else if (ticket.qrCode == null)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 24),
                    child: Text('Vé chưa được xác nhận thanh toán, chưa có mã QR.',
                        textAlign: TextAlign.center),
                  )
                else
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 24),
                    child: Text(
                      ticket.status == 'Used'
                          ? 'Vé này đã được sử dụng, không cần quét lại.'
                          : 'Vé này không còn hiệu lực để vào cửa.',
                      textAlign: TextAlign.center,
                    ),
                  ),
                const SizedBox(height: 24),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(ticket.showName, style: Theme.of(context).textTheme.titleLarge),
                        const SizedBox(height: 4),
                        Text(ticket.loungeName, style: Theme.of(context).textTheme.bodyMedium),
                        Text(ticket.loungeAddress, style: Theme.of(context).textTheme.bodySmall),
                        const Divider(height: 24),
                        _row(context, 'Suất diễn', formatDateTime(ticket.showScheduledStart)),
                        _row(context, 'Hạng vé', '${ticket.tierName} · ${ticket.priceName}'),
                        _row(context, 'Giá vé', formatCurrency(ticket.pricePaid)),
                        _row(context, 'Ngày mua', formatDateTime(ticket.purchasedAt)),
                        if (ticket.checkedInAt != null)
                          _row(context, 'Check-in lúc', formatDateTime(ticket.checkedInAt!)),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _row(BuildContext context, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.black54)),
          Flexible(child: Text(value, textAlign: TextAlign.right)),
        ],
      ),
    );
  }
}
