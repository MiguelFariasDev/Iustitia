# Conformidade OAB

> Detalhamento completo em
> `docs/requisitos/12-seguranca-lgpd-oab-detalhado.md` e princípios em
> `docs/requisitos/01-visao-geral-e-principios.md`.

## Provimento CFOAB nº 271/2025 — Revisão Humana Obrigatória

Nenhuma sugestão gerada por IA (classificação de publicação, prazo sugerido,
resposta da Athena) produz efeito sobre um caso real sem confirmação
explícita de um advogado. Implementado como:

- Tela de revisão humana obrigatória (`/revisao/:id` no web,
  `ReviewPublicationView` no mobile) antes de qualquer tarefa/ação ser
  criada a partir de uma sugestão de IA.
- `publication_reviews` registra quem revisou, quando e a ação tomada
  (aceitar/corrigir/rejeitar) — ver `docs/requisitos/09-modelo-de-dados.md`.
- Athena sempre pede confirmação humana antes de executar qualquer ação
  (criar tarefa, etc.) — nunca age de forma autônoma.

## Recomendação OAB nº 001/2024 — Transparência com Cliente Final

O cliente final do escritório deve ser informado, de alguma forma, que IA
foi usada no atendimento do seu caso. **TODO (Fase 3+):** definir o
mecanismo concreto de comunicação (cláusula contratual, aviso na
plataforma do cliente, etc.) junto com jurídico do próprio produto.

## Código de Ética — Responsabilidade Final do Advogado

O sistema é uma ferramenta de apoio à decisão. A responsabilidade pelo caso,
prazo e estratégia processual permanece integralmente do advogado
responsável — nunca do sistema ou da IA.

## Sigilo Profissional

Nenhum dado de processo, cliente ou publicação é usado para treinar modelos
de terceiros. Ver `contrato-nao-treinamento-anthropic.md`.

## Checklist de conformidade por feature de IA

Antes de qualquer feature que envolva IA entrar em produção:

- [ ] Existe tela de revisão humana antes de qualquer efeito no caso?
- [ ] A ação é auditada (`audit_logs`)?
- [ ] O prompt/versão do modelo usado está logado?
- [ ] O cliente final é informado do uso de IA neste fluxo?
