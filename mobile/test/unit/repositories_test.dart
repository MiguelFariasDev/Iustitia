import 'package:advocacia_mobile/core/api/api_client.dart';
import 'package:advocacia_mobile/core/notifications/alerts_repository.dart';
import 'package:advocacia_mobile/features/clientes/data/cliente_repository.dart';
import 'package:advocacia_mobile/features/clientes/domain/cliente.dart';
import 'package:advocacia_mobile/features/dashboard/data/dashboard_repository.dart';
import 'package:advocacia_mobile/features/processos/data/processo_repository.dart';
import 'package:advocacia_mobile/features/processos/domain/processo.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fakes.dart';

ApiClient api(FakeAdapter adapter) => ApiClient(
      baseUrl: 'https://api.exemplo.com/api/v1',
      readToken: () async => 't',
      refreshToken: () async => null,
      onUnauthorized: () {},
      dio: Dio()..httpClientAdapter = adapter,
    );

Map<String, Object?> processoJson({String status = 'Ativo', String area = 'Civel'}) => {
      'id': 'p1',
      'cnjNumber': '5001001-10.2026.8.26.0001',
      'clienteId': 'c1',
      'area': area,
      'status': status,
      'tribunal': 'TJSP',
      'parteContraria': null,
      'dataDistribuicao': '2026-03-12T00:00:00Z',
    };

Map<String, Object?> page(List<Object> items, {int p = 1, int total = 1}) => {
      'items': items,
      'page': p,
      'pageSize': 20,
      'totalCount': items.length,
      'totalPages': total,
    };

void main() {
  group('ApiProcessoRepository', () {
    test('lista com filtros de status e busca e interpreta a página', () async {
      final adapter = FakeAdapter((_) => jsonResponse(page([processoJson()], total: 3)));
      final repo = ApiProcessoRepository(api(adapter));

      final result = await repo.list(
        const ProcessoFilter(status: StatusProcesso.arquivado, busca: ' maria '),
        page: 2,
      );

      final query = adapter.requests.single.uri.queryParameters;
      expect(adapter.requests.single.uri.path, '/api/v1/processos');
      expect(query, {'page': '2', 'pageSize': '20', 'status': 'Arquivado', 'busca': 'maria'});
      expect(result.items.single.cnjNumber, '5001001-10.2026.8.26.0001');
      expect(result.items.single.dataDistribuicao, DateTime.utc(2026, 3, 12));
      expect(result.hasMore, isTrue); // página 1 de 3
    });

    test('valores desconhecidos não quebram: área vira Outros, status vira Ativo', () async {
      final adapter = FakeAdapter(
          (_) => jsonResponse(page([processoJson(status: 'EmAnaliseNova', area: 'Espacial')])));

      final item = (await ApiProcessoRepository(api(adapter)).list(const ProcessoFilter())).items.single;

      expect(item.area, AreaDireito.outros);
      expect(item.status, StatusProcesso.ativo);
    });

    test('detalhe usa o id na URL, escapado', () async {
      final adapter = FakeAdapter((_) => jsonResponse({
            ...processoJson(),
            'geraCaptura': true,
            'createdAt': '2026-03-10T00:00:00Z',
            'valorCausa': 35000.5,
          }));

      final p = await ApiProcessoRepository(api(adapter)).byId('a/b');

      expect(adapter.requests.single.uri.path, '/api/v1/processos/a%2Fb');
      expect(p.valorCausa, 35000.5);
      expect(p.geraCaptura, isTrue);
    });

    test('publicações do processo filtram por processId', () async {
      final adapter = FakeAdapter((_) => jsonResponse(page([
            {
              'id': 'pub1',
              'processId': 'p1',
              'cnjNumber': '5001001-10.2026.8.26.0001',
              'contentPreview': 'Intimação…',
              'publishedAt': '2026-11-01T10:00:00Z',
              'isRead': false,
            },
          ])));

      final result = await ApiProcessoRepository(api(adapter)).publicacoes('p1');

      expect(adapter.requests.single.uri.path, '/api/v1/publications');
      expect(adapter.requests.single.uri.queryParameters['processId'], 'p1');
      expect(result.items.single.preview, 'Intimação…');
      expect(result.items.single.isRead, isFalse);
    });
  });

  group('ApiClienteRepository', () {
    test('lista só clientes ativos e repassa a busca', () async {
      final adapter = FakeAdapter((_) => jsonResponse(page([
            {
              'id': 'c1',
              'tipo': 'PessoaJuridica',
              'nome': 'Padaria Boa Massa LTDA',
              'documento': '11.222.333/0001-81',
              'email': null,
              'telefone': '',
              'isActive': true,
            },
          ])));

      final result = await ApiClienteRepository(api(adapter)).list(const ClienteFilter(busca: 'padaria'));

      expect(adapter.requests.single.uri.queryParameters['apenasAtivos'], 'true');
      expect(adapter.requests.single.uri.queryParameters['busca'], 'padaria');
      final c = result.items.single;
      expect(c.tipo, TipoCliente.pessoaJuridica);
      expect(c.telefone, isNull); // string vazia vira ausente
    });

    test('tipo desconhecido cai em pessoa física', () {
      expect(TipoCliente.parse('Marciano'), TipoCliente.pessoaFisica);
    });

    test('detalhe monta o endereço numa linha só e ignora campos vazios', () async {
      final adapter = FakeAdapter((_) => jsonResponse({
            'id': 'c1',
            'tipo': 'PessoaFisica',
            'nome': 'Maria',
            'documento': '529.982.247-25',
            'logradouro': 'Rua das Flores',
            'numero': '120',
            'bairro': 'Centro',
            'cidade': 'São Paulo',
            'uf': 'SP',
            'cep': '01001-000',
            'complemento': '',
            'isActive': true,
          }));

      final c = await ApiClienteRepository(api(adapter)).byId('c1');

      expect(c.endereco, 'Rua das Flores, 120 · Centro · São Paulo - SP · 01001-000');
    });

    test('cliente sem endereço não inventa um', () async {
      final adapter = FakeAdapter((_) => jsonResponse({
            'id': 'c1',
            'tipo': 'PessoaFisica',
            'nome': 'Maria',
            'documento': 'x',
            'isActive': true,
          }));

      expect((await ApiClienteRepository(api(adapter)).byId('c1')).endereco, isNull);
    });
  });

  group('ApiDashboardRepository', () {
    test('compõe o painel com três consultas', () async {
      final adapter = FakeAdapter((o) {
        return switch (o.uri.path) {
          '/api/v1/publications/summary' =>
            jsonResponse({'total': 9, 'pendingReview': 4, 'unread': 3}),
          '/api/v1/publications' => jsonResponse(page([
              {
                'id': 'pub1',
                'cnjNumber': 'n',
                'contentPreview': 'x',
                'publishedAt': '2026-11-01T10:00:00Z',
                'isRead': false,
              },
            ])),
          '/api/v1/processos' => jsonResponse({...page([]), 'totalCount': 42}),
          _ => jsonResponse({}, status: 404),
        };
      });

      final data = await ApiDashboardRepository(api(adapter)).load();

      expect(adapter.requests, hasLength(3));
      expect(data.activeProcesses, 42);
      expect(data.unreadPublications, 3);
      expect(data.pendingReview, 4);
      expect(data.recentPublications, hasLength(1));
      final processos = adapter.requests.firstWhere((r) => r.uri.path.endsWith('/processos'));
      expect(processos.uri.queryParameters['status'], 'Ativo');
      expect(processos.uri.queryParameters['pageSize'], '1');
    });
  });

  group('alertas', () {
    test('payload do Realtime exige alertId e title', () {
      expect(AlertPayload.tryParse({'alertId': 'a1', 'title': 'Nova publicação'}), isNotNull);
      expect(AlertPayload.tryParse({'alertId': 'a1'}), isNull);
      expect(AlertPayload.tryParse({'title': 'x'}), isNull);
      expect(AlertPayload.tryParse('texto'), isNull);
      expect(AlertPayload.tryParse(null), isNull);
    });

    test('tipo ausente vira Other', () {
      expect(AlertPayload.tryParse({'alertId': 'a', 'title': 't'})!.type, 'Other');
    });

    test('alerta de publicação resolve o processo; sem processo, sem destino', () async {
      final withProcess = FakeAdapter((_) => jsonResponse({'id': 'pub1', 'processId': 'p9'}));
      final withoutProcess = FakeAdapter((_) => jsonResponse({'id': 'pub1', 'processId': null}));
      final missing = FakeAdapter((_) => jsonResponse({'detail': 'não existe'}, status: 404));
      const alert = AlertPayload(alertId: 'a1', type: 'Publication', title: 't', referenceId: 'pub1');

      expect(await ApiAlertsRepository(api(withProcess)).routeFor(alert), '/processos/p9');
      expect(await ApiAlertsRepository(api(withoutProcess)).routeFor(alert), isNull);
      // 404 ao resolver não é erro para o usuário: o banner aparece sem o botão "Ver".
      expect(await ApiAlertsRepository(api(missing)).routeFor(alert), isNull);
      expect(
        await ApiAlertsRepository(api(withProcess))
            .routeFor(const AlertPayload(alertId: 'a', type: 'Task', title: 't', referenceId: 'x')),
        isNull,
      );
    });

    test('contador de não lidos vem do resumo', () async {
      final adapter = FakeAdapter((_) => jsonResponse({'total': 10, 'unread': 4}));

      expect(await ApiAlertsRepository(api(adapter)).unreadCount(), 4);
    });
  });
}
