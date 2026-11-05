import 'jwt_claims.dart';

/// Usuário autenticado, como a UI o enxerga.
class SessionUser {
  const SessionUser({
    required this.id,
    required this.email,
    this.tenantId,
    this.role,
    this.name,
    this.oab,
  });

  final String id;
  final String email;
  final String? tenantId;
  final String? role;
  final String? name;
  final String? oab;

  /// Nome para exibir: o da claim, ou a parte local do e-mail quando não há nome.
  String get displayName {
    final n = name?.trim();
    if (n != null && n.isNotEmpty) return n;
    return email.split('@').first;
  }

  factory SessionUser.fromClaims(AccessTokenClaims claims, {required String id, required String email}) =>
      SessionUser(
        id: id,
        email: email,
        tenantId: claims.tenantId,
        role: claims.role,
        name: claims.name,
        oab: claims.oab,
      );
}
