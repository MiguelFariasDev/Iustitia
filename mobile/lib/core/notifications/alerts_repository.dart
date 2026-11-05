import '../api/api_client.dart';
import '../errors/app_exception.dart';

/// Alerta recebido em tempo real (payload do `AlertCreatedConsumer`).
class AlertPayload {
  const AlertPayload({
    required this.alertId,
    required this.type,
    required this.title,
    this.description,
    this.referenceId,
  });

  final String alertId;
  final String type;
  final String title;
  final String? description;
  final String? referenceId;

  /// Valida o payload do canal: dado de rede nunca é confiável só por vir do nosso backend.
  /// Devolve `null` se faltar o que a UI precisa.
  static AlertPayload? tryParse(Object? value) {
    if (value is! Map) return null;

    String? str(String key) =>
        value[key] is String && (value[key] as String).isNotEmpty ? value[key] as String : null;

    final alertId = str('alertId');
    final title = str('title');
    if (alertId == null || title == null) return null;

    return AlertPayload(
      alertId: alertId,
      type: str('type') ?? 'Other',
      title: title,
      description: str('description'),
      referenceId: str('referenceId'),
    );
  }
}

abstract interface class AlertsRepository {
  /// Alertas não lidos do usuário (badge).
  Future<int> unreadCount();

  /// Para onde um alerta leva. Alerta de publicação aponta para a publicação; o app não tem
  /// tela de publicação, então resolve o PROCESSO dela. `null` = sem destino específico.
  Future<String?> routeFor(AlertPayload alert);
}

class ApiAlertsRepository implements AlertsRepository {
  ApiAlertsRepository(this._api);

  final ApiClient _api;

  @override
  Future<int> unreadCount() async {
    final json = await _api.getObject('/alerts/summary');
    return json['unread'] is int ? json['unread'] as int : 0;
  }

  @override
  Future<String?> routeFor(AlertPayload alert) async {
    if (alert.type != 'Publication' || alert.referenceId == null) return null;

    try {
      final publication = await _api.getObject(
        '/publications/${Uri.encodeComponent(alert.referenceId!)}',
      );
      final processId = publication['processId'];
      return processId is String ? '/processos/$processId' : null;
    } on ApiException {
      // Sem destino não é erro: o alerta continua visível e o usuário abre o painel.
      return null;
    }
  }
}
