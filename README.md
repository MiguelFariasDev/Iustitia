# Iustitia

Sistema de gestão para escritórios de advocacia, simplicidade de uso e IA integrada (assistente Athena).

>
> **Requisitos detalhados:** [`docs/requisitos/00-indice.md`](./docs/requisitos/00-indice.md)
> (não versionado neste repositório — uso interno).

## Stack (resumo)

- **Backend:** .NET 10 (LTS) + C# 14, ASP.NET Core, EF Core 10, MediatR,
  FluentValidation, Polly, Hangfire, MassTransit
- **Frontend Web:** React 19 + TypeScript + Vite + Tailwind + shadcn/ui
- **Banco de Dados:** Supabase (PostgreSQL gerenciado, RLS multi-tenant)
- **Auth:** Supabase Auth (JWT)
- **Hospedagem:** Microsoft Azure (Container Apps + Static Web Apps)
- **IA:** Anthropic Claude (Opus para o assistente Athena, Sonnet para
  classificação em lote, Haiku para tarefas simples)
- **Mobile:** Swift 6 + SwiftUI (iOS 26+)

## Arquitetura

Modular Monolith + Clean Architecture (Domain / Application /
Infrastructure / Api) + Vertical Slice, com separação explícita entre:

- **Plataforma** (`src/BuildingBlocks`, `src/Platform`) — núcleo
  compartilhado por todo o sistema: tenancy, identidade, auditoria,
  settings, feature flags, jobs.
- **Módulos de negócio** (`src/Modules/*`) — Legal, Workflow,
  Notifications, Integrations, Reporting, CRM, Documents, Athena, Api —
  cada um resolvendo um domínio jurídico específico.

## Estrutura do Monorepo

```
/src
  /BuildingBlocks   -> abstrações compartilhadas (Domain/Application/Infrastructure)
  /Platform         -> núcleo multi-tenant (Domain/Application/Infrastructure/Api)
  /Modules          -> módulos de negócio (Legal, Workflow, CRM, Documents, Athena, ...)
  /Hosts            -> composition roots (Api, Worker)
/tests
  /UnitTests
  /IntegrationTests
  /ArchitectureTests
/web                -> frontend React
/mobile-ios         -> app Swift 6 / SwiftUI
/infra              -> docker, terraform, supabase
/docs               -> ADRs, compliance, navegação, diagramas
/scripts            -> scripts de seed e migração
```

## Como rodar

### Backend

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Hosts/Api
dotnet run --project src/Hosts/Worker
```

### Frontend

```bash
cd web
npm install
npm run dev
```

### Infraestrutura local

```bash
docker compose -f infra/docker/docker-compose.yml up -d
npx supabase start
```

## Documentação

- [`docs/adr`](./docs/adr) — Architecture Decision Records
- [`docs/compliance`](./docs/compliance) — LGPD, OAB, contrato Anthropic
- [`docs/navigation`](./docs/navigation) — sitemaps web e mobile
- [`docs/api`](./docs/api) — documentação da API (OpenAPI/Swagger)

## Princípios inegociáveis

1. Revisão humana obrigatória em todo fluxo de IA (Provimento CFOAB nº 271/2025)
2. Isolamento de dados entre escritórios (multi-tenant via RLS)
3. LGPD — criptografia em repouso e em trânsito, direito ao esquecimento
4. Sigilo profissional — nenhum dado usado para treinar modelos de terceiros
5. Transparência com cliente final (Recomendação OAB nº 001/2024)
6. Responsabilidade final do advogado sempre preservada
