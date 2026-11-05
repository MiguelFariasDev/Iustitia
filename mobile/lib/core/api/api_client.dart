import 'package:dio/dio.dart';

import '../errors/app_exception.dart';

typedef AccessTokenReader = Future<String?> Function();
typedef AccessTokenRefresher = Future<String?> Function();

/// Cliente HTTP da API. Os repositórios só enxergam JSON e [ApiException]: o `Dio` e o
/// `DioException` não vazam para fora desta camada.
class ApiClient {
  ApiClient({
    required String baseUrl,
    required AccessTokenReader readToken,
    required AccessTokenRefresher refreshToken,
    required void Function() onUnauthorized,
    Dio? dio,
  }) : _dio = dio ?? Dio() {
    _dio.options
      ..baseUrl = baseUrl
      ..connectTimeout = const Duration(seconds: 15)
      ..receiveTimeout = const Duration(seconds: 30)
      ..headers['Accept'] = 'application/json';

    _dio.interceptors
      ..add(AuthInterceptor(
        dio: _dio,
        readToken: readToken,
        refreshToken: refreshToken,
        onUnauthorized: onUnauthorized,
      ))
      ..add(ProblemDetailsInterceptor());
  }

  final Dio _dio;

  Future<Map<String, dynamic>> getObject(String path, {Map<String, Object?>? query}) async {
    final data = await _send(() => _dio.get<Object?>(path, queryParameters: _clean(query)));
    return _asObject(data);
  }

  Future<Map<String, dynamic>> postObject(String path, {Object? body}) async {
    final data = await _send(() => _dio.post<Object?>(path, data: body));
    return data is Map<String, dynamic> ? data : const {};
  }

  Future<void> post(String path, {Object? body}) async {
    await _send(() => _dio.post<Object?>(path, data: body));
  }

  Future<Object?> _send(Future<Response<Object?>> Function() request) async {
    try {
      return (await request()).data;
    } on DioException catch (e) {
      final error = e.error;
      throw error is ApiException ? error : ApiException(status: e.response?.statusCode ?? 0);
    }
  }

  Map<String, dynamic> _asObject(Object? data) {
    if (data is Map<String, dynamic>) return data;
    // Resposta 2xx sem o objeto esperado: contrato quebrado, não "lista vazia".
    throw ApiException(status: 500, message: 'Resposta inesperada do servidor.');
  }

  /// Parâmetros nulos ou vazios não vão para a URL: `busca=` filtraria por texto vazio.
  Map<String, Object?>? _clean(Map<String, Object?>? query) {
    if (query == null) return null;
    return {
      for (final e in query.entries)
        if (e.value != null && '${e.value}'.isNotEmpty) e.key: e.value,
    };
  }
}

/// Injeta o Bearer do Supabase e, em 401, tenta UMA renovação antes de desistir.
///
/// O cliente do Supabase renova o token sozinho em segundo plano; este caminho cobre a
/// janela em que o token expirou entre a renovação e a requisição. Se a renovação também
/// falha, a sessão acabou de verdade e [onUnauthorized] manda o usuário para o login.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.dio,
    required this.readToken,
    required this.refreshToken,
    required this.onUnauthorized,
  });

  final Dio dio;
  final AccessTokenReader readToken;
  final AccessTokenRefresher refreshToken;
  final void Function() onUnauthorized;

  static const _retriedKey = 'auth_retried';

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    // No retry após renovar, o cabeçalho JÁ leva o token novo. Reler aqui poderia sobrescrevê-lo
    // com um valor velho (depende de quem implementa `readToken`): o retry não pode depender disso.
    if (options.extra[_retriedKey] != true) {
      final token = await readToken();
      if (token != null) options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final request = err.requestOptions;
    final retried = request.extra[_retriedKey] == true;

    if (err.response?.statusCode != 401 || retried) {
      if (err.response?.statusCode == 401) onUnauthorized();
      return handler.next(err);
    }

    final fresh = await refreshToken();
    if (fresh == null) {
      onUnauthorized();
      return handler.next(err);
    }

    request
      ..headers['Authorization'] = 'Bearer $fresh'
      ..extra[_retriedKey] = true;

    try {
      handler.resolve(await dio.fetch<Object?>(request));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }
}

/// Converte qualquer falha em [ApiException] (ProblemDetails -> exceção local).
class ProblemDetailsInterceptor extends Interceptor {
  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.error is ApiException) return handler.next(err);

    final status = err.response?.statusCode ?? 0;
    final isConnectionFailure = err.type == DioExceptionType.connectionTimeout ||
        err.type == DioExceptionType.receiveTimeout ||
        err.type == DioExceptionType.sendTimeout;

    final exception = status == 0
        ? ApiException(status: isConnectionFailure ? 408 : 0)
        : ApiException.fromProblem(status, err.response?.data);

    handler.next(DioException(
      requestOptions: err.requestOptions,
      response: err.response,
      type: err.type,
      error: exception,
    ));
  }
}
