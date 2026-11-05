/// Publicação capturada do diário oficial (`ListPublicationsResponse` da API).
///
/// A listagem traz só um TRECHO ([preview]); o inteiro teor é dado sob sigilo e nem chega ao
/// celular nesta etapa.
class Publicacao {
  const Publicacao({
    required this.id,
    required this.cnjNumber,
    required this.publishedAt,
    required this.isRead,
    required this.preview,
    this.processId,
    this.clienteNome,
    this.tribunal,
    this.source,
    this.type,
  });

  final String id;
  final String? processId;
  final String? clienteNome;
  final String? tribunal;
  final String cnjNumber;
  final String? source;
  final String preview;
  final DateTime publishedAt;
  final String? type;
  final bool isRead;

  factory Publicacao.fromJson(Map<String, dynamic> json) {
    String? str(String key) => json[key] is String && (json[key] as String).isNotEmpty
        ? json[key] as String
        : null;

    return Publicacao(
      id: json['id'] as String,
      processId: str('processId'),
      clienteNome: str('clienteNome'),
      tribunal: str('tribunal'),
      cnjNumber: json['cnjNumber'] as String? ?? '',
      source: str('source'),
      preview: json['contentPreview'] as String? ?? '',
      publishedAt: DateTime.tryParse(json['publishedAt'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      type: str('type'),
      isRead: json['isRead'] == true,
    );
  }
}
