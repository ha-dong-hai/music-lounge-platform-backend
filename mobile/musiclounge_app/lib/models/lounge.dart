/// LoungeListItemDto — enough of it for the venue picker on the Audience
/// F&B ordering flow.
class LoungeSummary {
  final int id;
  final String name;
  final String? primaryImageUrl;
  final String street;
  final String district;
  final String city;

  LoungeSummary({
    required this.id,
    required this.name,
    required this.primaryImageUrl,
    required this.street,
    required this.district,
    required this.city,
  });

  String get address => '$street, $district, $city';

  factory LoungeSummary.fromJson(Map<String, dynamic> json) => LoungeSummary(
        id: json['id'] as int,
        name: json['name'] as String,
        primaryImageUrl: json['primaryImageUrl'] as String?,
        street: json['street'] as String,
        district: json['district'] as String,
        city: json['city'] as String,
      );
}
