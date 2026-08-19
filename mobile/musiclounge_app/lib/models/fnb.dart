class FnbMenu {
  final int id;
  final int loungeId;
  final String name;
  final String? description;
  final bool isActive;
  final int displayOrder;

  FnbMenu({
    required this.id,
    required this.loungeId,
    required this.name,
    required this.description,
    required this.isActive,
    required this.displayOrder,
  });

  factory FnbMenu.fromJson(Map<String, dynamic> json) => FnbMenu(
        id: json['id'] as int,
        loungeId: json['loungeId'] as int,
        name: json['name'] as String,
        description: json['description'] as String?,
        isActive: json['isActive'] as bool,
        displayOrder: json['displayOrder'] as int,
      );
}

class FnbMenuItem {
  final int id;
  final int menuId;
  final String category;
  final String name;
  final String? description;
  final double price;
  final String? imageUrl;
  final bool isAvailable;
  final int displayOrder;

  FnbMenuItem({
    required this.id,
    required this.menuId,
    required this.category,
    required this.name,
    required this.description,
    required this.price,
    required this.imageUrl,
    required this.isAvailable,
    required this.displayOrder,
  });

  factory FnbMenuItem.fromJson(Map<String, dynamic> json) => FnbMenuItem(
        id: json['id'] as int,
        menuId: json['menuId'] as int,
        category: json['category'] as String,
        name: json['name'] as String,
        description: json['description'] as String?,
        price: (json['price'] as num).toDouble(),
        imageUrl: json['imageUrl'] as String?,
        isAvailable: json['isAvailable'] as bool,
        displayOrder: json['displayOrder'] as int,
      );
}

class FnbOrderItem {
  final int id;
  final int menuItemId;
  final String menuItemName;
  final int quantity;
  final double unitPrice;
  final bool cancelled;
  final String? note;

  FnbOrderItem({
    required this.id,
    required this.menuItemId,
    required this.menuItemName,
    required this.quantity,
    required this.unitPrice,
    required this.cancelled,
    required this.note,
  });

  factory FnbOrderItem.fromJson(Map<String, dynamic> json) => FnbOrderItem(
        id: json['id'] as int,
        menuItemId: json['menuItemId'] as int,
        menuItemName: json['menuItemName'] as String,
        quantity: json['quantity'] as int,
        unitPrice: (json['unitPrice'] as num).toDouble(),
        cancelled: json['cancelled'] as bool,
        note: json['note'] as String?,
      );
}

/// FnbOrderDto — GET /fnb-orders/{id} and /fnb-orders/my items.
class FnbOrder {
  final int id;
  final int loungeId;
  final int? showId;
  final String? tableNote;
  final String status;
  final double totalAmount;
  final String? note;
  final DateTime createdAt;
  final List<FnbOrderItem> items;

  static const statusSequence = ['Pending', 'Preparing', 'Served', 'Paid'];

  FnbOrder({
    required this.id,
    required this.loungeId,
    required this.showId,
    required this.tableNote,
    required this.status,
    required this.totalAmount,
    required this.note,
    required this.createdAt,
    required this.items,
  });

  factory FnbOrder.fromJson(Map<String, dynamic> json) => FnbOrder(
        id: json['id'] as int,
        loungeId: json['loungeId'] as int,
        showId: json['showId'] as int?,
        tableNote: json['tableNote'] as String?,
        status: json['status'] as String,
        totalAmount: (json['totalAmount'] as num).toDouble(),
        note: json['note'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
        items: (json['items'] as List<dynamic>? ?? [])
            .map((e) => FnbOrderItem.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
