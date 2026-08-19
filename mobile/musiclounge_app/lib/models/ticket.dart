/// One row from GET /tickets/my.
class TicketSummary {
  final String id;
  final int showId;
  final String showName;
  final String loungeName;
  final String loungeCity;
  final DateTime showScheduledStart;
  final String tierName;
  final String priceName;
  final double pricePaid;
  final String accessType;
  final String status;
  final String? qrCode;
  final DateTime purchasedAt;
  final bool hasPendingTransfer;

  TicketSummary({
    required this.id,
    required this.showId,
    required this.showName,
    required this.loungeName,
    required this.loungeCity,
    required this.showScheduledStart,
    required this.tierName,
    required this.priceName,
    required this.pricePaid,
    required this.accessType,
    required this.status,
    required this.qrCode,
    required this.purchasedAt,
    required this.hasPendingTransfer,
  });

  factory TicketSummary.fromJson(Map<String, dynamic> json) => TicketSummary(
        id: json['id'] as String,
        showId: json['showId'] as int,
        showName: json['showName'] as String,
        loungeName: json['loungeName'] as String,
        loungeCity: json['loungeCity'] as String,
        showScheduledStart: DateTime.parse(json['showScheduledStart'] as String),
        tierName: json['tierName'] as String,
        priceName: json['priceName'] as String,
        pricePaid: (json['pricePaid'] as num).toDouble(),
        accessType: json['accessType'] as String,
        status: json['status'] as String,
        qrCode: json['qrCode'] as String?,
        purchasedAt: DateTime.parse(json['purchasedAt'] as String),
        hasPendingTransfer: json['hasPendingTransfer'] as bool? ?? false,
      );
}

/// TicketDetailDto — returned by GET /tickets/{id}, GET /tickets/by-qr/{qrCode}
/// and POST /tickets/check-in (all share the same shape).
class TicketDetail {
  final String id;
  final String showName;
  final String loungeName;
  final String loungeAddress;
  final DateTime showScheduledStart;
  final String tierName;
  final String priceName;
  final double pricePaid;
  final String accessType;
  final String status;
  final String? qrCode;
  final DateTime purchasedAt;
  final DateTime? checkedInAt;

  TicketDetail({
    required this.id,
    required this.showName,
    required this.loungeName,
    required this.loungeAddress,
    required this.showScheduledStart,
    required this.tierName,
    required this.priceName,
    required this.pricePaid,
    required this.accessType,
    required this.status,
    required this.qrCode,
    required this.purchasedAt,
    required this.checkedInAt,
  });

  factory TicketDetail.fromJson(Map<String, dynamic> json) {
    final physicalDetail = json['physicalDetail'] as Map<String, dynamic>?;
    return TicketDetail(
      id: json['id'] as String,
      showName: json['showName'] as String,
      loungeName: json['loungeName'] as String,
      loungeAddress: json['loungeAddress'] as String,
      showScheduledStart: DateTime.parse(json['showScheduledStart'] as String),
      tierName: json['tierName'] as String,
      priceName: json['priceName'] as String,
      pricePaid: (json['pricePaid'] as num).toDouble(),
      accessType: json['accessType'] as String,
      status: json['status'] as String,
      qrCode: json['qrCode'] as String?,
      purchasedAt: DateTime.parse(json['purchasedAt'] as String),
      checkedInAt: physicalDetail?['checkedInAt'] != null
          ? DateTime.parse(physicalDetail!['checkedInAt'] as String)
          : null,
    );
  }
}
