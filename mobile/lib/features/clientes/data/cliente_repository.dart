import '../../../core/api/api_client.dart';
import '../../../shared/models/paged.dart';
import '../domain/cliente.dart';

abstract interface class ClienteRepository {
  Future<Paged<ClienteListItem>> list(ClienteFilter filter, {int page = 1});
  Future<Cliente> byId(String id);
}

class ApiClienteRepository implements ClienteRepository {
  ApiClienteRepository(this._api);

  final ApiClient _api;

  static const pageSize = 20;

  @override
  Future<Paged<ClienteListItem>> list(ClienteFilter filter, {int page = 1}) async {
    final json = await _api.getObject('/clientes', query: {
      'page': page,
      'pageSize': pageSize,
      // Só ativos, como o padrão do web: cliente desativado some da lista do dia a dia.
      'apenasAtivos': true,
      'busca': filter.busca.trim(),
    });
    return Paged.fromJson(json, ClienteListItem.fromJson);
  }

  @override
  Future<Cliente> byId(String id) async =>
      Cliente.fromJson(await _api.getObject('/clientes/${Uri.encodeComponent(id)}'));
}
