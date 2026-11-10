import 'dart:convert';

import 'package:advocacia_mobile/core/auth/jwt_claims.dart';
import 'package:advocacia_mobile/core/auth/session_user.dart';
import 'package:flutter_test/flutter_test.dart';

String fakeJwt(Map<String, Object?> payload) {
  String enc(Object o) => base64Url.encode(utf8.encode(jsonEncode(o))).replaceAll('=', '');
  return '${enc({'alg': 'none'})}.${enc(payload)}.assinatura';
}

void main() {
  test('lê as claims emitidas pelo Custom Access Token Hook', () {
    final claims = AccessTokenClaims.fromToken(fakeJwt({
      'sub': 'u1',
      'email': 'a@b.com',
      'tenant_id': 't1',
      'user_role': 'Partner',
      'nome': 'João da Silva',
    }));

    expect(claims.sub, 'u1');
    expect(claims.tenantId, 't1');
    expect(claims.role, 'Partner');
    expect(claims.name, 'João da Silva');
  });

  test('aceita acentos no payload (UTF-8, não Latin-1)', () {
    expect(AccessTokenClaims.fromToken(fakeJwt({'nome': 'José Conceição'})).name, 'José Conceição');
  });

  test('token malformado devolve claims vazias, sem lançar', () {
    expect(AccessTokenClaims.fromToken('isto-nao-e-um-jwt').tenantId, isNull);
    expect(AccessTokenClaims.fromToken('a.@@@.c').tenantId, isNull);
    expect(AccessTokenClaims.fromToken(fakeJwt({'tenant_id': 123})).tenantId, isNull);
  });

  test('SessionUser usa o nome da claim, ou a parte local do e-mail', () {
    const withName = SessionUser(id: '1', email: 'ana@x.com', name: 'Ana Souza');
    const withoutName = SessionUser(id: '1', email: 'ana.souza@x.com');
    const blankName = SessionUser(id: '1', email: 'bruno@x.com', name: '   ');

    expect(withName.displayName, 'Ana Souza');
    expect(withoutName.displayName, 'ana.souza');
    expect(blankName.displayName, 'bruno');
  });
}
