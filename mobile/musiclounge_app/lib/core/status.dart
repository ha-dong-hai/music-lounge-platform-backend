import 'package:flutter/material.dart';

/// Colors and Vietnamese labels for the small set of status enums this app
/// renders (TicketStatus, FnbOrderStatus). Keep in sync with
/// docs/api/26-api-field-reference.md's enum quick-reference if new values ship.
class StatusColors {
  static Color forTicket(String status) {
    switch (status) {
      case 'Confirmed':
        return const Color(0xFF2E7D32);
      case 'Used':
        return const Color(0xFF616161);
      case 'Pending':
        return const Color(0xFFEF6C00);
      case 'Cancelled':
      case 'Refunded':
        return const Color(0xFFBA1A1A);
      default:
        return const Color(0xFF616161);
    }
  }

  static String labelForTicket(String status) {
    switch (status) {
      case 'Confirmed':
        return 'Đã xác nhận';
      case 'Used':
        return 'Đã sử dụng';
      case 'Pending':
        return 'Chờ thanh toán';
      case 'Cancelled':
        return 'Đã huỷ';
      case 'Refunded':
        return 'Đã hoàn tiền';
      default:
        return status;
    }
  }

  static Color forFnbOrder(String status) {
    switch (status) {
      case 'Pending':
        return const Color(0xFFEF6C00);
      case 'Preparing':
        return const Color(0xFF1565C0);
      case 'Served':
        return const Color(0xFF6A1B9A);
      case 'Paid':
        return const Color(0xFF2E7D32);
      case 'Cancelled':
        return const Color(0xFFBA1A1A);
      default:
        return const Color(0xFF616161);
    }
  }

  static String labelForFnbOrder(String status) {
    switch (status) {
      case 'Pending':
        return 'Chờ xử lý';
      case 'Preparing':
        return 'Đang chuẩn bị';
      case 'Served':
        return 'Đã phục vụ';
      case 'Paid':
        return 'Đã thanh toán';
      case 'Cancelled':
        return 'Đã huỷ';
      default:
        return status;
    }
  }
}
