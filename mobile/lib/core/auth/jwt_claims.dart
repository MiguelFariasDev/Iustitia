import 'dart:convert';

/// Claims do access token, lidas **só para exibição** (papel, escritório, nome).
///
/// NUNCA usar para autorização: quem valida a assinatura e decide permissão é o backend
/// (JWKS). `tenant_id` e `user_role` vêm do Custom Access Token Hook (ADR-029).
class AccessTokenClaims {
  const AccessTokenClaims({this.sub, this.email, this.tenantId, this.role, this.name, this.oab});

  final String? sub;
  final String? email;
  final String? tenantId;
  final String? role;
  final String? name;
  final String? oab;

  static const empty = AccessTokenClaims();

  factory AccessTokenClaims.fromToken(String token) {
    final parts = token.split('.');
    if (parts.length < 2) return empty;

    try {
      final json = utf8.decode(base64Url.decode(base64Url.normalize(parts[1])));
      final map = jsonDecode(json);
      if (map is! Map<String, dynamic>) return empty;

      String? str(String key) => map[key] is String ? map[key] as String : null;
      return AccessTokenClaims(
        sub: str('sub'),
        email: str('email'),
        tenantId: str('tenant_id'),
        role: str('user_role'),
        name: str('nome'),
        oab: str('oab'),
      );
    } on FormatException {
      return empty;
    }
  }
}
