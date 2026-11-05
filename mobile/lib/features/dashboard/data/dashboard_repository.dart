import '../../../core/api/api_client.dart';
import '../../processos/domain/publicacao.dart';
import '../domain/dashboard_data.dart';

abstract interface class DashboardRepository {
  Future<DashboardData> load();
}

/// O painel é composto NO CLIENTE a partir dos endpoints de cada módulo — não existe
/// `/dashboard/summary` no backend (criá-lo obrigaria o Platform a depender dos módulos de
/// negócio; ver ModuleBoundaryTests). As três consultas correm em paralelo.
class ApiDashboardRepository implements DashboardRepository {
  ApiDashboardRepository(this._api);

  final ApiClient _api;

  static const recentItems = 5;

  @override
  Future<DashboardData> load() async {
    final results = await Future.wait([
      _api.getObject('/publications/summary'),
      _api.getObject('/publications', query: {'page': 1, 'pageSize': recentItems, 'isRead': false}),
      // pageSize 1: só interessa o totalCount.
      _api.getObject('/processos', query: {'page': 1, 'pageSize': 1, 'status': 'Ativo'}),
    ]);

    final summary = results[0];
    final recent = results[1]['items'];
    int count(Object? v) => v is int ? v : 0;

    return DashboardData(
      activeProcesses: count(results[2]['totalCount']),
      unreadPublications: count(summary['unread']),
      pendingReview: count(summary['pendingReview']),
      recentPublications: [
        if (recent is List) ...recent.whereType<Map<String, dynamic>>().map(Publicacao.fromJson),
      ],
    );
  }
}
