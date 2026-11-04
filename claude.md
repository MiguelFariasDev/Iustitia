================================================================================
CLAUDE.md — IUSTITIA
Arquivo de Contexto para o Agente Claude Code (Anthropic)
Versão 1.0 — Setembro de 2026
================================================================================

Este arquivo é o guia mestre para o agente Claude Code trabalhar neste
repositório. Ele contém contexto, regras, convenções, comandos e princípios
que não são negociáveis. Leia com atenção antes de qualquer ação.

================================================================================
SUMÁRIO
================================================================================

1.  Visão Geral do Projeto
2.  Stack Tecnológica (Anthropic + Azure + Supabase)
3.  Princípios Inegociáveis
4.  Arquitetura
5.  Módulos (Bounded Contexts)
6.  Marca Desacoplada (Regra Crítica)
7.  Estrutura de Pastas
8.  Convenções de Código
9.  Comandos Úteis
10. Fluxo de Trabalho
11. Fases de Desenvolvimento
12. Critérios de Aceite da Fase 0
13. ADRs a Criar
14. Segurança e Compliance
15. O que Nunca Fazer
16. O que Sempre Fazer
17. Perguntas de Decisão Pendentes
18. Contato e Suporte
19. Lembrete Final

================================================================================
1. VISÃO GERAL DO PROJETO
================================================================================

Produto: Iustitia — sistema de gestão para escritórios de advocacia.
Concorrentes diretos: Astrea, ADVBOX, Projuris.
Público-alvo: escritórios pequenos e médios (1 a 15 advogados).

Diferencial estratégico:
- Confiança na automação (revisão humana obrigatória em todo o fluxo de IA)
- Simplicidade de uso (curva de aprendizado baixa)
- Preço acessível
- Assistente de IA integrado (Athena)

Estratégia de construção: "Platform First". Começamos pelo núcleo que sustenta
tudo (identidade, multi-tenancy, auditoria, jobs, observabilidade) e derivamos
as funcionalidades a partir dessa base.

================================================================================
2. STACK TECNOLÓGICA (ANTHROPIC + AZURE + SUPABASE)
================================================================================

--------------------------------------------------------------------------
INTELIGÊNCIA ARTIFICIAL — ANTHROPIC (CLAUDE)
--------------------------------------------------------------------------
- Provedor principal de IA: Anthropic
- Modelos:
  * Claude Sonnet 4.5 (ou mais recente) — classificação de publicações,
    sugestão de prazos, análise de documentos
  * Claude Opus 4.5 (ou mais recente) — tarefas complexas, raciocínio
    jurídico avançado, assistente Athena
  * Claude Haiku 4.5 (ou mais recente) — tarefas simples, alta velocidade,
    baixo custo (ex: sumarização rápida)
- SDK: Anthropic .NET SDK (oficial) ou cliente HTTP customizado
- API: Messages API (não mais Completion API)
- Recursos a usar:
  * Prompt caching (reduz custo em prompts repetidos)
  * Tool use / function calling (para Athena executar ações)
  * Vision (para análise de documentos digitalizados, se aplicável)
  * Extended thinking (para raciocínio jurídico complexo)
- Contrato de não-treinamento: Anthropic não treina com dados de API por
  padrão. Documentar por escrito.
- Abstração: IClassificationService e IAthenaService (permitem trocar
  provider se necessário)
- Prompts versionados em arquivos .md com testes de regressão
- Log de todas as chamadas (input, output, tokens, custo) para auditoria
- Fallback: Azure OpenAI (apenas se Anthropic indisponível por política
  interna ou compliance regional)

--------------------------------------------------------------------------
BACKEND — .NET 10 (LTS)
--------------------------------------------------------------------------
- .NET 10 LTS (suporte de longo prazo; ~3 anos)
- C# 14+
- ASP.NET Core Web API
- Entity Framework Core 10
- Dapper 2.x (queries críticas)
- MediatR 12+
- FluentValidation 11+
- Mapster (mapeamento)
- Serilog (logging estruturado JSON)
- Polly 8+ (resiliência)
- Refit (clientes HTTP tipados)
- Hangfire 2.x (jobs agendados)
- MassTransit 8+ (mensageria)
- OpenTelemetry .NET
- AspNetCore.HealthChecks
- AspNetCore.RateLimiting
- Microsoft.FeatureManagement
- Konscious.Security.Cryptography.Argon2 (hash de senha)
- NetArchTest (testes de arquitetura)

--------------------------------------------------------------------------
BANCO DE DADOS — SUPABASE (PostgreSQL GERENCIADO)
--------------------------------------------------------------------------
- PostgreSQL 16+ (via Supabase)
- Row-Level Security (RLS) para isolamento multi-tenant
- JSONB para payloads semi-estruturados
- Full-text search nativo
- Extensão pgcrypto (criptografia em nível de coluna)
- Extensão pg_trgm (busca fuzzy)
- Extensão uuid-ossp (IDs)
- Supabase Realtime (push interno web)
- Supabase Storage (documentos anexados)
- Supabase Vault (segredos por tenant)

--------------------------------------------------------------------------
AUTENTICAÇÃO — SUPABASE AUTH
--------------------------------------------------------------------------
- Supabase Auth gerencia credenciais, MFA, OAuth, emissão de JWT
- Backend .NET valida o JWT emitido pelo Supabase (JWKS endpoint)
- Claims customizadas (tenant_id, role, permissions) via Custom Access Token
  Hook do Supabase
- Refresh tokens gerenciados pelo SDK do Supabase

--------------------------------------------------------------------------
HOSPEDAGEM — MICROSOFT AZURE
--------------------------------------------------------------------------
- Azure Container Apps (backend .NET + worker jobs)
- Azure Static Web Apps (frontend React)
- Azure Container Registry (ACR)
- Azure Key Vault (segredos)
- Azure Service Bus (mensageria)
- Azure Cache for Redis
- Azure Application Insights (observabilidade)
- Azure Front Door (CDN + WAF)
- Azure Blob Storage (arquivos grandes)

--------------------------------------------------------------------------
FRONTEND WEB — REACT + TYPESCRIPT
--------------------------------------------------------------------------
- React 19+
- TypeScript 5.5+ (strict mode)
- Vite
- React Router 7
- TanStack Query
- Zustand
- React Hook Form + Zod
- Tailwind CSS 4+
- shadcn/ui
- Lucide React
- Recharts ou ApexCharts
- Supabase JS Client
- Axios com interceptor
- Vitest + Testing Library
- Playwright

--------------------------------------------------------------------------
MOBILE iOS — SWIFT 6 + SWIFTUI (iOS 26+)
--------------------------------------------------------------------------
- Swift 6+ (concurrency estrita, Sendable, actors)
- SwiftUI
- Swift Concurrency (async/await, Task, Actor)
- SwiftData (persistência local)
- @Observable macro
- NavigationStack + NavigationPath
- URLSession + async/await
- Supabase Swift SDK
- Anthropic Swift SDK (ou URLSession direto para API Anthropic)
- APNs (push notifications)
- Keychain (tokens)
- LocalAuthentication (Face ID / Touch ID)
- WidgetKit + Live Activities
- SwiftLint + SwiftFormat
- XCTest + Swift Testing
- Xcode Cloud ou GitHub Actions com macOS runner
- iOS 26+ (mínimo)

--------------------------------------------------------------------------
OBSERVABILIDADE E DEVOPS
--------------------------------------------------------------------------
- OpenTelemetry -> Azure Application Insights
- Serilog -> Application Insights
- GitHub Actions (CI/CD)
- Terraform (IaC — provider Azure)
- Docker + Docker Compose (dev local)

================================================================================
3. PRINCÍPIOS INEGOCIÁVEIS
================================================================================

Estes princípios são requisitos legais e éticos, não escolhas de produto.
Nunca viole, nunca sugira contornar, nunca trate como opcional.

1. REVISÃO HUMANA OBRIGATÓRIA: Nenhuma sugestão de IA vira tarefa aprovada
   sem confirmação explícita do advogado. Exigência do Provimento CFOAB
   nº 271/2025.

2. ISOLAMENTO DE DADOS: Cada escritório (tenant) tem dados completamente
   isolados via Row-Level Security (RLS) no PostgreSQL.

3. LGPD: Dados sensíveis criptografados em repouso e em trânsito; exclusão
   sob demanda; DPIA documentado; DPO designado.

4. SIGILO PROFISSIONAL: Nenhum dado processual é usado para treinar modelos
   de terceiros. Contrato de não-treinamento documentado por escrito com
   Anthropic.

5. TRANSPARÊNCIA: O cliente final do escritório deve ser informado sobre uso
   de IA (Recomendação nº 001/2024 da OAB).

6. RESPONSABILIDADE PRESERVADA: O sistema apoia a decisão, nunca substitui o
   advogado. O advogado é o responsável final por cada caso.

================================================================================
4. ARQUITETURA
================================================================================

Padrão principal: Modular Monolith (NÃO microserviços).

Camadas: Clean Architecture + Vertical Slice Architecture.

Padrões internos:
- CQRS pragmático com MediatR
- DDD tático (Entities, Value Objects, Aggregates, Domain Events)
- Transactional Outbox Pattern para eventos confiáveis
- Result Pattern (em vez de exceptions de negócio)
- Specification Pattern para queries complexas
- Multi-tenancy via Row-Level Security (RLS)
- Resiliência com Polly 8 (retry, circuit breaker, timeout, bulkhead)

Regra de dependência: dependências apontam sempre para dentro. Domain não
conhece Infrastructure. Application não conhece Presentation.

================================================================================
5. MÓDULOS (BOUNDED CONTEXTS)
================================================================================

- Identity: auth Supabase + tenants + usuários + roles
- Legal: clientes, processos, publicações
- Workflow: tarefas, fila priorizada, revisão humana, kanban de equipe
- Notifications: e-mail, push, WhatsApp (futuro)
- Integrations: CNJ, Anthropic, Google Calendar, WhatsApp
- Reporting: dashboards, painéis, relatórios
- CRM: gestão de relacionamento (fase futura)
- Documents: assinatura digital, peças jurídicas, arquivos
- Athena: assistente de IA do escritório (Claude Opus 4.5)
- Api: REST + MCP (Model Context Protocol)

Cada módulo tem suas camadas: Domain, Application, Infrastructure, Api.

================================================================================
6. MARCA DESACOPLADA (REGRA CRÍTICA)
================================================================================

Para permitir eventual mudança de nome sem custo alto, use "Marca Desacoplada":

FAÇA:
- Namespaces genéricos: Advocacia.Modules.Legal
- Solution genérica: Advocacia.sln
- Projetos genéricos: Advocacia.Api.csproj
- Bundle ID iOS genérico: br.com.escritorio.advocacia
- Resource Groups Azure genéricos: rg-advocacia-prod
- Nome do banco: advocacia_db
- Marca configurável via APP_NAME / VITE_APP_NAME / AppName (iOS)

NÃO FAÇA:
- Iustitia.Modules.Legal (namespace com marca)
- Iustitia.sln (solution com marca)
- com.iustitia.app (Bundle ID com marca)
- rg-iustitia-prod (Resource Group com marca)
- iustitia_db (banco com marca)

A marca "Iustitia" só aparece em:
- UI (configurável via variável de ambiente)
- appsettings.json ("AppName": "Iustitia")
- Assets (logo, launch screen)
- Documentação (README, docs)
- Textos de marketing

================================================================================
7. ESTRUTURA DE PASTAS
================================================================================

  /src
    /BuildingBlocks
      /BuildingBlocks.Domain
      /BuildingBlocks.Application
      /BuildingBlocks.Infrastructure
    /Platform
      /Platform.Domain
      /Platform.Application
      /Platform.Infrastructure
      /Platform.Api
    /Modules
      /Identity
      /Legal
      /Workflow
      /Notifications
      /Integrations
        /Integrations.CNJ
        /Integrations.Anthropic
        /Integrations.Email
      /Reporting
      /CRM
      /Documents
      /Athena
      /Api
    /Hosts
      /Api
      /Worker
  /tests
    /UnitTests
    /IntegrationTests
    /ArchitectureTests
  /web
    /src
      /app
      /components
      /features
      /services
      /hooks
      /lib
      /styles
    /public
  /mobile-ios
    /Advocacia
      /App
      /Domain
      /Data
      /Presentation
      /Resources
    /AdvocaciaTests
    /AdvocaciaUITests
  /infra
    /docker
    /terraform
    /supabase
      /migrations
      /seed
  /docs
    /adr
    /api
    /compliance
    /navigation
    /diagrams
  /scripts
  .editorconfig
  .gitignore
  Directory.Build.props
  Advocacia.sln
  CLAUDE.md
  README.md

================================================================================
8. CONVENÇÕES DE CÓDIGO
================================================================================

BACKEND (.NET 10 / C# 14):
- Namespaces: Advocacia.Modules.Legal.Features.CreateProcess
- Classes: PascalCase; interfaces: IPrefix; privados: _camelCase
- Async sempre com sufixo Async: GetProcessAsync
- CancellationToken em todos os métodos async
- Nullable reference types habilitado
- Records para DTOs e Value Objects
- Primary constructors quando aplicável
- Collection expressions (C# 12+)
- Sem #region
- Comentários apenas quando o "porquê" não é óbvio
- Result pattern para erros de negócio
- Nunca usar async void (exceto event handlers)
- Nunca usar .Result ou .Wait() (use await)

FRONTEND REACT:
- TypeScript strict (sem any implícito)
- Componentes funcionais + hooks
- Nomes: PascalCase para componentes, camelCase para hooks/utilitários
- TanStack Query para dados remotos
- Zustand para estado global
- Zod para validação
- Tailwind utility-first (sem CSS-in-JS)
- Acessibilidade primeiro (ARIA quando necessário)
- Sem console.log em produção

MOBILE SWIFT 6:
- SwiftLint + SwiftFormat configurados
- Sem force unwrap (!) e sem force try
- Guard clauses para early return
- Nomes descritivos, sem abreviações
- ViewModels com @Observable + @MainActor
- Views sem lógica de negócio
- Sendable em modelos que cruzam actors
- Actors para estado compartilhado
- Concurrency estruturada (sem Task.detached desnecessário)

GIT:
- Branches: main, develop, feature/*, hotfix/*
- Commits: Conventional Commits (feat:, fix:, docs:, refactor:, test:, chore:)
- PRs com descrição, checklist e link para issue
- Squash merge em develop

================================================================================
9. COMANDOS ÚTEIS
================================================================================

BACKEND:
  dotnet restore
  dotnet build
  dotnet test
  dotnet test /p:CollectCoverage=true
  dotnet run --project src/Hosts/Api
  dotnet run --project src/Hosts/Worker
  dotnet ef migrations add NomeDaMigration --project src/Modules/Legal/Legal.Infrastructure --startup-project src/Hosts/Api
  dotnet ef database update --project src/Modules/Legal/Legal.Infrastructure --startup-project src/Hosts/Api

FRONTEND:
  cd web
  npm install
  npm run dev
  npm run build
  npm run test
  npm run lint
  npm run type-check

MOBILE iOS:
  cd mobile-ios
  xcodebuild -project Advocacia.xcodeproj -scheme Advocacia -destination 'platform=iOS Simulator,name=iPhone 16' build
  xcodebuild test -project Advocacia.xcodeproj -scheme Advocacia -destination 'platform=iOS Simulator,name=iPhone 16'

DOCKER:
  docker-compose up -d
  docker-compose logs -f
  docker-compose down
  docker-compose down -v

SUPABASE:
  npx supabase start
  npx supabase stop
  npx supabase migration new nome_da_migration
  npx supabase db push

================================================================================
10. FLUXO DE TRABALHO
================================================================================

Para cada feature, siga este ciclo:

1. Entender: leia os requisitos e o contexto acima
2. Planejar: descreva a abordagem antes de codar
3. Implementar: código + testes
4. Validar: rode testes, lint, build
5. Documentar: atualize README, ADRs, OpenAPI
6. Revisar: verifique conformidade com princípios inegociáveis

Ao final de cada fase, gere um relatório de progresso com:
- O que foi entregue
- O que ficou pendente
- Riscos identificados
- Próximos passos

================================================================================
11. FASES DE DESENVOLVIMENTO
================================================================================

FASE 0 — PLATAFORMA CORE (Semanas 1–3): fundação técnica
FASE 1 — PROVA DE CONCEITO CNJ (Semanas 4–5): captura de publicações
FASE 2 — MÓDULO LEGAL (Semanas 6–10): clientes, processos, publicações
FASE 3 — MÓDULO WORKFLOW + IA (Semanas 11–15): classificação via Claude,
  sugestão de prazo, revisão humana, fila priorizada, alertas
FASE 4 — MÓDULOS DE GESTÃO (Semanas 16–20): tarefas, calendário, pessoas,
  CRM, financeiro (básico), indicações, ferramentas, kanban de equipe
FASE 5 — ASSISTENTE ATHENA + API MCP (Semanas 21–24): chat conversacional
  com Claude Opus 4.5, integração MCP, API REST pública
FASE 6 — PILOTO DE USO REAL (Semanas 25–28): deploy produção Azure
FASE 7 — MOBILE iOS (paralelo ou pós-piloto): app Swift 6/SwiftUI
FASE 8 — EXPANSÃO CORE (~2,5–3,5 meses pós-piloto): financeiro completo,
  busca CPF/CNPJ, gestão de equipe, painel do sócio
FASE 9 — EXPANSÃO SECUNDÁRIA (~1,5–2 meses): Google Calendar, WhatsApp,
  kanban avançado, personalização, evolução mobile

Nunca pule fases. A Fase 0 é a fundação. Sem ela, nada funciona.

================================================================================
12. CRITÉRIOS DE ACEITE DA FASE 0
================================================================================

Ao final da Fase 0, o repositório deve ter:

[ ] Monorepo criado com estrutura de pastas definida
[ ] Solution Advocacia.sln com todos os projetos
[ ] docker-compose up sobe PostgreSQL, Redis, RabbitMQ
[ ] Supabase configurado (dev + prod)
[ ] Migrations iniciais do EF Core aplicadas
[ ] RLS policies configuradas em todas as tabelas de negócio
[ ] Dois tenants distintos criados; dados isolados por RLS (testado)
[ ] Login funcional via Supabase Auth; JWT válido no backend
[ ] Audit log registrando criação de usuário e login
[ ] Health checks respondendo (/health/live, /health/ready)
[ ] Swagger documentando endpoints v1
[ ] Frontend React com login + dashboard inicial funcionando
[ ] Testes de arquitetura (NetArchTest) garantindo isolamento de camadas
[ ] Testes unitários e de integração cobrindo BuildingBlocks
[ ] ADRs documentados (mínimo os 5 primeiros)
[ ] README com instruções de setup
[ ] Deploy em Azure Container Apps + Azure Static Web Apps funcionando
[ ] Pipeline CI/CD básico no GitHub Actions
[ ] Marca Desacoplada aplicada (namespaces genéricos, APP_NAME=Iustitia)
[ ] Integração inicial com Anthropic Claude configurada (chave no Key Vault,
    cliente HTTP testado, sem uso em produção ainda)

================================================================================
13. ADRs A CRIAR
================================================================================

O agente deve criar, em /docs/adr/, os seguintes Architecture Decision Records:

ADR-001: Modular Monolith em vez de Microserviços
ADR-002: PostgreSQL via Supabase como banco principal
ADR-003: Supabase Auth em vez de ASP.NET Core Identity
ADR-004: Row-Level Security para multi-tenancy
ADR-005: Clean Architecture + Vertical Slice
ADR-006: CQRS pragmático com MediatR
ADR-007: Result Pattern em vez de exceptions de negócio
ADR-008: Outbox Pattern para eventos confiáveis
ADR-009: .NET 10 LTS como stack backend
ADR-010: React + TypeScript + Vite + TanStack Query no frontend
ADR-011: Swift 6 + SwiftUI nativo para mobile (iOS 26+)
ADR-012: Revisão humana obrigatória como requisito de domínio
ADR-013: Anthropic Claude como provedor principal de IA
ADR-014: Contrato de não-treinamento com Anthropic
ADR-015: Azure Container Apps em vez de AKS
ADR-016: MassTransit + Azure Service Bus para mensageria
ADR-017: Hangfire para jobs agendados
ADR-018: Supabase Realtime para atualizações ao vivo no React
ADR-019: SwiftData para persistência local no mobile
ADR-020: Offline-first no mobile com outbox local
ADR-021: MCP (Model Context Protocol) para integração da Athena
ADR-022: API REST pública versionada (/api/v1)
ADR-023: Marca Desacoplada (namespaces genéricos, marca configurável)
ADR-024: Prompt caching da Anthropic para reduzir custo
ADR-025: Uso de Claude Opus 4.5 para Athena e Claude Sonnet 4.5 para
  classificação em lote

================================================================================
14. SEGURANÇA E COMPLIANCE
================================================================================

- Nunca commitar segredos. Use .env (no .gitignore) em dev e Azure Key Vault
  em produção.
- Senhas: Argon2id (via Konscious.Security.Cryptography.Argon2).
- JWT: expiração curta (15min) + refresh token com rotação (7 dias).
- HTTPS obrigatório em produção (TLS 1.3).
- Rate limiting em endpoints públicos.
- Idempotency-Key em POSTs críticos.
- Logs sem dados sensíveis: mascarar CPF, e-mail, telefone.
- Backup diário criptografado.
- Auditoria: toda ação sensível gera registro imutável; retenção mínima de
  5 anos.
- LGPD: direito ao esquecimento via endpoint de exclusão.
- OAB: revisão humana obrigatória; transparência com cliente final;
  contrato de não-treinamento com Anthropic.
- Anthropic: documentar por escrito que dados de API não são usados para
  treinar modelos (política padrão da Anthropic).

================================================================================
15. O QUE NUNCA FAZER
================================================================================

X Executar ação jurídica automaticamente (sem revisão humana)
X Compartilhar dados entre tenants (violar RLS)
X Usar dados de cliente para treinar modelos de IA
X Commitar segredos, tokens ou chaves de API
X Colocar a marca "Iustitia" em namespaces, solutions ou Bundle IDs
X Pular a Fase 0
X Usar async void, .Result ou .Wait() no backend
X Usar force unwrap (!) no Swift
X Usar any implícito no TypeScript
X Fazer commit direto em main ou develop (use feature branches)
X Ignorar testes (cobertura mínima: 80% backend, 70% mobile)
X Deixar console.log ou print em código de produção
X Adicionar dependência sem justificar no PR
X Quebrar a regra de dependência (Domain -> Infrastructure)
X Enviar dados de cliente para a Anthropic sem antes verificar o contrato
  de não-treinamento e a conformidade com LGPD/OAB

================================================================================
16. O QUE SEMPRE FAZER
================================================================================

OK Revisão humana obrigatória em todo fluxo de IA
OK Isolamento de dados por tenant (RLS)
OK Criptografia em repouso e em trânsito
OK Auditoria de ações sensíveis
OK Result pattern para erros de negócio
OK CancellationToken em métodos async
OK Testes antes de merge
OK ADRs para decisões arquiteturais
OK Documentação de API (OpenAPI/Swagger)
OK Marca Desacoplada (namespaces genéricos)
OK Conventional Commits
OK PRs com descrição e checklist
OK Atualizar README quando necessário
OK Rodar lint e type-check antes de commit
OK Logar todas as chamadas à Anthropic (input, output, tokens, custo) para
  auditoria
OK Usar prompt caching da Anthropic quando o prompt for repetitivo

================================================================================
17. PERGUNTAS
================================================================================

Antes de começar a Fase 0, o agente deve perguntar ao desenvolvedor:

1. Anthropic API direta ou via AWS Bedrock / Google Vertex AI?
   (Recomendação: Anthropic API direta para simplicidade; Bedrock/Vertex
   se houver exigência de compliance regional.)

2. Região Azure: Brazil South, East US 2 ou West Europe?
   (Recomendação: Brazil South se disponível; senão East US 2.)

3. Supabase: projeto hospedado (EUA) ou self-hosted na Azure?
   (Recomendação: hospedado para menor custo operacional; self-hosted se
   houver exigência de soberania de dados no Brasil.)

4. O desenvolvedor tem conta na Anthropic, Azure e Supabase?
   (Necessário para configurar acessos e chaves.)

5. Modelos Anthropic a usar:
   - Athena (assistente): Claude Opus 4.5 (mais recente)
   - Classificação em lote: Claude Sonnet 4.5 (ou mais recente)
   - Tarefas simples: Claude Haiku 4.5 (ou mais recente)
   (Confirmar disponibilidade e custo atual.)


================================================================================
18. CONTATO E SUPORTE
================================================================================

- Documentação: /docs
- ADRs: /docs/adr
- API: /docs/api (OpenAPI/Swagger)
- Compliance: /docs/compliance
- Navegação: /docs/navigation

Para dúvidas sobre o projeto, consulte o documento norteador completo em
/docs/README.md ou o próprio CLAUDE.md.

================================================================================
19. LEMBRETE FINAL
================================================================================

Este é um sistema jurídico. Erros podem causar perda de prazos e danos reais a
clientes. A revisão humana não é uma feature, é um requisito legal e ético.
Simplicidade de uso é o diferencial competitivo. Não complique.

A Anthropic é o provedor de IA escolhido. Use Claude com responsabilidade:
sempre com revisão humana, sempre com contrato de não-treinamento documentado,
sempre com auditoria de chamadas.

Bom trabalho. ⚖️

================================================================================
FIM DO ARQUIVO
================================================================================