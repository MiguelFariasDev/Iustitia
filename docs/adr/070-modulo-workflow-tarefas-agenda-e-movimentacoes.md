# ADR-070 — Módulo Workflow: tarefas, agenda e movimentações

**Status:** aceito · **Relacionado:** ADR-001 (modular monolith), ADR-004 (RLS), ADR-055 (módulo Legal)

## Contexto

O app móvel precisa de prazos, agenda e movimentações do processo. O módulo `Workflow` existia só como
pastas vazias: não havia entidade de tarefa nem de evento. Esta etapa o cria do zero, **só com o que o
app lê** e o mínimo para que haja dado a ler.

## Decisão

1. **`Workflow` com o mesmo desenho de `Legal`**: Domain / Application / Infrastructure / Api, seu próprio
   `WorkflowDbContext` (mesmo banco), `IWorkflowUnitOfWork` próprio, migration `InitialCreate`, RLS em
   `015_workflow.sql`. Não depende de Legal nem de Platform.Domain/Application.
2. **`WorkTask` (prazo/tarefa)** e **`CalendarEvent` (compromisso com horário)** são agregados separados.
   Prazo **não** é um tipo de evento: a agenda os junta na resposta (`Kind=Task, Type=Deadline`).
3. **O vencimento é uma DATA (`DateOnly`, coluna `date`)**, não um instante. Prazo jurídico vence "no dia
   12/11"; um instante em UTC faria um prazo de 12/11 à noite, em São Paulo, parecer do dia 13.
4. **"Hoje" é o dia no fuso do escritório** (`Workflow:TimeZoneId`, padrão `America/Sao_Paulo`),
   decidido pelo `BusinessCalendar`. Há testes para as 22h–00h, onde UTC e São Paulo discordam. Um fuso
   inexistente falha ao subir, não numa consulta. *Quando o produto tiver fuso por escritório, é aqui que ele entra.*
5. **Semântica dos filtros** (`GET /tasks`): `today` = vence hoje; `overdue` = venceu antes de hoje **e está
   pendente** (concluída nunca está atrasada, nem pedindo `status=Completed`); `next_7_days` = de hoje a
   hoje+7, inclusive. `status` e `filter` são independentes; valor inválido é `TASK_FILTER_INVALID` (400).
6. **Resumo** (`GET /tasks/summary`): pendentes, vencendo hoje, atrasadas, próximos 7 dias e concluídas no mês
   — todos exceto o último contam só pendentes.
7. **Agenda** (`GET /agenda?start&end`, AAAA-MM-DD, inclusivos): eventos e prazos num só lugar, cada item com
   `Date` (o dia no fuso do escritório) para o app agrupar por dia. No dia: prazos primeiro, depois eventos por
   horário. **Teto de 62 dias** (`AGENDA_PERIOD_INVALID`): sem ele, uma chamada com 20 anos traria a agenda inteira.
8. **Tarefas e eventos são do usuário** (assignee/dono), por menor privilégio. A agenda do escritório inteiro é
   decisão de produto, não técnica — o filtro está num lugar só, o repositório.
9. **Escrita mínima, além do pedido.** O pedido listava só `GET`. Endpoints de leitura sobre uma entidade que
   ninguém consegue criar devolveriam zeros para sempre — e o app os mostraria como se fossem reais. Por isso
   há `POST /tasks`, `PATCH /tasks/{id}/complete` e `POST /agenda/events`. Não há edição, exclusão nem atribuição
   a outro usuário.
10. **`ProcessoId` é só uma referência**: sem chave estrangeira nem validação, porque o Workflow não pode
    conhecer Legal (ADR-001). Um processo apagado deixa a referência órfã e a tela precisa tolerar isso.

## Movimentações (`GET /processos/{id}/movimentacoes`, módulo Legal)

Derivadas das **publicações capturadas do diário (DJEN)** para o processo — **não** do andamento completo do
tribunal. O DataJud, hoje, só entrega metadados do cadastro (ADR-064). Cada item: `data`, `tipo`, `descricao`,
`fonte`, `lida`. A `descricao` é o **trecho de até 280 caracteres**; o inteiro teor nunca sai (sigilo
profissional) — há teste que fixa o conjunto de campos da resposta. Quando houver movimentos do tribunal,
entram com outra `fonte`, sem mudar o contrato.

## Consequências e limites

- **Sem eventos de domínio ainda**: o `WorkflowDbContext` não mapeia `outbox_messages`. O primeiro evento
  exigirá mapear a tabela com `ExcludeFromMigrations`, como em Legal.
- **Alerta de prazo** (a tarefa gerar um alerta/push quando vence) **não existe**: seria um job novo.
- **Tarefas só se criam pela API**: não há tela no web para isso ainda.
