import '../../features/clientes/data/cliente_repository.dart';
import '../../features/clientes/domain/cliente.dart';
import '../../features/dashboard/data/dashboard_repository.dart';
import '../../features/dashboard/domain/dashboard_data.dart';
import '../../features/processos/data/processo_repository.dart';
import '../../features/processos/domain/processo.dart';
import '../../features/processos/domain/publicacao.dart';
import '../../shared/models/paged.dart';
import '../errors/app_exception.dart';
import '../notifications/alerts_repository.dart';

/// Repositórios EM MEMÓRIA, para demonstração (`API_MOCKS=true`) e para os testes. Reimplementam
/// as regras de filtro da API (busca por CNJ, cliente e parte contrária; filtro de status).
/// Os dados são fictícios.
abstract final class MockData {
  static final clientes = <Cliente>[
    const Cliente(
      id: 'cli-001',
      tipo: TipoCliente.pessoaFisica,
      nome: 'Maria Aparecida Silva',
      documento: '529.982.247-25',
      email: 'maria.silva@exemplo.com.br',
      telefone: '(11) 98765-4321',
      isActive: true,
      endereco: 'Rua das Flores, 120 · Centro · São Paulo - SP · 01001-000',
      observacoes: 'Prefere contato por WhatsApp.',
    ),
    const Cliente(
      id: 'cli-002',
      tipo: TipoCliente.pessoaJuridica,
      nome: 'Padaria Boa Massa LTDA',
      documento: '11.222.333/0001-81',
      email: 'contato@boamassa.com.br',
      telefone: '(11) 3456-7890',
      isActive: true,
    ),
    const Cliente(
      id: 'cli-003',
      tipo: TipoCliente.pessoaFisica,
      nome: 'José Lima Ferreira',
      documento: '111.444.777-35',
      telefone: '(21) 99876-1234',
      isActive: true,
    ),
    const Cliente(
      id: 'cli-004',
      tipo: TipoCliente.pessoaJuridica,
      nome: 'Construtora Horizonte S.A.',
      documento: '45.723.174/0001-10',
      email: 'juridico@horizonte.com.br',
      isActive: true,
    ),
  ];

  static final processos = <Processo>[
    Processo(
      id: 'proc-001',
      cnjNumber: '5001001-10.2026.8.26.0001',
      clienteId: 'cli-001',
      area: AreaDireito.civel,
      status: StatusProcesso.ativo,
      geraCaptura: true,
      createdAt: DateTime(2026, 3, 10),
      tribunal: 'TJSP',
      comarca: 'São Paulo',
      vara: '3ª Vara Cível',
      assunto: 'Ação de cobrança',
      parteContraria: 'Carlos Eduardo Pereira',
      valorCausa: 35000,
      dataDistribuicao: DateTime(2026, 3, 12),
    ),
    Processo(
      id: 'proc-002',
      cnjNumber: '5002002-20.2026.5.02.0001',
      clienteId: 'cli-003',
      area: AreaDireito.trabalhista,
      status: StatusProcesso.ativo,
      geraCaptura: true,
      createdAt: DateTime(2026, 4, 2),
      tribunal: 'TRT2',
      comarca: 'São Paulo',
      assunto: 'Reclamação trabalhista',
      parteContraria: 'Indústria Alfa LTDA',
      dataDistribuicao: DateTime(2026, 4, 5),
    ),
    Processo(
      id: 'proc-003',
      cnjNumber: '5003003-30.2025.8.26.0100',
      clienteId: 'cli-002',
      area: AreaDireito.empresarial,
      status: StatusProcesso.suspenso,
      geraCaptura: true,
      createdAt: DateTime(2025, 11, 20),
      tribunal: 'TJSP',
      parteContraria: 'Fornecedor Beta S.A.',
    ),
    Processo(
      id: 'proc-004',
      cnjNumber: '5004004-40.2024.8.26.0200',
      clienteId: 'cli-004',
      area: AreaDireito.consumidor,
      status: StatusProcesso.arquivado,
      geraCaptura: false,
      createdAt: DateTime(2024, 6, 1),
      tribunal: 'TJSP',
      valorCausa: 8000,
    ),
  ];

  static final publicacoes = <Publicacao>[
    Publicacao(
      id: 'pub-001',
      processId: 'proc-001',
      clienteNome: 'Maria Aparecida Silva',
      tribunal: 'TJSP',
      cnjNumber: '5001001-10.2026.8.26.0001',
      source: 'DJEN',
      preview: 'Intimação para apresentar réplica à contestação no prazo de 15 dias.',
      publishedAt: DateTime.now().subtract(const Duration(hours: 3)),
      type: 'Intimação',
      isRead: false,
    ),
    Publicacao(
      id: 'pub-002',
      processId: 'proc-002',
      clienteNome: 'José Lima Ferreira',
      tribunal: 'TRT2',
      cnjNumber: '5002002-20.2026.5.02.0001',
      source: 'DJEN',
      preview: 'Designada audiência de instrução. Intimem-se as partes.',
      publishedAt: DateTime.now().subtract(const Duration(days: 1)),
      type: 'Despacho',
      isRead: false,
    ),
  ];
}

Paged<T> _page<T>(List<T> all, int page, int size) {
  final start = (page - 1) * size;
  final slice = start >= all.length ? <T>[] : all.skip(start).take(size).toList();
  return Paged(
    items: slice,
    page: page,
    pageSize: size,
    totalCount: all.length,
    totalPages: (all.length / size).ceil(),
  );
}

bool _matches(String? text, String query) => text != null && text.toLowerCase().contains(query);

ApiException _notFound() => ApiException(status: 404, message: 'Recurso não encontrado.');

class MockProcessoRepository implements ProcessoRepository {
  MockProcessoRepository({this.pageSize = 20});

  final int pageSize;

  @override
  Future<Paged<ProcessoListItem>> list(ProcessoFilter filter, {int page = 1}) async {
    final query = filter.busca.trim().toLowerCase();
    final items = MockData.processos.where((p) {
      if (filter.status != null && p.status != filter.status) return false;
      if (filter.clienteId != null && p.clienteId != filter.clienteId) return false;
      if (query.isEmpty) return true;

      final cliente = MockData.clientes.where((c) => c.id == p.clienteId).firstOrNull;
      return _matches(p.cnjNumber, query) ||
          _matches(p.parteContraria, query) ||
          _matches(cliente?.nome, query);
    }).map((p) => ProcessoListItem(
          id: p.id,
          cnjNumber: p.cnjNumber,
          clienteId: p.clienteId,
          area: p.area,
          status: p.status,
          tribunal: p.tribunal,
          parteContraria: p.parteContraria,
          dataDistribuicao: p.dataDistribuicao,
        )).toList();

    return _page(items, page, pageSize);
  }

  @override
  Future<Processo> byId(String id) async =>
      MockData.processos.where((p) => p.id == id).firstOrNull ?? (throw _notFound());

  @override
  Future<Paged<Publicacao>> publicacoes(String processId, {int page = 1}) async =>
      _page(MockData.publicacoes.where((p) => p.processId == processId).toList(), page, pageSize);
}

class MockClienteRepository implements ClienteRepository {
  MockClienteRepository({this.pageSize = 20});

  final int pageSize;

  @override
  Future<Paged<ClienteListItem>> list(ClienteFilter filter, {int page = 1}) async {
    final query = filter.busca.trim().toLowerCase();
    final digits = query.replaceAll(RegExp(r'\D'), '');
    final items = MockData.clientes.where((c) {
      if (query.isEmpty) return true;
      return _matches(c.nome, query) ||
          (digits.isNotEmpty && c.documento.replaceAll(RegExp(r'\D'), '').contains(digits));
    }).map((c) => ClienteListItem(
          id: c.id,
          tipo: c.tipo,
          nome: c.nome,
          documento: c.documento,
          isActive: c.isActive,
          email: c.email,
          telefone: c.telefone,
        )).toList();

    return _page(items, page, pageSize);
  }

  @override
  Future<Cliente> byId(String id) async =>
      MockData.clientes.where((c) => c.id == id).firstOrNull ?? (throw _notFound());
}

class MockDashboardRepository implements DashboardRepository {
  @override
  Future<DashboardData> load() async => DashboardData(
        activeProcesses: MockData.processos.where((p) => p.status == StatusProcesso.ativo).length,
        unreadPublications: MockData.publicacoes.where((p) => !p.isRead).length,
        pendingReview: MockData.publicacoes.length,
        recentPublications: MockData.publicacoes,
      );
}

class MockAlertsRepository implements AlertsRepository {
  MockAlertsRepository({this.unread = 2});

  final int unread;

  @override
  Future<int> unreadCount() async => unread;

  @override
  Future<String?> routeFor(AlertPayload alert) async =>
      alert.type == 'Publication' ? '/processos/proc-001' : null;
}
