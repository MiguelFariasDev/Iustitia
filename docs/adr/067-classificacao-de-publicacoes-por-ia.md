# ADR-067 — Classificação de publicações e sugestão de prazo por IA

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.2

## Contexto

O job do CNJ captura publicações, mas a advogada ainda precisa ler cada uma para saber que ato
é e que prazo abre. Queremos que o Claude leia e **sugira** o tipo do ato e o prazo — mantendo
a revisão humana obrigatória (ADR-012, Provimento CFOAB nº 271/2025). Um prazo perdido causa
dano real a um cliente, então o desenho parte da premissa de que **a saída do modelo é uma fonte
não confiável**.

## Decisão

1. **Sugestão, nunca ação.** O resultado vai para `publication_suggestions` (uma por
   publicação, imutável). Nada altera `Publication.Status`/`Type`, cria prazo, tarefa ou alerta —
   isso é a revisão humana (Etapa 3.3). Um teste do job garante que a publicação continua
   `PendingReview` e sem tipo depois da classificação.
2. **Duas chamadas, dois prompts.** `IClassificationService` devolve tipo do ato + prazo inicial;
   `IDeadlineSuggestionService`, chamado só após uma classificação bem-sucedida, refina o prazo
   para aquele tipo. O prazo refinado vale mais que o da classificação; se a segunda chamada
   falhar, a sugestão é gravada com o prazo da primeira (já paga e válida) em vez de ser
   descartada. A **confiança da sugestão é o menor** dos dois valores.
3. **Validação estrita do contrato** (`AiResponseParser`, System.Text.Json): JSON malformado,
   campo ausente ou de tipo errado, `act_type` fora de uma **lista fechada**, prazo fora de
   0–365, `confidence` fora de 0–100 e prazo sem forma de contagem (ou o inverso) são
   **rejeitados**, não "consertados". O banco repete as regras em CHECK constraints
   (`014_publication_suggestions.sql`). O único tolerado é a cerca markdown em volta do JSON.
   Resposta inválida vira `Result` de erro `AI_INVALID_RESPONSE`; o log traz só o nome do campo.
4. **Confiança < 50 ⇒ revisão manual.** Gravada em `requires_manual_review`. Escala 0–100 inteira
   (a coluna é `int`).
5. **Sem laço de cobrança.** Resposta inválida é cobrada; após `MaxInvalidResponseAttempts` (3,
   contadas em `ai_usage_logs`) o job para de tentar aquela publicação, que segue na fila de
   revisão sem sugestão. O job pagina por cima das publicações esgotadas para não matar de fome
   as seguintes. Provedor fora do ar (indisponível, timeout, limite, credencial) interrompe a
   execução inteira e relança para o retry do Hangfire.
6. **Job** `PublicationClassificationJob`: a cada 15 min, lote por escritório
   (`BatchSizePerTenant`), `DisableConcurrentExecution` (lock distribuído — duas instâncias
   cobrariam a mesma publicação duas vezes), idempotente pelo índice único
   `ux_publication_suggestions_publication`.
7. **Defesa contra injeção de prompt.** O texto da publicação entra entre `<publicacao>` e o
   system prompt o define como dado; o placeholder é substituído em uma passada (o texto não
   é reinterpretado como template); `act_type` que entra no prompt de prazo só aceita valores da
   lista fechada; e a saída é validada de qualquer forma.
8. **Texto longo é cortado** (`MaxContentCharacters`, 16.000) para limitar custo, e a sugestão
   marca `content_truncated` para o advogado saber que o modelo viu só o começo.

## Fronteiras de módulo

`Legal.Application` define `IClassificationService`/`IDeadlineSuggestionService` com tipos
próprios (sem vazar o provedor). `Legal.Infrastructure` depende só de **portas** de
`Integrations.Anthropic.Application`; os hosts registram as implementações. Verificado em
`AiModuleConventionTests`.

## Consequências

- (+) Uma resposta ruim do modelo nunca chega ao banco nem ao advogado como se fosse boa.
- (+) O custo de uma publicação "teimosa" tem teto.
- (−) Duas chamadas por publicação dobram latência e custo frente a uma só; o prompt de prazo
  pode ser desligado se a medição mostrar que a primeira já basta.
- (−) A lista fechada de tipos de ato precisa ser mantida em dois lugares (prompt e
  `ActTypes`); `EmbeddedPromptCatalogTests` falha se divergirem.
