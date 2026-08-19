class AuthResult {
  final String token;
  final String userId;
  final String email;
  final String fullName;
  final String role;
  final int? loungeId;

  AuthResult({
    required this.token,
    required this.userId,
    required this.email,
    required this.fullName,
    required this.role,
    this.loungeId,
  });

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
        token: json['token'] as String,
        userId: json['userId'].toString(),
        email: json['email'] as String,
        fullName: json['fullName'] as String,
        role: json['role'] as String,
        loungeId: json['loungeId'] as int?,
      );
}
