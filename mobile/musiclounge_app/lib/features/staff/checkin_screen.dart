import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/logout.dart';
import '../../core/session.dart';
import '../../core/status.dart';
import '../../models/ticket.dart';

enum _Stage { scanning, loadingPreview, preview, confirming, result }

/// The entire Staff surface of this app: scan a ticket QR, preview it,
/// confirm the check-in. Per the app's scope, Staff gets nothing else here
/// (no walk-in sales, no F&B order board) — see docs/stitch/stitch-brief-staff-mobile.md
/// for the fuller Staff app design this intentionally narrows down from.
class StaffCheckInScreen extends StatefulWidget {
  const StaffCheckInScreen({super.key});

  @override
  State<StaffCheckInScreen> createState() => _StaffCheckInScreenState();
}

class _StaffCheckInScreenState extends State<StaffCheckInScreen> {
  final MobileScannerController _controller = MobileScannerController(autoStart: false);
  _Stage _stage = _Stage.scanning;
  String? _fullName;
  TicketDetail? _ticket;
  String? _errorMessage;
  bool _resultSuccess = false;
  String? _pendingQrCode;

  @override
  void initState() {
    super.initState();
    Session.instance.getFullName().then((v) {
      if (mounted) setState(() => _fullName = v);
    });
    _controller.start();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _onDetect(BarcodeCapture capture) async {
    if (_stage != _Stage.scanning) return;
    final code = capture.barcodes.isNotEmpty ? capture.barcodes.first.rawValue : null;
    if (code == null || code.isEmpty) return;
    _pendingQrCode = code;
    setState(() {
      _stage = _Stage.loadingPreview;
      _errorMessage = null;
    });
    await _controller.stop();
    try {
      final data = await ApiClient.instance.get('/tickets/by-qr/${Uri.encodeComponent(code)}');
      if (!mounted) return;
      setState(() {
        _ticket = TicketDetail.fromJson(data as Map<String, dynamic>);
        _stage = _Stage.preview;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.message;
        _ticket = null;
        _stage = _Stage.preview;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'Mất kết nối mạng. Vui lòng thử lại.';
        _ticket = null;
        _stage = _Stage.preview;
      });
    }
  }

  Future<void> _confirmCheckIn() async {
    final code = _pendingQrCode;
    if (code == null) return;
    setState(() => _stage = _Stage.confirming);
    try {
      final data = await ApiClient.instance.post('/tickets/check-in', body: {'qrCode': code});
      if (!mounted) return;
      final ticket = TicketDetail.fromJson(data as Map<String, dynamic>);
      setState(() {
        _ticket = ticket;
        _resultSuccess = true;
        _errorMessage = null;
        _stage = _Stage.result;
      });
      Future.delayed(const Duration(milliseconds: 1600), _resetToScanning);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _resultSuccess = false;
        _errorMessage = e.message;
        _stage = _Stage.result;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _resultSuccess = false;
        _errorMessage =
            'Mất kết nối mạng — chưa chắc chắn check-in đã thành công. Vui lòng quét lại để kiểm tra.';
        _stage = _Stage.result;
      });
    }
  }

  void _resetToScanning() {
    if (!mounted) return;
    setState(() {
      _stage = _Stage.scanning;
      _ticket = null;
      _errorMessage = null;
      _pendingQrCode = null;
    });
    _controller.start();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Check-In'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Đăng xuất',
            onPressed: logout,
          ),
        ],
      ),
      body: Column(
        children: [
          if (_fullName != null)
            Container(
              width: double.infinity,
              color: Theme.of(context).colorScheme.surfaceContainerHighest,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text('Nhân viên: $_fullName', style: Theme.of(context).textTheme.bodySmall),
            ),
          Expanded(child: _buildBody()),
        ],
      ),
    );
  }

  Widget _buildBody() {
    switch (_stage) {
      case _Stage.scanning:
        return _buildScanner();
      case _Stage.loadingPreview:
        return Stack(
          children: [
            _buildScanner(),
            Container(
              color: Colors.black54,
              child: const Center(child: CircularProgressIndicator(color: Colors.white)),
            ),
          ],
        );
      case _Stage.preview:
        return _buildPreview();
      case _Stage.confirming:
        return const Center(child: CircularProgressIndicator());
      case _Stage.result:
        return _buildResult();
    }
  }

  Widget _buildScanner() {
    return Stack(
      fit: StackFit.expand,
      children: [
        MobileScanner(controller: _controller, onDetect: _onDetect),
        Center(
          child: Container(
            width: 260,
            height: 260,
            decoration: BoxDecoration(
              border: Border.all(color: Colors.white, width: 3),
              borderRadius: BorderRadius.circular(16),
            ),
          ),
        ),
        Positioned(
          bottom: 32,
          left: 0,
          right: 0,
          child: Text(
            'Đưa mã QR của vé vào khung hình',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              shadows: [Shadow(color: Colors.black.withValues(alpha: 0.6), blurRadius: 6)],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildPreview() {
    final colors = Theme.of(context).colorScheme;
    if (_errorMessage != null) {
      return _buildStatusScreen(
        icon: Icons.error_outline,
        color: colors.error,
        title: 'Không quét được vé',
        message: _errorMessage!,
        primaryLabel: 'Quét vé khác',
        onPrimary: _resetToScanning,
      );
    }
    final ticket = _ticket!;
    if (ticket.status == 'Used') {
      return _buildStatusScreen(
        icon: Icons.block,
        color: const Color(0xFFEF6C00),
        title: 'Vé đã được sử dụng',
        message: ticket.checkedInAt != null
            ? 'Đã check-in lúc ${formatDateTime(ticket.checkedInAt!)}'
            : 'Vé này đã được check-in trước đó.',
        primaryLabel: 'Quét vé khác',
        onPrimary: _resetToScanning,
        ticket: ticket,
      );
    }
    if (ticket.accessType == 'Livestream') {
      return _buildStatusScreen(
        icon: Icons.wifi,
        color: const Color(0xFF1565C0),
        title: 'Vé xem trực tuyến',
        message: 'Vé online không cần check-in tại cửa.',
        primaryLabel: 'Quét vé khác',
        onPrimary: _resetToScanning,
        ticket: ticket,
      );
    }
    if (ticket.status != 'Confirmed') {
      return _buildStatusScreen(
        icon: Icons.error_outline,
        color: colors.error,
        title: 'Vé không hợp lệ',
        message: 'Trạng thái vé: ${StatusColors.labelForTicket(ticket.status)}',
        primaryLabel: 'Quét vé khác',
        onPrimary: _resetToScanning,
        ticket: ticket,
      );
    }
    return _buildStatusScreen(
      icon: Icons.check_circle_outline,
      color: const Color(0xFF2E7D32),
      title: 'Vé hợp lệ',
      message: 'Xác nhận check-in cho khách này?',
      primaryLabel: 'Xác nhận Check-In',
      onPrimary: _confirmCheckIn,
      secondaryLabel: 'Huỷ',
      onSecondary: _resetToScanning,
      ticket: ticket,
    );
  }

  Widget _buildResult() {
    final colors = Theme.of(context).colorScheme;
    if (_resultSuccess && _ticket != null) {
      return _buildStatusScreen(
        icon: Icons.check_circle,
        color: const Color(0xFF2E7D32),
        title: 'Check-In thành công',
        message: 'Đang chuyển sang khách tiếp theo...',
        ticket: _ticket,
      );
    }
    return _buildStatusScreen(
      icon: Icons.error_outline,
      color: colors.error,
      title: 'Check-In thất bại',
      message: _errorMessage ?? 'Đã có lỗi xảy ra',
      primaryLabel: 'Quét vé khác',
      onPrimary: _resetToScanning,
    );
  }

  Widget _buildStatusScreen({
    required IconData icon,
    required Color color,
    required String title,
    required String message,
    TicketDetail? ticket,
    String? primaryLabel,
    VoidCallback? onPrimary,
    String? secondaryLabel,
    VoidCallback? onSecondary,
  }) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        children: [
          const SizedBox(height: 16),
          Icon(icon, size: 88, color: color),
          const SizedBox(height: 16),
          Text(
            title,
            style: Theme.of(context)
                .textTheme
                .headlineSmall
                ?.copyWith(color: color, fontWeight: FontWeight.bold),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 8),
          Text(message, style: Theme.of(context).textTheme.bodyLarge, textAlign: TextAlign.center),
          if (ticket != null) ...[
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
                    const Divider(height: 24),
                    _infoRow('Hạng vé', '${ticket.tierName} · ${ticket.priceName}'),
                    _infoRow('Suất diễn', formatDateTime(ticket.showScheduledStart)),
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: 32),
          if (primaryLabel != null) FilledButton(onPressed: onPrimary, child: Text(primaryLabel)),
          if (secondaryLabel != null) ...[
            const SizedBox(height: 8),
            OutlinedButton(onPressed: onSecondary, child: Text(secondaryLabel)),
          ],
        ],
      ),
    );
  }

  Widget _infoRow(String label, String value) {
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
