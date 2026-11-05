import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/paged.dart';

/// Estado de uma lista paginada que cresce conforme o usuário rola.
class PagedState<T> {
  const PagedState({
    required this.items,
    required this.page,
    required this.totalCount,
    required this.hasMore,
    this.isLoadingMore = false,
    this.loadMoreFailed = false,
  });

  final List<T> items;
  final int page;
  final int totalCount;
  final bool hasMore;
  final bool isLoadingMore;

  /// Falhou ao buscar a PRÓXIMA página: a lista já carregada continua na tela.
  final bool loadMoreFailed;

  PagedState<T> copyWith({
    List<T>? items,
    int? page,
    bool? hasMore,
    bool? isLoadingMore,
    bool? loadMoreFailed,
  }) =>
      PagedState(
        items: items ?? this.items,
        page: page ?? this.page,
        totalCount: totalCount,
        hasMore: hasMore ?? this.hasMore,
        isLoadingMore: isLoadingMore ?? this.isLoadingMore,
        loadMoreFailed: loadMoreFailed ?? this.loadMoreFailed,
      );
}

/// Base das listas paginadas. A subclasse implementa [fetch] e, em `build`, observa os filtros
/// (para a lista recarregar quando mudam) e chama [loadFirst].
abstract class PagedListNotifier<T> extends AsyncNotifier<PagedState<T>> {
  Future<Paged<T>> fetch(int page);

  Future<PagedState<T>> loadFirst() async {
    final first = await fetch(1);
    return PagedState(
      items: first.items,
      page: first.page,
      totalCount: first.totalCount,
      hasMore: first.hasMore,
    );
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;

    state = AsyncData(current.copyWith(isLoadingMore: true, loadMoreFailed: false));
    try {
      final next = await fetch(current.page + 1);
      state = AsyncData(current.copyWith(
        items: [...current.items, ...next.items],
        page: next.page,
        hasMore: next.hasMore,
        isLoadingMore: false,
      ));
    } on Object {
      state = AsyncData(current.copyWith(isLoadingMore: false, loadMoreFailed: true));
    }
  }
}
