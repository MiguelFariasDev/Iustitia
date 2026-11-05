/// Página de resultados (`PagedList` da API).
class Paged<T> {
  const Paged({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  const Paged.empty()
      : items = const [],
        page = 1,
        pageSize = 20,
        totalCount = 0,
        totalPages = 0;

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  bool get hasMore => page < totalPages;

  factory Paged.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) parse) {
    final raw = json['items'];
    return Paged(
      items: [
        if (raw is List) ...raw.whereType<Map<String, dynamic>>().map(parse),
      ],
      page: _int(json['page'], 1),
      pageSize: _int(json['pageSize'], 20),
      totalCount: _int(json['totalCount'], 0),
      totalPages: _int(json['totalPages'], 0),
    );
  }

  static int _int(Object? value, int fallback) => value is int ? value : fallback;
}
