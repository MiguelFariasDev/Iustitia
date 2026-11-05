import '../../processos/domain/publicacao.dart';

/// Tudo o que o painel mostra vem do banco do escritório. **Prazos e tarefas não aparecem**:
/// não existem no backend (Fases 3 e 4), e um cartão com número inventado é pior que cartão
/// nenhum — quem olha o painel toma decisão com base nele. (Mesma decisão do painel web.)
class DashboardData {
  const DashboardData({
    required this.activeProcesses,
    required this.unreadPublications,
    required this.pendingReview,
    required this.recentPublications,
  });

  final int activeProcesses;
  final int unreadPublications;
  final int pendingReview;
  final List<Publicacao> recentPublications;
}
