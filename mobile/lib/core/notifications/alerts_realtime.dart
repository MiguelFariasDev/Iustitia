import 'dart:async';

import 'package:supabase_flutter/supabase_flutter.dart';

import 'alerts_repository.dart';

/// Mesmo nome de evento que o backend usa (`RealtimeChannels`, Platform.Application).
const alertCreatedEvent = 'alert.created';

/// Canal privado por usuário: `tenant:{tenantId}:alerts:{userId}` (ADR-065).
String alertsChannelName(String tenantId, String userId) => 'tenant:$tenantId:alerts:$userId';

abstract interface class AlertsRealtime {
  /// Alertas novos do usuário, em tempo real. Falha de conexão nunca vira erro no stream: o
  /// Realtime é conveniência, e o contador de não lidos converge pelo polling.
  Stream<AlertPayload> watch({required String tenantId, required String userId});
}

/// Assina o canal PRIVADO no Supabase Realtime. A policy `alerts_realtime_receive`
/// (`infra/supabase/migrations/013_alerts.sql`) só autoriza o tópico montado com o `tenant_id`
/// e o `sub` do JWT — por isso o token é entregue ao Realtime (`setAuth`) ANTES de assinar.
class SupabaseAlertsRealtime implements AlertsRealtime {
  SupabaseAlertsRealtime(this._client);

  final SupabaseClient _client;

  @override
  Stream<AlertPayload> watch({required String tenantId, required String userId}) {
    late final StreamController<AlertPayload> controller;
    RealtimeChannel? channel;

    Future<void> start() async {
      try {
        final token = _client.auth.currentSession?.accessToken;
        if (token != null) await _client.realtime.setAuth(token);

        channel = _client
            .channel(alertsChannelName(tenantId, userId), opts: const RealtimeChannelConfig(private: true))
            .onBroadcast(
              event: alertCreatedEvent,
              callback: (message) {
                // Conforme a versão do cliente, o corpo vem direto ou embrulhado em `payload`.
                final body = message['payload'] is Map ? message['payload'] : message;
                final alert = AlertPayload.tryParse(body);
                if (alert != null && !controller.isClosed) controller.add(alert);
              },
            )..subscribe();
      } on Object {
        // Realtime é conveniência: sem canal, o polling do contador cobre.
      }
    }

    controller = StreamController<AlertPayload>(
      onListen: () => unawaited(start()),
      onCancel: () async {
        final current = channel;
        if (current != null) await _client.removeChannel(current);
      },
    );
    return controller.stream;
  }
}

/// Realtime de demonstração/testes: nada chega sozinho; [push] simula a chegada.
class MockAlertsRealtime implements AlertsRealtime {
  final _controller = StreamController<AlertPayload>.broadcast();

  void push(AlertPayload alert) => _controller.add(alert);

  @override
  Stream<AlertPayload> watch({required String tenantId, required String userId}) => _controller.stream;
}
