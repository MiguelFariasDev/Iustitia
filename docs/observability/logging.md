# Logging

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md),
[ADR-037](../adr/037-correlation-id-mascaramento-dados-sensiveis.md).

## Convenções

- Sempre logar via `ILogger<T>` (nunca `Console.WriteLine`), com placeholders
  estruturados (`logger.LogInformation("Tenant {TenantId} criado", tenantId)`),
  nunca interpolação de string (`$"Tenant {tenantId} criado"`) — interpolação
  perde a propriedade estruturada e não passa pelo `SensitiveDataMaskingEnricher`.
- **Information**: eventos de negócio relevantes (tenant criado, outbox
  processada, job executado). **Warning**: algo fora do esperado mas não
  quebrou o fluxo (outbox degradada, performance acima do limiar). **Error**:
  falha real que impediu uma operação (exceção não tratada, falha ao
  publicar evento).
- Nunca logar: payload completo de eventos de domínio/integração, corpo de
  requisição/resposta HTTP, senha, connection string, chave de API.

## Mascaramento de dados sensíveis

`LogMaskingPolicy.Mask(string)` (BuildingBlocks.Infrastructure.Observability)
mascara CPF, CNPJ, e-mail, telefone e tokens Bearer. Aplicado automaticamente
a toda propriedade string de log via `SensitiveDataMaskingEnricher` — não
precisa ser chamado manualmente na maioria dos casos. Chame diretamente só
se for compor uma string a ser logada em um contexto fora do pipeline do
Serilog (raro).

## Correlation ID

Ver ADR-037. Toda requisição HTTP e toda mensagem de mensageria carrega um
`CorrelationId`, disponível em qualquer log via a propriedade `CorrelationId`
— não precisa ser passado manualmente entre camadas.

## Como adicionar um novo enricher

1. Implemente `Serilog.Core.ILogEventEnricher` em
   `BuildingBlocks.Infrastructure/Observability/`.
2. Registre em `SerilogConfiguration.AddAdvocaciaSerilog`:
   `.Enrich.With<MeuEnricher>()`.
3. Se o enricher precisar de estado ambiente (algo que só existe durante uma
   requisição/job, não injetável via DI no momento da configuração do
   Serilog), siga o padrão de `LogEnrichmentContext` (AsyncLocal estático) —
   nunca tente resolver um serviço `Scoped` a partir do `IServiceProvider`
   raiz usado para configurar o logger.
