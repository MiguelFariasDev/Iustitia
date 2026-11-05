/// Valida uma rota vinda de FORA do app (payload de push, de alerta, de deep link) antes de
/// navegar. É o equivalente do `safeRedirectPath` do web: dado de rede não é confiável só por
/// vir do nosso backend, e uma rota arbitrária poderia abrir uma tela que o usuário não deveria
/// alcançar por ali.
///
/// Aceita só caminhos internos conhecidos; qualquer outra coisa devolve `null`.
String? safeInternalRoute(String? raw) {
  if (raw == null) return null;
  final route = raw.trim();

  if (!route.startsWith('/') || route.startsWith('//') || route.contains(r'\')) return null;

  final uri = Uri.tryParse(route);
  if (uri == null || uri.hasScheme || uri.hasAuthority) return null;

  final segments = uri.pathSegments;
  if (segments.isEmpty) return null;

  const withDetail = {'processos', 'clientes'};
  const leaf = {'dashboard', 'agenda', 'perfil'};
  final first = segments.first;

  final valid = switch (segments.length) {
    1 => leaf.contains(first) || withDetail.contains(first),
    2 => withDetail.contains(first) && _isId(segments[1]),
    _ => false,
  };

  return valid ? uri.path : null;
}

/// Ids do backend são GUIDs; aceita letras, dígitos e hífen, sem barras nem pontos.
bool _isId(String value) => RegExp(r'^[A-Za-z0-9-]{1,64}$').hasMatch(value);
