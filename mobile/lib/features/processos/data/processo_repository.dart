import '../../../core/api/api_client.dart';
import '../../../shared/models/paged.dart';
import '../domain/processo.dart';
import '../domain/publicacao.dart';

abstract interface class ProcessoRepository {
  Future<Paged<ProcessoListItem>> list(ProcessoFilter filter, {int page = 1});
  Future<Processo> byId(String id);
  Future<Paged<Publicacao>> publicacoes(String processId, {int page = 1});
}

class ApiProcessoRepository implements ProcessoRepository {
  ApiProcessoRepository(this._api);

  final ApiClient _api;

  static const pageSize = 20;

  @override
  Future<Paged<ProcessoListItem>> list(ProcessoFilter filter, {int page = 1}) async {
    final json = await _api.getObject('/processos', query: {
      'page': page,
      'pageSize': pageSize,
      'status': filter.status?.apiValue,
      'busca': filter.busca.trim(),
      'clienteId': filter.clienteId,
    });
    return Paged.fromJson(json, ProcessoListItem.fromJson);
  }

  @override
  Future<Processo> byId(String id) async =>
      Processo.fromJson(await _api.getObject('/processos/${Uri.encodeComponent(id)}'));

  @override
  Future<Paged<Publicacao>> publicacoes(String processId, {int page = 1}) async {
    final json = await _api.getObject('/publications', query: {
      'page': page,
      'pageSize': pageSize,
      'processId': processId,
    });
    return Paged.fromJson(json, Publicacao.fromJson);
  }
}
