import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../shared/models/paged.dart';
import '../../../shared/widgets/paged_state.dart';
import '../domain/cliente.dart';

class ClienteFilterController extends Notifier<ClienteFilter> {
  @override
  ClienteFilter build() => const ClienteFilter();

  void setBusca(String busca) => state = ClienteFilter(busca: busca);
}

final clienteFilterProvider =
    NotifierProvider<ClienteFilterController, ClienteFilter>(ClienteFilterController.new);

class ClientesController extends PagedListNotifier<ClienteListItem> {
  late ClienteFilter _filter;

  @override
  Future<PagedState<ClienteListItem>> build() {
    _filter = ref.watch(clienteFilterProvider);
    return loadFirst();
  }

  @override
  Future<Paged<ClienteListItem>> fetch(int page) =>
      ref.read(clienteRepositoryProvider).list(_filter, page: page);
}

final clientesProvider =
    AsyncNotifierProvider.autoDispose<ClientesController, PagedState<ClienteListItem>>(
  ClientesController.new,
);

final clienteProvider = FutureProvider.autoDispose.family<Cliente, String>(
  (ref, id) => ref.watch(clienteRepositoryProvider).byId(id),
);
