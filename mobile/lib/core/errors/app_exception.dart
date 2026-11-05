/// Erro normalizado de toda chamada à API: a UI nunca lida com `DioException`.
/// Espelha o `ApiError` do frontend web.
class ApiException implements Exception {
  ApiException({
    required this.status,
    String? message,
    String? code,
    this.correlationId,
    this.fieldErrors = const {},
  })  : code = code ?? 'HTTP_$status',
        message = message ?? _fallback(status);

  /// 0 = sem resposta (rede/timeout).
  final int status;
  final String message;
  final String code;
  final String? correlationId;
  final Map<String, List<String>> fieldErrors;

  bool get isNetworkError => status == 0;
  bool get isUnauthorized => status == 401;
  bool get isNotFound => status == 404;

  /// Constrói a partir de um corpo `ProblemDetails` (RFC 7807) — título, detalhe e o
  /// `errorCode` do catálogo de erros do backend.
  factory ApiException.fromProblem(int status, Object? body) {
    if (body is! Map) return ApiException(status: status);

    final detail = body['detail'];
    final title = body['title'];
    final errors = body['errors'];

    return ApiException(
      status: status,
      message: detail is String && detail.isNotEmpty ? detail : null,
      code: body['errorCode'] is String
          ? body['errorCode'] as String
          : (title is String ? title : null),
      correlationId: body['correlationId'] is String ? body['correlationId'] as String : null,
      fieldErrors: errors is Map
          ? {
              for (final entry in errors.entries)
                '${entry.key}': [
                  if (entry.value is List) ...(entry.value as List).map((e) => '$e'),
                ],
            }
          : const {},
    );
  }

  static String _fallback(int status) => switch (status) {
        0 => 'Não foi possível conectar ao servidor. Verifique sua conexão.',
        400 => 'Os dados enviados são inválidos.',
        401 => 'Sua sessão expirou. Entre novamente.',
        403 => 'Você não tem permissão para esta ação.',
        404 => 'Recurso não encontrado.',
        408 => 'O servidor demorou para responder. Tente novamente.',
        409 => 'Conflito: o recurso foi alterado por outra pessoa.',
        429 => 'Muitas requisições. Aguarde um instante.',
        _ => 'Erro inesperado no servidor. Tente novamente em instantes.',
      };

  @override
  String toString() => 'ApiException($status, $code)';
}

/// Mensagem amigável para qualquer erro mostrado na tela.
String errorMessage(Object error) {
  if (error is ApiException) return error.message;
  return 'Algo deu errado. Tente novamente.';
}
