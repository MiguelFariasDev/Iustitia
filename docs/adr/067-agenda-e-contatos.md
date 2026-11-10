# ADR-067: Agenda (Workflow) e Contatos (CRM)

**Status:** Aceito — Fase 4, etapas 4.1 e 4.2

## Contexto

A Fase 4 acrescenta a agenda do escritório e o cadastro de pessoas que não são clientes
(parte contrária, testemunha, fornecedor). Os módulos `Workflow` e `CRM` existiam só como
pastas vazias, e o repositório não tem a entidade `Document` nem as tarefas da Fase 3 que o
roteiro da Fase 4 assume.

## Decisão

### Onde cada coisa mora

- **`CalendarEvent` em Workflow.** Agenda é fluxo de trabalho do escritório (prazo, audiência,
  reunião, tarefa) e vai ser consumida pelas tarefas/kanban quando existirem.
- **`Contact` em CRM.** Contato é relacionamento. Fica **separado** de `Cliente` (Legal) de
  propósito: quem o escritório representa tem processo, documento validado e histórico
  financeiro; uma testemunha, não. Misturar os dois obrigaria todo cliente a carregar
  campos opcionais que só fazem sentido para contatos.

### Referência a processo sem acoplar módulos

`CalendarEvent.ProcessId` é um `Guid` solto. Para validar que o processo existe no escritório
sem Workflow referenciar Legal, a Application define a porta `IProcessLookup` e a
Infrastructure a implementa com um `SELECT EXISTS` em `processos`. É acoplamento de esquema,
não de código, e foi aceito por ser pequeno e coberto pelo RLS. Se Legal ganhar uma API
interna de consulta entre módulos, a implementação troca sem tocar nos handlers.

### Lembretes

Job recorrente do Hangfire (`CalendarReminderJob`, a cada 5 minutos) publica alertas in-app
via `IAlertPublisher` 1 dia e 1 hora antes. O evento guarda quando cada aviso saiu
(`OneDayReminderSentAt`, `OneHourReminderSentAt`), o que torna o job idempotente; remarcar o
horário zera os dois. Como o alerta é idempotente por (usuário, tipo, referência), cada
antecedência usa uma referência derivada de forma determinística — com o id do evento puro, o
segundo lembrete seria descartado como duplicata do primeiro.

O alerta leva só título e horário. Descrição, local e dados do processo não saem do banco, e
o log registra contagens e ids, nunca títulos.

### Sem eventos de domínio nesta etapa

Nenhum consumidor existe, e evento sem consumidor só custaria o mapeamento da outbox em cada
DbContext. Quando houver (ex.: tarefas reagindo a prazo), o mapeamento segue o padrão de
`OutboxMessageMappingConfiguration` do Legal.

### Exclusão

Contato nunca é excluído (só desativado) — o histórico de quem foi parte ou testemunha precisa
permanecer. Evento da agenda é excluído logicamente (`ISoftDeletable`).

## Consequências

- As migrations do EF Core de `CrmDbContext` e `WorkflowDbContext` **precisam ser geradas**
  com `dotnet ef migrations add Initial --project <Modulo>.Infrastructure --startup-project src/Hosts/Api`
  antes de aplicar `infra/supabase/migrations/014_calendar_contacts.sql` (RLS).
- A agenda limita a consulta a 62 dias por requisição, o suficiente para a visão mensal com
  as semanas vizinhas.

## Complementos de frontend (Fase 4)

- **Prazos a partir de publicações.** "Agendar prazo" na publicação abre o diálogo da agenda
  com tipo Prazo, dia inteiro e o processo já vinculados. A descrição leva só a referência
  (data e diário), nunca o texto da publicação: descrição de evento vai para lembretes.
  A sugestão automática de prazo por IA é da Fase 3 e não é tocada aqui.
- **Agenda por responsável.** Filtro por advogado na agenda (`responsavelId` no endpoint de
  listagem) e campo Responsável no evento.
- **Quadro de processos.** Visão "Quadro" em `/processos`, com colunas Ativo, Suspenso e
  Encerrado. Mover um cartão usa o mesmo `PATCH /processos/{id}/status` da tela de detalhe,
  então as regras de transição são as do servidor, com atualização otimista e volta ao lugar
  se ele recusar. **Arquivado não é coluna**: arquivar é estado final com confirmação própria.
  Cada coluna mostra até 100 processos e avisa quando há mais; arrastar exige ponteiro, e o
  seletor "Mover para" de cada cartão é o caminho por teclado.
