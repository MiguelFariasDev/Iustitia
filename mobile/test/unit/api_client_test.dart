import 'package:advocacia_mobile/core/api/api_client.dart';
import 'package:advocacia_mobile/core/errors/app_exception.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

ApiClient clientWith(
  FakeAdapter adapter, {
  String? token = 'tok-1',
  Future<String?> Function()? refresh,
  void Function()? onUnauthorized,
}) =>
    ApiClient(
      baseUrl: 'https://api.exemplo.com/api/v1',
      readToken: () async => token,
      refreshToken: refresh ?? () async => null,
      onUnauthorized: onUnauthorized ?? () {},
      dio: Dio()..httpClientAdapter = adapter,
    );

void main() {
  group('requisições', () {
    test('injeta o Bearer e monta a URL pela base', () async {
      final adapter = FakeAdapter((_) => jsonResponse({'ok': true}));

      await clientWith(adapter).getObject('/processos');

      expect(adapter.requests.single.uri.toString(), 'https://api.exemplo.com/api/v1/processos');
      expect(adapter.requests.single.headers['Authorization'], 'Bearer tok-1');
    });

    test('sem token, não manda o cabeçalho', () async {
      final adapter = FakeAdapter((_) => jsonResponse({}));

      await clientWith(adapter, token: null).getObject('/x');

      expect(adapter.requests.single.headers.containsKey('Authorization'), isFalse);
    });

    test('parâmetros nulos ou vazios não vão para a URL', () async {
      final adapter = FakeAdapter((_) => jsonResponse({}));

      await clientWith(adapter).getObject('/processos', query: {
        'page': 2,
        'busca': '',
        'status': null,
        'apenasAtivos': true,
      });

      expect(adapter.requests.single.uri.queryParameters, {'page': '2', 'apenasAtivos': 'true'});
    });

    test('resposta que não é objeto vira erro, não lista vazia', () async {
      final adapter = FakeAdapter((_) => jsonResponse([1, 2]));

      expect(
        clientWith(adapter).getObject('/x'),
        throwsA(isA<ApiException>().having((e) => e.status, 'status', 500)),
      );
    });
  });

  group('erros (ProblemDetails -> ApiException)', () {
    test('usa detail, errorCode e correlationId do backend', () async {
      final adapter = FakeAdapter((_) => jsonResponse({
            'title': 'Not Found',
            'detail': 'Processo não encontrado.',
            'errorCode': 'PROCESSO_NOT_FOUND',
            'correlationId': 'abc-123',
          }, status: 404));

      await expectLater(
        clientWith(adapter).getObject('/processos/x'),
        throwsA(isA<ApiException>()
            .having((e) => e.status, 'status', 404)
            .having((e) => e.message, 'message', 'Processo não encontrado.')
            .having((e) => e.code, 'code', 'PROCESSO_NOT_FOUND')
            .having((e) => e.correlationId, 'correlationId', 'abc-123')
            .having((e) => e.isNotFound, 'isNotFound', isTrue)),
      );
    });

    test('mapeia os erros de validação por campo', () async {
      final adapter = FakeAdapter((_) => jsonResponse({
            'title': 'Validation',
            'errors': {
              'busca': ['Muito curta.'],
            },
          }, status: 400));

      await expectLater(
        clientWith(adapter).getObject('/x'),
        throwsA(isA<ApiException>().having((e) => e.fieldErrors, 'fieldErrors', {
          'busca': ['Muito curta.'],
        })),
      );
    });

    test('corpo sem ProblemDetails cai na mensagem padrão do status', () async {
      final adapter = FakeAdapter((_) => ResponseBody.fromString('boom', 500));

      await expectLater(
        clientWith(adapter).getObject('/x'),
        throwsA(isA<ApiException>().having((e) => e.message, 'message', contains('Erro inesperado'))),
      );
    });

    test('falha de conexão vira status 0 com mensagem de rede', () async {
      final adapter = FakeAdapter((o) => throw DioException.connectionError(
            requestOptions: o,
            reason: 'sem rede',
          ));

      await expectLater(
        clientWith(adapter).getObject('/x'),
        throwsA(isA<ApiException>()
            .having((e) => e.isNetworkError, 'isNetworkError', isTrue)
            .having((e) => e.message, 'message', contains('conectar'))),
      );
    });
  });

  group('401', () {
    test('renova o token UMA vez e refaz a requisição com o token novo', () async {
      var calls = 0;
      final adapter = FakeAdapter((o) {
        calls++;
        return o.headers['Authorization'] == 'Bearer novo'
            ? jsonResponse({'ok': true})
            : jsonResponse({}, status: 401);
      });
      var unauthorized = 0;

      final result = await clientWith(
        adapter,
        refresh: () async => 'novo',
        onUnauthorized: () => unauthorized++,
      ).getObject('/x');

      expect(result, {'ok': true});
      expect(calls, 2);
      expect(unauthorized, 0);
    });

    test('se a renovação falha, avisa que a sessão acabou', () async {
      final adapter = FakeAdapter((_) => jsonResponse({}, status: 401));
      var unauthorized = 0;

      await expectLater(
        clientWith(adapter, onUnauthorized: () => unauthorized++).getObject('/x'),
        throwsA(isA<ApiException>().having((e) => e.isUnauthorized, 'isUnauthorized', isTrue)),
      );
      expect(unauthorized, 1);
      expect(adapter.requests, hasLength(1));
    });

    test('não entra em loop: segundo 401 após renovar desiste', () async {
      final adapter = FakeAdapter((_) => jsonResponse({}, status: 401));
      var unauthorized = 0;

      await expectLater(
        clientWith(adapter, refresh: () async => 'novo', onUnauthorized: () => unauthorized++)
            .getObject('/x'),
        throwsA(isA<ApiException>()),
      );
      expect(adapter.requests, hasLength(2));
      expect(unauthorized, 1);
    });
  });
}
