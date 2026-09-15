# Contrato de Não-Treinamento — Anthropic

> Ver ADR-013 e ADR-014 (`docs/adr/013-*.md`, `docs/adr/014-*.md`) para o
> racional da escolha do provedor e desta exigência contratual.

## Exigência

Nenhum dado de processo, cliente, publicação ou conversa com a Athena pode
ser usado pela Anthropic (ou por qualquer subprocessador) para treinar
modelos de IA de terceiros. Isso é um requisito de sigilo profissional
(Código de Ética da OAB) e de confiança do produto — não é opcional.

## Status

- [ ] Confirmar por escrito com a Anthropic a política de não-treinamento
      sobre dados enviados via API (política padrão da Anthropic para uso
      via API é não treinar com esses dados — **validar e anexar
      documentação oficial aqui antes de ir a produção**).
- [ ] Definir se o acesso será via API direta da Anthropic ou via AWS
      Bedrock/Google Vertex AI (ver
      `docs/requisitos/15-decisoes-pendentes.md`, item 1) — a política de
      retenção/treinamento pode variar conforme o canal.
- [ ] Documentar a versão do contrato/termos vigente e a data de aceite.

## O que a aplicação garante do seu lado

- `IClassificationService`/`IAthenaService` como abstrações — nenhum
  código de negócio chama a API da Anthropic diretamente, o que permite
  auditar e trocar de provedor se a política de dados mudar.
- Log de toda chamada à IA (input, output, tokens, custo) para auditoria —
  ver `audit_logs` e `publication_suggestions` em
  `docs/requisitos/09-modelo-de-dados.md`.
- Nenhum dado é enviado à Anthropic sem que o contrato de não-treinamento
  esteja confirmado (ver checklist de conformidade em `oab.md`).
