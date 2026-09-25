# ADR-038: Feature flags por tenant

**Status:** Aceito — implementado na Etapa 0.6

## Contexto

O roadmap do CLAUDE.md libera módulos/funcionalidades em fases distintas
(Legal na Fase 2, Workflow na Fase 3, Athena na Fase 5, ...). Mesmo depois
de uma funcionalidade estar pronta no código, muitas vezes é desejável
liberá-la gradualmente — só para alguns escritórios (early adopters), com
possibilidade de desligar rapidamente sem deploy caso algo dê errado.

A tabela `feature_flags` (por tenant) já existia desde a Etapa 0.4, com
handlers de CRUD administrativo (`GetFeatureFlagsHandler`/
`UpdateFeatureFlagHandler`). Faltava um serviço de leitura eficiente para
**checar em runtime** se uma flag está ligada para um tenant específico —
os handlers de CRUD não são adequados para isso: fazem uma query completa a
cada chamada, sem cache.

## Decisão

1. `FeatureFlagKeys` (Platform.Domain.FeatureFlags) — constantes para as
   chaves conhecidas: `LegalModule`, `WorkflowModule`, `AiClassification`,
   `AthenaAssistant`, `PublicApi`, `WhatsAppNotifications`,
   `GoogleCalendarSync`, `KanbanBoard`, `FinancialModule`.
2. `IFeatureFlagService` (Platform.Application.Abstractions) —
   `IsEnabledAsync(key, tenantId)` e `InvalidateCacheAsync(key, tenantId)`.
   Implementação real (`FeatureFlagService`, Platform.Infrastructure) lê
   `feature_flags` via `IgnoreQueryFilters` (o `tenantId` já vem explícito
   no parâmetro — não faz sentido também aplicar o filtro global de tenant
   ambiente, que pode ser de outro tenant, ex.: um job de background) e
   cacheia o resultado em Redis por 5 minutos (`ICacheService.GetOrSetAsync`).
   **Fail-closed**: ausência de registro para o par (tenant, key) é tratada
   como desabilitada — um módulo nunca "vaza" ligado por omissão.
3. `UpdateFeatureFlagHandler` chama `InvalidateCacheAsync` após qualquer
   mutação bem-sucedida, para nunca servir um valor de cache desatualizado
   depois de um toggle administrativo.
4. `Microsoft.FeatureManagement` também é registrado (`AddFeatureManagement`)
   para toggles globais simples via appsettings (seção
   "FeatureManagement") — mecanismo complementar, mais simples, sem
   variação por tenant; `IFeatureFlagService` é o caminho recomendado para
   qualquer decisão que precise variar por escritório.

## Consequências

**Positivas:**
- Testado (Etapa 0.6): flag existente e habilitada → `true`; flag
  inexistente → `false` (fail-closed); isolamento por tenant verificado
  (habilitar para um tenant não afeta outro); invalidação de cache
  verificada via mock de `ICacheService`.
- Cache de 5 minutos reduz drasticamente a carga no Postgres para uma
  checagem que, em potencial, roda em todo request/handler de um módulo
  liberado gradualmente.
- `IFeatureFlagService` vive em Platform.Application.Abstractions — módulos
  de negócio (Legal/Workflow) da Fase 1+ conseguem consumi-lo sem depender
  de Platform.Infrastructure diretamente.

**Negativas / trade-offs:**
- TTL de 5 minutos significa que um toggle manual (fora do fluxo do
  `UpdateFeatureFlagHandler`, ex.: um UPDATE direto no banco) só reflete
  depois desse tempo — aceitável para o caso de uso (rollout gradual, não
  kill-switch de emergência via SQL).
- `FeatureFlagFilter` (action filter para bloquear endpoints por flag
  desabilitada, mencionado como opcional no prompt desta etapa) não foi
  implementado — não há ainda nenhum endpoint atrás de uma flag para
  justificá-lo; revisitar quando o primeiro módulo de negócio (Fase 1+)
  precisar disso.
