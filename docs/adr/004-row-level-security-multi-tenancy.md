# ADR-004: Row-Level Security para multi-tenancy

**Status:** Aceito

## Contexto

O isolamento de dados entre escritórios (tenants) é um **princípio
inegociável** do produto (ver `claude.md`, seção 3, e
`docs/requisitos/01-visao-geral-e-principios.md`): um bug que vaze dados de
um escritório para outro é inaceitável — tanto por confidencialidade quanto
por sigilo profissional (Código de Ética da OAB). Depender apenas de
filtros `WHERE tenant_id = @tenantId` na camada de aplicação é frágil: um
único `Include`, join manual ou query Dapper esquecida quebra o isolamento
silenciosamente.

## Decisão

Aplicar **Row-Level Security (RLS)** nativa do PostgreSQL em **todas** as
tabelas de negócio com `tenant_id` (ver `docs/requisitos/09-modelo-de-dados.md`),
como camada de defesa **em profundidade**, complementar (não substituta) ao
filtro global do EF Core:

```sql
CREATE POLICY tenant_isolation ON processes
  USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

A aplicação também aplica um filtro global equivalente no `BaseDbContext`
(ver `BuildingBlocks.Infrastructure`, Etapa 2) a partir de `ITenantContext`.
Mesmo que o filtro da aplicação falhe por erro de programação, o RLS no
banco impede o vazamento.

## Consequências

**Positivas:**
- Isolamento garantido mesmo diante de bugs na camada de aplicação
  (defesa em profundidade).
- Auditável diretamente no banco, independente do código da aplicação.

**Negativas / trade-offs:**
- Exige que a conexão do EF Core configure `app.current_tenant` por sessão/
  transação (implementado na Etapa 2, via interceptor no `BaseDbContext`).
- Policies precisam ser criadas e testadas para cada nova tabela — processo
  que deve fazer parte do checklist de toda migration.
- Testes de integração devem validar isolamento com dois tenants distintos
  (ver critério de aceite da Fase 0 em `claude.md`).
