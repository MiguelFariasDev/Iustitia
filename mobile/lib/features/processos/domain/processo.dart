enum AreaDireito {
  civel('Civel', 'Cível'),
  trabalhista('Trabalhista', 'Trabalhista'),
  criminal('Criminal', 'Criminal'),
  tributario('Tributario', 'Tributário'),
  previdenciario('Previdenciario', 'Previdenciário'),
  familia('Familia', 'Família'),
  consumidor('Consumidor', 'Consumidor'),
  empresarial('Empresarial', 'Empresarial'),
  administrativo('Administrativo', 'Administrativo'),
  ambiental('Ambiental', 'Ambiental'),
  outros('Outros', 'Outros');

  const AreaDireito(this.apiValue, this.label);
  final String apiValue;
  final String label;

  /// Valor desconhecido vira "Outros": uma área nova no backend não pode quebrar a tela.
  static AreaDireito parse(Object? value) =>
      values.firstWhere((a) => a.apiValue == value, orElse: () => outros);
}

enum StatusProcesso {
  ativo('Ativo', 'Ativo'),
  suspenso('Suspenso', 'Suspenso'),
  encerrado('Encerrado', 'Encerrado'),
  arquivado('Arquivado', 'Arquivado');

  const StatusProcesso(this.apiValue, this.label);
  final String apiValue;
  final String label;

  /// Status desconhecido cai em "Ativo", não em "Arquivado": na dúvida, o processo continua
  /// à vista. Mostrá-lo como arquivado esconderia um caso que pode ter prazo correndo.
  static StatusProcesso parse(Object? value) =>
      values.firstWhere((s) => s.apiValue == value, orElse: () => ativo);
}

String? _str(Object? v) => v is String && v.isNotEmpty ? v : null;

DateTime? _date(Object? v) => v is String ? DateTime.tryParse(v) : null;

/// Item da listagem (`ProcessoListItem` da API). Sem nome do cliente: a API devolve só o id.
class ProcessoListItem {
  const ProcessoListItem({
    required this.id,
    required this.cnjNumber,
    required this.clienteId,
    required this.area,
    required this.status,
    this.tribunal,
    this.parteContraria,
    this.dataDistribuicao,
  });

  final String id;

  /// Já mascarado pela API: NNNNNNN-DD.AAAA.J.TR.OOOO.
  final String cnjNumber;
  final String clienteId;
  final AreaDireito area;
  final StatusProcesso status;
  final String? tribunal;
  final String? parteContraria;
  final DateTime? dataDistribuicao;

  factory ProcessoListItem.fromJson(Map<String, dynamic> json) => ProcessoListItem(
        id: json['id'] as String,
        cnjNumber: json['cnjNumber'] as String,
        clienteId: json['clienteId'] as String,
        area: AreaDireito.parse(json['area']),
        status: StatusProcesso.parse(json['status']),
        tribunal: _str(json['tribunal']),
        parteContraria: _str(json['parteContraria']),
        dataDistribuicao: _date(json['dataDistribuicao']),
      );
}

/// Detalhe (`ProcessoResponse` da API).
class Processo {
  const Processo({
    required this.id,
    required this.cnjNumber,
    required this.clienteId,
    required this.area,
    required this.status,
    required this.geraCaptura,
    required this.createdAt,
    this.tribunal,
    this.comarca,
    this.vara,
    this.assunto,
    this.parteContraria,
    this.valorCausa,
    this.dataDistribuicao,
    this.observacoes,
  });

  final String id;
  final String cnjNumber;
  final String clienteId;
  final AreaDireito area;
  final StatusProcesso status;

  /// Se o processo está sendo consultado no diário (ativo e suspenso geram captura).
  final bool geraCaptura;
  final DateTime createdAt;
  final String? tribunal;
  final String? comarca;
  final String? vara;
  final String? assunto;
  final String? parteContraria;
  final num? valorCausa;
  final DateTime? dataDistribuicao;
  final String? observacoes;

  factory Processo.fromJson(Map<String, dynamic> json) => Processo(
        id: json['id'] as String,
        cnjNumber: json['cnjNumber'] as String,
        clienteId: json['clienteId'] as String,
        area: AreaDireito.parse(json['area']),
        status: StatusProcesso.parse(json['status']),
        geraCaptura: json['geraCaptura'] == true,
        createdAt: _date(json['createdAt']) ?? DateTime.fromMillisecondsSinceEpoch(0),
        tribunal: _str(json['tribunal']),
        comarca: _str(json['comarca']),
        vara: _str(json['vara']),
        assunto: _str(json['assunto']),
        parteContraria: _str(json['parteContraria']),
        valorCausa: json['valorCausa'] is num ? json['valorCausa'] as num : null,
        dataDistribuicao: _date(json['dataDistribuicao']),
        observacoes: _str(json['observacoes']),
      );
}

class ProcessoFilter {
  const ProcessoFilter({this.status, this.busca = '', this.clienteId});

  final StatusProcesso? status;
  final String busca;
  final String? clienteId;

  ProcessoFilter copyWith({StatusProcesso? status, bool clearStatus = false, String? busca}) =>
      ProcessoFilter(
        status: clearStatus ? null : (status ?? this.status),
        busca: busca ?? this.busca,
        clienteId: clienteId,
      );

  @override
  bool operator ==(Object other) =>
      other is ProcessoFilter &&
      other.status == status &&
      other.busca == busca &&
      other.clienteId == clienteId;

  @override
  int get hashCode => Object.hash(status, busca, clienteId);
}
