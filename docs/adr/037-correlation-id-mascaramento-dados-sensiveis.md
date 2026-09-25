# ADR-037: Correlation ID e mascaramento de dados sensíveis

**Status:** Aceito — implementado na Etapa 0.6

## Contexto

Um fluxo de negócio real atravessa vários processos assíncronos: requisição
HTTP → handler → domain event → outbox → MassTransit → consumer (Worker).
Sem um identificador comum, correlacionar essas etapas nos logs é
impraticável. Ao mesmo tempo, logs estruturados capturam qualquer string que
passe por eles — sem uma política explícita, CPF, CNPJ, e-mail, telefone e
tokens de autenticação acabam vazando para o sistema de log (violação da
LGPD, ver `docs/compliance/lgpd.md`).

## Decisão

**Correlation ID:**

1. `CorrelationIdMiddleware` (Platform.Api, primeiro middleware do pipeline)
   lê `X-Correlation-Id` da requisição, ou gera um novo `Guid`, grava em
   `LogEnrichmentContext.CorrelationId` (estado ambiente via `AsyncLocal` —
   BuildingBlocks.Infrastructure.Observability) e devolve o mesmo valor no
   header de resposta.
2. `CorrelationIdEnricher` (Serilog) adiciona a propriedade `CorrelationId`
   a todo log event, lendo desse mesmo estado ambiente — funciona tanto em
   requisições HTTP quanto em jobs/consumers.
3. `OutboxInterceptor` grava o `CorrelationId` ambiente em cada
   `OutboxMessage` (nova coluna `correlation_id`, nullable). `OutboxProcessor`
   o repassa ao publicar via MassTransit (`PublishContext.CorrelationId`).
4. `CorrelationIdConsumeFilter<TMessage>` (MassTransit) lê
   `ConsumeContext.CorrelationId` e o restaura no `LogEnrichmentContext`
   durante o processamento da mensagem — todo log dentro do consumer (e de
   qualquer serviço que ele chame) carrega o mesmo id, sem precisar passá-lo
   explicitamente adiante.
5. `CorrelationIdPropagationHandler` (`DelegatingHandler`, registrado via
   `ConfigureHttpClientDefaults` — todo `HttpClient` criado pelo
   `IHttpClientFactory` já ganha esse handler automaticamente) propaga o
   mesmo id para chamadas HTTP de saída (CNJ, Anthropic, fases futuras).

**Mascaramento de dados sensíveis:**

`LogMaskingPolicy` (função pura, sem dependência do Serilog — testável
isoladamente) mascara, via regex: CPF (`000.000.000-00` →
`***.***.***-**`), CNPJ (`00.000.000/0000-00` → `**.***.***/****-**`),
e-mail (`usuario@dominio.com` → `us***@dominio.com`), telefone
(`(00) 00000-0000` → `(00) *****-****`, mantendo o DDD visível) e tokens
Bearer (`Bearer xxx` → `Bearer ***`). `SensitiveDataMaskingEnricher`
aplica essa política a todo valor de propriedade string de cada log event,
antes de qualquer sink renderizar — cobre tanto sinks estruturados quanto o
texto final do Console.

Senhas nunca passam por regex de mascaramento: a regra é simplesmente nunca
logar o campo de senha (não existe um padrão de texto genérico e seguro
para "isto parece uma senha").

## Consequências

**Positivas:**
- Testado (Etapa 0.6): requisição sem `X-Correlation-Id` recebe um novo no
  header de resposta; requisição com o header o ecoa de volta inalterado
  (`CorrelationIdTests`, integração via HTTP real).
- `LogMaskingPolicyTests` cobre os 5 tipos de dado sensível isoladamente,
  sem precisar montar um pipeline de log completo para testar a regra.
- CNPJ é verificado antes de CPF na cadeia de regex — evita que o prefixo
  numérico de um CNPJ seja mascarado (incorretamente) como CPF primeiro.

**Negativas / trade-offs:**
- O mascaramento cobre propriedades estruturadas (a forma correta e
  recomendada de logar), não literais soltos dentro do texto de uma
  exceção de terceiros (ex.: uma mensagem de erro de biblioteca externa que
  já contenha um CPF embutido). Mitigação: nunca logar `exception.Message`
  de exceções que manipulam dados de cliente sem antes mascarar.
- Correlação de jobs Hangfire fica parcial nesta etapa: cada execução de
  job não gera um `CorrelationId` próprio automaticamente — apenas
  requisições HTTP e mensagens de bus o fazem. Revisitar se jobs passarem a
  precisar de rastreabilidade ponta a ponta tão fina quanto HTTP/mensageria.
