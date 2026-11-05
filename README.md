# Iustitia

Sistema de gestão para escritórios de advocacia, simplicidade de uso e IA integrada (assistente Athena).

> ## Projeto de portfólio — não comercializado
>
> Este projeto usa a **API Pública do DataJud (CNJ)** para consultar metadados de processos
> e auto-preencher o cadastro. O uso comercial dessa API é **proibido** pelo Termo de Uso do
> CNJ (cláusulas 3.3 e 3.8), e consumir a API implica aceitar esses termos.
>
> O Iustitia **não é um produto comercial**: não é vendido, licenciado nem cobrado de
> nenhuma forma. Se um dia for comercializado, a integração com o DataJud sai — a porta
> `IConsultaProcessoTribunal` existe para tornar essa troca barata.
>
> Detalhes: [`docs/compliance/datajud-termo.md`](./docs/compliance/datajud-termo.md),
> [`docs/compliance/portfolio.md`](./docs/compliance/portfolio.md) e a **ADR-064**.
>
> A captura de **publicações** usa o DJEN/Comunica, que é outra API, com outro termo de uso.

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
- **Mobile:** Flutter (Dart) para iOS e Android — Riverpod, GoRouter, Dio, Supabase

## Arquitetura

Modular Monolith + Clean Architecture (Domain / Application /
Infrastructure / Api) + Vertical Slice, com separação explícita entre:

- **Plataforma** (`src/BuildingBlocks`, `src/Platform`) — núcleo
  compartilhado por todo o sistema: tenancy, identidade, auditoria,
  settings, feature flags, jobs.
- **Módulos de negócio** (`src/Modules/*`) — Legal, Atendimento, Workflow,
  Notifications, Integrations, Reporting, CRM, Documents, Athena, Api —
  cada um resolvendo um domínio jurídico específico.

O **Bot de WhatsApp** é um produto à parte (repositório e banco próprios) e conversa com o
Core só por API REST. O módulo **Atendimento** é a metade do Core dessa integração: inbox do
advogado, handoff entre bot e humano e a API que o Bot consome — ver
[`docs/modules/atendimento.md`](./docs/modules/atendimento.md) e o
[ADR-067](./docs/adr/067-modulo-atendimento-inbox-e-handoff.md).

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
/mobile             -> app Flutter (iOS e Android)
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

### Testes de integração sem Docker

Os testes de `tests/IntegrationTests/Platform.IntegrationTests` sobem PostgreSQL via Testcontainers.
Sem Docker, aponte para um PostgreSQL 16 já em pé (superusuário); a fixture cria e remove um banco
descartável nele:

```bash
ADVOCACIA_TEST_PG_SERVER="Host=127.0.0.1;Port=5432;Username=postgres;Password=postgres" \
  dotnet test tests/IntegrationTests/Platform.IntegrationTests
```

Parte dos testes de fluxo de autenticação e de tenant também depende de Redis (`localhost:6380`) e
RabbitMQ; sem eles esses testes falham ou expiram.
### Mobile

```bash
cd mobile
flutter pub get
# Demonstração, sem Supabase nem API (login: ana.souza@escritorio.com.br / demo1234):
flutter run --dart-define=AUTH_MODE=mock --dart-define=API_MOCKS=true
flutter test
```

Configuração real e detalhes em [`mobile/README.md`](./mobile/README.md).

### Infraestrutura local

```bash
docker compose -f infra/docker/docker-compose.yml up -d
npx supabase start
```

## Documentação

- [`docs/adr`](./docs/adr) — Architecture Decision Records
- [`docs/compliance`](./docs/compliance) — LGPD, OAB, contrato Anthropic
- [`docs/navigation`](./docs/navigation) — sitemaps web e mobile
- [`docs/mobile`](./docs/mobile) — app mobile (telas, notificações, lacunas do backend)
- [`docs/api`](./docs/api) — documentação da API (OpenAPI/Swagger)
- [`docs/modules`](./docs/modules) — módulos de negócio (alertas, atendimento, legal)

## Princípios inegociáveis

1. Revisão humana obrigatória em todo fluxo de IA (Provimento CFOAB nº 271/2025)
2. Isolamento de dados entre escritórios (multi-tenant via RLS)
3. LGPD — criptografia em repouso e em trânsito, direito ao esquecimento
4. Sigilo profissional — nenhum dado usado para treinar modelos de terceiros
5. Transparência com cliente final (Recomendação OAB nº 001/2024)
6. Responsabilidade final do advogado sempre preservada
