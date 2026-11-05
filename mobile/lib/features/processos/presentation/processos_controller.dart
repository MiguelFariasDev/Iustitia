import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../shared/models/paged.dart';
import '../../../shared/widgets/paged_state.dart';
import '../../clientes/domain/cliente.dart';
import '../domain/processo.dart';
import '../domain/publicacao.dart';

class ProcessoFilterController extends Notifier<ProcessoFilter> {
  @override
  ProcessoFilter build() => const ProcessoFilter();

  void setBusca(String busca) => state = state.copyWith(busca: busca);

  /// Tocar no filtro ativo o desliga ("todos").
  void toggleStatus(StatusProcesso status) => state = state.status == status
      ? state.copyWith(clearStatus: true)
      : state.copyWith(status: status);
}

final processoFilterProvider =
    NotifierProvider<ProcessoFilterController, ProcessoFilter>(ProcessoFilterController.new);

class ProcessosController extends PagedListNotifier<ProcessoListItem> {
  late ProcessoFilter _filter;

  @override
  Future<PagedState<ProcessoListItem>> build() {
    _filter = ref.watch(processoFilterProvider);
    return loadFirst();
  }

  @override
  Future<Paged<ProcessoListItem>> fetch(int page) =>
      ref.read(processoRepositoryProvider).list(_filter, page: page);
}

final processosProvider = AsyncNotifierProvider.autoDispose<ProcessosController, PagedState<ProcessoListItem>>(
  ProcessosController.new,
);

/// Processos de um cliente (aba do detalhe do cliente). Primeira página basta: um cliente com
/// mais de 20 processos é raro, e a lista completa é tarefa do web.
final processosDoClienteProvider =
    FutureProvider.autoDispose.family<Paged<ProcessoListItem>, String>((ref, clienteId) {
  return ref.watch(processoRepositoryProvider).list(ProcessoFilter(clienteId: clienteId));
});

final processoProvider = FutureProvider.autoDispose.family<Processo, String>(
  (ref, id) => ref.watch(processoRepositoryProvider).byId(id),
);

final publicacoesDoProcessoProvider =
    FutureProvider.autoDispose.family<Paged<Publicacao>, String>(
  (ref, processId) => ref.watch(processoRepositoryProvider).publicacoes(processId),
);

/// Nome do cliente por id. A listagem de processos devolve só o id; resolver aqui evita um
/// cadastro de cliente por linha na API e o cache do Riverpod garante UMA consulta por cliente
/// enquanto a lista estiver aberta.
final clienteNomeProvider = FutureProvider.autoDispose.family<String, String>((ref, id) async {
  final Cliente cliente = await ref.watch(clienteRepositoryProvider).byId(id);
  return cliente.nome;
});
