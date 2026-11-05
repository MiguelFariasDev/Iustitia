enum TipoCliente {
  pessoaFisica('PessoaFisica', 'Pessoa física'),
  pessoaJuridica('PessoaJuridica', 'Pessoa jurídica');

  const TipoCliente(this.apiValue, this.label);
  final String apiValue;
  final String label;

  /// Tipo desconhecido cai em pessoa física: é o caso majoritário e, ao contrário de um papel
  /// de acesso, não concede nada — errar aqui muda um rótulo, não uma permissão.
  static TipoCliente parse(Object? value) =>
      value == 'PessoaJuridica' ? pessoaJuridica : pessoaFisica;
}

String? _str(Object? v) => v is String && v.isNotEmpty ? v : null;

class ClienteListItem {
  const ClienteListItem({
    required this.id,
    required this.tipo,
    required this.nome,
    required this.documento,
    required this.isActive,
    this.email,
    this.telefone,
  });

  final String id;
  final TipoCliente tipo;
  final String nome;

  /// Já mascarado pela API (CPF/CNPJ).
  final String documento;
  final String? email;
  final String? telefone;
  final bool isActive;

  factory ClienteListItem.fromJson(Map<String, dynamic> json) => ClienteListItem(
        id: json['id'] as String,
        tipo: TipoCliente.parse(json['tipo']),
        nome: json['nome'] as String,
        documento: json['documento'] as String? ?? '',
        email: _str(json['email']),
        telefone: _str(json['telefone']),
        isActive: json['isActive'] != false,
      );
}

class Cliente {
  const Cliente({
    required this.id,
    required this.tipo,
    required this.nome,
    required this.documento,
    required this.isActive,
    this.email,
    this.telefone,
    this.dataNascimento,
    this.observacoes,
    this.endereco,
  });

  final String id;
  final TipoCliente tipo;
  final String nome;
  final String documento;
  final String? email;
  final String? telefone;
  final DateTime? dataNascimento;
  final String? observacoes;
  final bool isActive;

  /// Endereço numa linha só, ou `null` se não há nada preenchido.
  final String? endereco;

  factory Cliente.fromJson(Map<String, dynamic> json) {
    final street = [
      _str(json['logradouro']),
      _str(json['numero']),
    ].whereType<String>().join(', ');
    final city = [_str(json['cidade']), _str(json['uf'])].whereType<String>().join(' - ');
    final parts = [
      street,
      _str(json['complemento']),
      _str(json['bairro']),
      city,
      _str(json['cep']),
    ].whereType<String>().where((p) => p.isNotEmpty);

    return Cliente(
      id: json['id'] as String,
      tipo: TipoCliente.parse(json['tipo']),
      nome: json['nome'] as String,
      documento: json['documento'] as String? ?? '',
      email: _str(json['email']),
      telefone: _str(json['telefone']),
      dataNascimento: json['dataNascimento'] is String
          ? DateTime.tryParse(json['dataNascimento'] as String)
          : null,
      observacoes: _str(json['observacoes']),
      isActive: json['isActive'] != false,
      endereco: parts.isEmpty ? null : parts.join(' · '),
    );
  }
}

class ClienteFilter {
  const ClienteFilter({this.busca = ''});

  final String busca;

  @override
  bool operator ==(Object other) => other is ClienteFilter && other.busca == busca;

  @override
  int get hashCode => busca.hashCode;
}
