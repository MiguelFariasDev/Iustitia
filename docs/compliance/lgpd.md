# LGPD — Lei Geral de Proteção de Dados

> Detalhamento completo em
> `docs/requisitos/12-seguranca-lgpd-oab-detalhado.md`. Este arquivo é o
> documento de compliance vivo do repositório — deve ser atualizado conforme
> decisões reais forem tomadas (DPO nomeado, DPIA concluído, etc.).

## Base legal do tratamento

- Execução de contrato (prestação do serviço de gestão jurídica).
- Legítimo interesse (para funcionalidades de produtividade/analytics
  internas ao escritório).

## Medidas técnicas

- [ ] Criptografia em repouso: `pgcrypto` para colunas sensíveis (ver
      `docs/requisitos/09-modelo-de-dados.md`).
- [ ] Criptografia em trânsito: TLS 1.3 obrigatório em todas as camadas.
- [ ] Mascaramento de dados sensíveis em logs (CPF, e-mail, telefone).
- [ ] Backups diários criptografados.

## Direitos do titular

- [ ] Endpoint de exclusão (direito ao esquecimento) — a implementar no
      módulo Identity/Legal quando os dados de cliente existirem (Fase 2).
- [ ] Endpoint/processo de exportação de dados pessoais sob demanda.

## Governança

- [ ] DPO (Data Protection Officer) designado — **TODO: preencher nome/contato**.
- [ ] DPIA (Data Protection Impact Assessment) documentado — **TODO: anexar**.
- [ ] Registro de consentimento sobre uso de IA nos dados do cliente final.

## IA e dados pessoais

- Nenhum dado de cliente é usado para treinar modelos de terceiros (ver
  `contrato-nao-treinamento-anthropic.md`).
- Toda sugestão de IA passa por revisão humana antes de qualquer efeito
  sobre o caso do cliente (ver ADR-012).
