# Auditoria técnica, de segurança e de produto — Iustitia

**Data:** 2026-11-11 · **Base analisada:** `origin/master` no commit `bbcbbce`, que **já inclui** o PR #10 (frontend v4), o
PR #11 (módulo Drafting: fila, GPU sob demanda, RAG e acervo normativo) e os commits "fixed a lot of bugs in database/frontend"
(tarefas, etiquetas e migrações EF). O PR #12 (landing page) ainda não está no `master`.
Outras branches abertas (`feat/backend-mobile-gaps`, `feat/mobile-flutter-foundation` etc.) **não foram auditadas**.
*Nota de revisão:* uma primeira versão deste relatório foi escrita sobre um checkout anterior ao merge do PR #11 e foi
refeita sobre o `master` atual; os números abaixo são do `master` atual.

**Método e limites.** Só código, configuração, testes, migrações e documentação do repositório. Nada foi
executado (sem SDK .NET no ambiente de análise): afirmações sobre comportamento em runtime são leitura de código.
Onde não achei evidência, escrevo **AUSENTE** (busca sem ocorrências) ou **DESCONHECIDO** (não consegui verificar).
Documentos em `docs/requisitos/` são citados pelo README como "não versionados": não os li.

---

## 1. Resumo executivo

1. **O que existe e é sólido (master):** núcleo multi-tenant com RLS, JWT/Supabase, auditoria imutável, criptografia de
   campos com rotação documentada, idempotência, rate limit, outbox, jobs; publicações (captura DJEN → triagem → sugestão
   de IA → revisão humana); processos e clientes; agenda com lembretes; **tarefas com Kanban e etiquetas**; inbox de
   atendimento (WhatsApp, lado Core); módulo de geração de peças com RAG (mergeado, **desligado por padrão**); web
   (Next.js 16) e app Flutter parcial; bom volume de testes (1.391 métodos `[Fact]/[Theory]` .NET, 116 arquivos de teste
   web, 16 e2e).
2. **O que o produto ainda não é:** um "Astrea". Faltam, no backend, **gestão de documentos, fluxos/workflows, financeiro,
   portal do cliente, relatórios, funil comercial, assinatura eletrônica**. Os módulos `Documents`, `Reporting`,
   `Notifications`, `Athena` e `Identity` são **pastas vazias** (0 arquivos `.cs`); as telas Financeiro, Documentos e
   Indicadores existem no web só com dados de demonstração, e a **Athena do web é demonstração** (sem serviço de IA ligado).
3. **A IA jurídica local (Ollama/Qdrant/BGE-M3) está no `master`, mas desligada** (`Drafting:Enabled=false`): migração EF
   gerada em 10/11 (o que indica que o código compilou na máquina do autor), tela `PecasView` ligada à API. **Não sei se os
   353 testes do módulo foram executados**, e não há CI que os rode. **Jurema-7B não aparece em nenhum lugar do código**;
   o padrão é Qwen3 8B.
4. **Cinco bloqueios (P0) antes de qualquer uso real:**
   - **O projeto se declara "de portfólio, não comercializado"** (README, `docs/compliance/portfolio.md`, ADR-064) e é
     isso que legitima o uso do DataJud, cujo termo **proíbe uso comercial**. Um SaaS B2B contradiz o enquadramento.
   - **Não há CI que compile e teste o código.** Os workflows são só segurança (audit/Trivy) e backup; o módulo mais
     novo e maior (Drafting, 93 arquivos) entrou no `master` sem esse portão.
   - **Não há autorização por caso/cliente/documento:** o controle é por papel + tenant. Todo advogado do escritório
     enxerga tudo; o filtro por responsável é opcional na consulta, não uma regra.
   - **E-mail e push são stubs:** `EmailService` só escreve no log; `PushService` está vazio. Convites e lembretes de
     prazo por e-mail não chegam a ninguém. Para um produto cujo valor é "não perder prazo", é crítico.
   - **LGPD operacional ausente:** sem direitos do titular, retenção/exclusão, consentimento, política de privacidade,
     termos, DPO/DPIA; o checklist de `docs/compliance/lgpd.md` está inteiro em aberto.
5. **Decisão de topologia pendente:** o repositório inteiro (Terraform, Front Door, Container Apps, Key Vault, backups
   em Azure Blob) assume **Azure + Supabase em nuvem**. O plano descrito agora (execução local, Cloudflare Tunnel,
   GPU na Vast.ai) não tem infraestrutura, runbook nem configuração segura no master.

---

## 2. Inventário funcional

Legenda: **IMPL** implementado · **PARC** parcial · **AUS** ausente · **N/A** · **DESC** desconhecido.
"Web" = `web/src`; "demo" = a tela usa dados de demonstração (`mocks.ts`, `useMocks`).

### Cadastros e organização

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Escritório e configurações | IMPL | `Platform`: `CreateTenant`, `UpdateTenant`, `Suspend/Reactivate`, `Settings`; web `onboarding`, `configuracoes/escritorio`; `docs/compliance/auth-flow.md` | Fluxo não validado de ponta a ponta contra um Supabase real (`auth-flow.md`, "O que ainda não foi validado") |
| Usuários, papéis, permissões | PARC | `UserRole` (Owner, Partner, Lawyer, Intern, Admin, Financial); `Policies` (`OwnerOnly`, `PartnerOrAbove`, `LawyerOrAbove`, `AdminOnly`, `FinancialOnly`); convite/desativação/troca de papel | Permissão **granular** por cliente/caso/documento (ver L-03). 18 endpoints de Legal usam só `LawyerOrAbove`/`PartnerOrAbove` |
| Advogados, estagiários, equipe | PARC | `User.OABNumber`; papel `Intern` | Sem entidade de equipe/grupo; sem regra do que o estagiário pode ou não fazer |
| Clientes PF e PJ | IMPL | `Legal.Domain/Clientes/Cliente` (`TipoCliente`, `Documento`, `Contato`, `Endereco`, `NomeBusca`/`DocumentoBusca`), criptografia de campos + backfill job | Sem campos personalizados; sem "cliente ↔ casos" além do processo |
| Partes adversas e terceiros | PARC | `Processo.ParteContraria` é **uma string**; `CRM.Contact` com `ContactType` | Sem modelagem de partes por processo (autor, réu, terceiro, advogado da parte contrária) |
| Contatos e histórico | PARC | `CRM`: `Contact`, endpoints, 8 testes de domínio | Sem linha do tempo de interações por contato |
| Leads e potenciais casos | AUS | busca por "lead/funil" sem ocorrências no backend e no web | Entidade, funil, origem |
| Campos personalizáveis | AUS | sem ocorrências | — |
| Busca global | PARC | `001_fulltext.sql`; web `features/search` e paleta de comandos | Cobertura da busca por módulo não verificada |

### Captação e atendimento

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Funil, origem do lead, triagem, conflito de interesses | AUS | sem ocorrências (`conflito de interesse`, `funil`, `lead`) | Tudo |
| Proposta e contrato de honorários | AUS | backend: 0 ocorrências; web: só a tela demo de Financeiro | Tudo |
| Conversão lead → cliente/caso | AUS | — | Depende do funil e do conceito de Caso |
| Atendimento (inbox, handoff bot↔humano) | IMPL (Core) | `Atendimento.*`: `Atendimento`, `AtendimentoMensagem`, `BotApiKey`, `BotEndpoints`, `AtendimentosEndpoints`; ADR-067; 83 testes | O **Bot é outro repositório** (não verificável aqui). O endpoint de webhook do WhatsApp **não existe no Core** (`security-hardening.md`) |
| Histórico de atendimento | IMPL | `AtendimentoMensagem`, `AtendimentoAuditoria` | — |

### Casos e processos

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Caso jurídico (≠ processo) | AUS | só existe `Processo`; não há entidade `Caso` | Conceito de caso agregando processos, documentos, tarefas, financeiro |
| Processo judicial | IMPL | `Processo` (`CNJNumber`, `ClienteId`, `ResponsavelUserId`, `AreaDireito`, `StatusProcesso`, `DadosForo`, `Assunto`, `ValorCausa`, `DataDistribuicao`), soft delete, auditoria | — |
| Administrativo, arbitral, extrajudicial | AUS | `Processo` exige `CNJNumber` | Tipo de caso sem número CNJ |
| Número CNJ / OAB | PARC | `CNJNumber` com validação; `OABNumber` só no usuário | Sem OAB por processo/representação |
| Tribunal, vara, comarca, classe, assunto | PARC | `DadosForo`, `TribunalResolver`, `Assunto` | Classe processual não encontrada |
| Partes, advogados, terceiros | PARC | ver "Partes adversas" | — |
| Movimentações processuais | AUS | não há entidade; `DataJud` só preenche cadastro (`DataJudEndpoints`) | Entidade, captura, histórico |
| Timeline do caso | PARC | `audit_logs` por entidade; publicações ligadas ao processo | Linha do tempo unificada por processo |
| Status do caso | IMPL | `StatusProcesso`: Ativo, Suspenso, Encerrado, Arquivado | — |
| Vínculos (caso, documentos, tarefas, financeiro) | PARC | `Publication→Processo/Cliente`; `CalendarEvent→Processo` | Documentos, tarefas e financeiro sem vínculo |

### Prazos e agenda

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Agenda coletiva e individual | IMPL | `CalendarEvent` (`AssignedUserId`, `CalendarEventType`: Prazo, Audiencia, Reuniao, Tarefa), `CalendarEndpoints`, web `agenda` | — |
| Prazo processual | PARC | tipo `Prazo` no calendário; `DeadlineType` (Business/Calendar) na **sugestão** de IA; web `features/revisao/diasUteis.ts` soma dias úteis (CPC art. 224) **e avisa que não conhece feriados, recesso nem suspensão** | **Calculadora no backend** com feriados forenses, recesso e suspensões; hoje é só estimativa no navegador |
| Prazo automático a partir de publicação | PARC | `PublicationSuggestion` (dias, base legal, confiança); web `revisao` e `PublicacaoAcoes.tsx` abrem o evento **pré-preenchido** para o advogado confirmar | Criação no backend, idempotente, com a revisão humana registrada |
| Alertas de prazo | IMPL | `CalendarReminderJob` (1 dia e 1 hora), `Alerts` in-app (ADR-065) | Reforço por e-mail/push (ver L-04) |
| Notificações e-mail / push / sistema | PARC | sistema: `Alerts` + `SupabaseRealtimeNotifier`. **E-mail: `EmailService` só loga** ("placeholder"). **Push: `PushService` vazio**. Pasta `Integrations.Email` vazia | Provedor de e-mail transacional, FCM/APNs |
| Sincronização tarefas ↔ prazos | PARC | existem `WorkTask` (com `DueDate`) **e** `CalendarEventType.Tarefa`: dois conceitos de tarefa | Unificar ou ligar os dois |
| Feriados e recesso | AUS | sem ocorrências no código | Calendário forense por tribunal |
| Prazo por responsável | PARC | `AssignedUserId` | Visão de carga e redistribuição |

### Publicações

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Captura | IMPL | `Integrations.CNJ` (46 testes), `CnjCaptureJob`, `PublicationSource` | — |
| Monitoramento de processos | PARC | consulta DataJud para cadastro | Monitoramento automático de movimentos (e DataJud é vedado comercialmente) |
| Triagem e classificação | IMPL | `PublicationStatus` (PendingReview, Approved, Corrected, Rejected, Archived); `PublicationClassificationJob`; `ClassificationService` (Claude) | — |
| Urgência | PARC | `Confidence`, `RequiresManualReview`, `DeadlineBadge` no web | Campo explícito de urgência |
| Publicação → tarefa/prazo | PARC | ver Prazos | — |
| Alerta de nova movimentação | PARC | `PublicationCapturedEvent` → `AlertCreatedConsumer` | — |
| Histórico de tratamento, responsável, filtros | IMPL | `ReviewNotes`, `ResponsavelUserId`, `ListPublications` com specifications | — |

### Tarefas e fluxos

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Tarefas e Kanban | PARC | `Workflow.Domain/Tasks/WorkTask` (título, descrição, `Status` Todo/InProgress/InReview/Done, `DueDate`, `ProcessId`, `AssignedUserId`), `TaskAndTagEndpoints` (CRUD + `PATCH /status`), migração `AddTasksAndTags`, web `features/tarefas` (Kanban via API) | Prioridade, vínculo com cliente/caso, notificação ao atribuir. **Sem testes de Application/API do módulo** (só 14 testes de domínio em `Workflow.Domain.UnitTests`) |
| Etiquetas | IMPL | `Tag`, `TagAssignment`, `TagTarget`, web `features/tags` | — |
| Workflows e modelos de fluxo | AUS | o módulo `Workflow` tem calendário e tarefas; nada de fluxo/modelo | Tudo |
| Tempo gasto | AUS | sem ocorrências | — |
| Comentários e anexos | AUS | — | Depende de Documentos |
| Indicadores de carga | PARC | web `indicadores` (demo) | Backend (`Reporting` vazio) |

### Documentos e modelos

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Upload, organização, versionamento, permissões, pré-visualização, busca, retenção | PARC | **Existe upload só para o acervo da IA**: `Drafting.Api/DocumentosEndpoints` (PDF com texto, DOCX, TXT; limite de tamanho; SHA-256 contra duplicata; visibilidade `escritorio`/`privado`; extração e indexação em fila). O módulo `Documents` tem **0 arquivos** e a tela `documentos` usa mocks | Gestão documental de verdade (pastas, versões, permissões por caso, pré-visualização, retenção); **scan de malware**; vínculo com caso/processo |
| Modelos de peças, variáveis, exportação PDF/DOCX | AUS | — | — |
| Geração de minutas | PARC | `Drafting.*` no `master`, `Drafting:Enabled=false`; web `PecasView` + `features/pecas/api.ts` ligados à API; `features/revisao` para publicações | Rodar os testes, validar com peças reais, tela de revisão da minuta com revisor registrado, exportação DOCX/PDF |
| Assinatura eletrônica | AUS | sem ocorrências | — |

### IA jurídica e RAG

| Item | Status | Evidência | O que falta |
|---|---|---|---|
| Classificação de publicações | IMPL | `ClassificationService`, `AiPromptExecutor`, prompts versionados, `tests/RegressionTests/Prompts` | — |
| Sugestão de prazos | IMPL | `DeadlineSuggestionService`, `PublicationSuggestion` | Ver "prazo determinístico" |
| Registro de modelo, prompt, custo | PARC | `publication_suggestions` (`AiModel`, `PromptVersion`, `RawResponse`), `ai_usage_logs` (015), `AiUsageRecorder` | **Limite/orçamento por escritório não encontrado** (só registro) |
| Chat jurídico (Athena) | AUS | módulo `Athena` vazio; só modelos de mensagem/tools em `Integrations.Anthropic` | — |
| RAG local (Ollama, Qdrant, BGE-M3) | PARC | `Drafting.*`: `OllamaService`, `QdrantService`, `NormaQdrantStore`, `HybridSearchService`, `NormaChunker`…; migração `InitialCreate` do Drafting gerada | Testes sem CI; sem evidência de execução contra Ollama/Qdrant reais; ver §6 |
| Modelo Jurema-7B | AUS | zero ocorrências; padrão do PR é `qwen3:8b` | Confirmar disponibilidade em formato Ollama/GGUF, licença e desempenho em português jurídico |
| Pesquisa e comparação de jurisprudência; resumo de documentos/movimentações; extração estruturada | AUS | — | — |
| Fila assíncrona, auto start/stop da GPU, streaming NDJSON, auditoria de geração | PARC | `GeracaoPecaFila`, `GpuOrchestrator`, `NdjsonStreamResult`, `GenerationAudit`, `GpuInstanceLog` | Idem; teste real com a Vast.ai |
| Aviso de revisão humana | PARC | revisão obrigatória em publicações (`PublicationStatus`); minuta: aviso + `ExportacaoBloqueada` | Tela de revisão da minuta com registro do revisor; endpoint de exportação |
| Athena (assistente) | PARC | web `features/athena`: respostas de **demonstração**, sem IA ligada (a própria tela avisa) | Backend; decidir entre Claude e modelo local |

### Financeiro, portal, comunicação, relatórios

| Módulo | Status | Evidência |
|---|---|---|
| Financeiro e honorários (todos os itens) | AUS (backend) | 0 ocorrências de `boleto`/`pix`/`honorário` no backend; web `financeiro` é demo |
| Portal do cliente (todos os itens) | AUS | 0 ocorrências |
| Central de notificações | PARC | in-app e Realtime sim; e-mail/push stubs |
| WhatsApp | PARC | inbox no Core; bot em outro repositório; sem webhook no Core |
| Modelos de mensagem, envio em massa, consentimento | AUS | — |
| Relatórios, dashboard | PARC | web `painel` e `counts`; `Reporting`: **0 arquivos**; `indicadores` demo |
| Exportação PDF/Excel | AUS | sem geradores no backend |

### Segurança, LGPD e integrações

| Item | Status | Evidência |
|---|---|---|
| Autenticação | IMPL | Supabase Auth + JWT (`JwtValidationSetup`), `auth-flow.md` |
| MFA | AUS | `security.md`: "habilitado no Supabase, mas ainda não exigido" |
| Autorização por papel | IMPL | `Policies` |
| Autorização granular | AUS | ver L-03 |
| Multi-tenant | IMPL | RLS (`002_rls.sql`, `011_publications_rls`, `012…`), `TenantContextMiddleware`, `scripts/validate-rls.sql`, testes de integração |
| Auditoria | IMPL | `AuditLog`, `014_audit_immutability.sql`, arquivamento em Blob (WORM e lifecycle **pendentes de infra**) |
| Criptografia de campos | IMPL | `FieldEncryption` + blind index, `ClienteEncryptionBackfillJob`, `docs/runbooks/field-encryption.md` (com rotação). Chaves no Key Vault: pendente de infra |
| Backup | PARC | workflows diários `backup.yml` e `backup-storage.yml` (pg_dump e Storage → Azure Blob). **Teste de restauração não encontrado** |
| Retenção, exclusão, direitos do titular, consentimento | AUS | `lgpd.md`: todos os itens em aberto |
| Política de privacidade e termos de uso | AUS | sem páginas no web e sem texto no repositório |
| Resposta a incidentes | AUS | único runbook: `field-encryption.md` |
| Rate limit, CORS, validação de entrada | IMPL | `RateLimitingConfiguration` (login 5/5 min, escrita 60/min, leitura 300/min, bot 1200/min), CORS validado na subida, FluentValidation |
| Rate limit distribuído | AUS | `security-hardening.md` |
| Prompt injection, SSRF, scan de upload | PARC | injection: só no Drafting (`PromptInjectionGuard`); SSRF e scan de malware: sem ocorrências |
| E-mail, calendário (ICS/Google), assinatura, webhooks/API pública, contabilidade | AUS | sem ocorrências (API keys existem só para o bot) |
| Tribunais | PARC | DJEN (captura) e DataJud (consulta, **uso comercial vedado**) |
| Object storage | PARC | Azure Blob para auditoria e backup; documentos: nada |
| Mobile | PARC | Flutter (`mobile/lib/features`: auth, agenda, clientes, dashboard, perfil, processos); 13 testes; sem publicações, atendimento ou documentos. `mobile-ios/` (Swift) é resto do ADR-011, já substituído pelo ADR-067 |
| Web responsivo | IMPL | Next.js 16, protótipo v4 e landing; 631 testes de unidade |

---

## 3. Matriz de lacunas (priorizada)

Esforço: B baixo (≤ 1 semana) · M médio (1–4 semanas) · A alto (> 1 mês).

| ID | Lacuna | Módulo | Status hoje | Prio | Esforço | Risco / impacto | Critério de aceite | Testes |
|---|---|---|---|---|---|---|---|---|
| L-01 | Enquadramento "portfólio" × SaaS comercial; DataJud vedado | Legal / produto | contradição documentada (README, `portfolio.md`, ADR-064) | **P0** | B (decisão) + M (remoção) | Violação de termo de uso do CNJ; dano reputacional e jurídico | Decisão escrita; em modo comercial a integração DataJud fica desligada por feature flag e a UI mostra preenchimento manual | Teste de arquitetura: nenhum caminho comercial referencia `DataJud` com a flag desligada |
| L-02 | CI sem build/test/lint | Plataforma | só `security.yml`, `backup*.yml` | **P0** | B | Regressão silenciosa; o Drafting entrou sem build nem testes automáticos | PR só mergeia com `dotnet build/test`, `vitest`, `eslint`, `tsc` verdes | O próprio pipeline |
| L-03 | Autorização por caso/cliente/documento (IDOR intra-tenant) | Legal, Plataforma | só papel + tenant; filtro por responsável opcional | **P0** | A | Estagiário ou advogado vê processos que não deveria; sigilo profissional | Regra declarativa (responsável, equipe, "restrito"); 404 para o resto; vale em listagem, detalhe e busca | Testes de integração: usuário A não lê processo/cliente de B em todos os endpoints (matriz papel × recurso) |
| L-04 | E-mail e push são stubs | Notificações | `EmailService` loga; `PushService` vazio | **P0** | M | Convite e lembrete de prazo por e-mail não são entregues | Convite chega; lembrete de prazo por e-mail com reenvio e registro de entrega | Teste com SMTP/API simulada; teste de falha e retry |
| L-05 | LGPD operacional | Plataforma | checklist `lgpd.md` todo aberto | **P0** | A | Multa e perda de confiança; exigência de cliente B2B | Política e termos publicados; endpoint de exportação e de exclusão/anonimização; registro de consentimento; DPO nomeado | Teste: exportar e excluir um titular remove/anonimiza PII em todas as tabelas e no Qdrant |
| L-06 | Módulo Drafting mergeado sem portão de qualidade | Drafting | migração gerada (compilou localmente); **353 testes (incl. Testcontainers) sem evidência de execução**; desligado por padrão | **P0** (antes de ligar `Drafting:Enabled`) | M | Fila, GPU e RAG não exercitados de ponta a ponta contra Ollama/Qdrant/Vast.ai reais | Suíte do módulo verde em CI; teste manual com Ollama local; checklist de `drafting.md` cumprido | Fila sob concorrência em Postgres real; uma minuta com GPU fria e quente |
| L-07 | Topologia de produção indefinida (Azure × local + Tunnel) | Infra | Terraform Azure; Tunnel só na branch `feature/forwarded-headers-tunnel`, **em conflito** com `AddAdvocaciaForwardedHeaders` do master (que **confia em qualquer proxy**) | **P0** | M | Spoof de `X-Forwarded-For` burla rate limit; ingress mal definido | Uma topologia escolhida, documentada e testada; proxy confiável restrito | Teste de integração `ForwardedHeaders` (já existe na branch) |
| L-08 | Dados de caso na GPU alugada (terceiro) e na Anthropic (EUA) | IA / LGPD | Drafting: HTTPS e chave; Anthropic: contrato de não-treinamento **não confirmado** (`contrato-nao-treinamento-anthropic.md`) | **P0** | M | Violação de sigilo; transferência internacional sem base | Pseudonimização antes do prompt; contrato/DPA anexado; oferta Vast.ai de datacenter; aceite do escritório registrado | Teste: o prompt enviado não contém CPF/CNPJ/nome do cliente; auditoria do que saiu |
| L-09 | MFA não exigido | Identity | habilitado no Supabase, sem política | P1 | B | Conta de sócio comprometida expõe tudo | MFA obrigatório para Owner/Partner/Admin | Teste de claim `amr`/AAL no pipeline de autorização |
| L-10 | Gestão documental (além do acervo da IA) | Documents | módulo vazio; só o upload do acervo da IA (`DocumentosEndpoints`) | P1 | A | Núcleo do produto; hoje o documento do caso não tem onde morar | Pastas por caso, versões, permissões herdadas do caso, pré-visualização, busca, retenção; storage privado | Upload malicioso rejeitado; IDOR; versão preservada |
| L-11 | Scan de malware no upload | Documents | AUS | P1 | M | Vetor de ataque via anexos | Arquivo só fica "disponível" depois do scan | EICAR em teste de integração |
| L-12 | Completar tarefas e criar fluxos | Workflow | `WorkTask` + etiquetas + Kanban existem; sem prioridade, comentários, anexos, tempo, modelos de fluxo; sem testes de Application/API | P1 | M | Kanban sem comentário nem aviso de prazo não substitui a rotina | Prioridade e comentários; notificação ao atribuir; 1 modelo de fluxo; testes de handlers e de autorização | Handlers de tarefas e etiquetas; e2e do quadro |
| L-13 | Calculadora de prazo no backend (feriado, recesso, suspensão) | Legal | só estimativa de dias úteis no navegador, com aviso | P1 | M | Prazo errado = responsabilidade do advogado | Calendário forense por tribunal; contagem em dias úteis testada com casos reais | Tabela de casos-ouro revisados por advogado |
| L-14 | Prazo automático a partir da publicação (no backend) | Legal/Workflow | só diálogo pré-preenchido | P1 | M | Retrabalho; esquecimento | Aprovar publicação gera evento com origem e revisão registradas | Integração: aprovar → evento criado, idempotente |
| L-15 | Financeiro e honorários | Novo | AUS | P1 | A | Principal lacuna comercial | Honorários (hora, fixo, êxito, mensal), contas a receber, cobrança PIX/boleto via provedor | Testes de domínio de rateio; conciliação |
| L-16 | Caso como entidade e partes por processo | Legal | AUS | P1 | A | Sem agregação, tudo fica solto | `Caso` ↔ N processos, partes, vínculos | Migração sem perda |
| L-17 | Funil comercial, conflito de interesses, proposta | CRM | AUS | P2 | A | Captação manual | Lead → proposta → contrato → caso; checagem de conflito por documento/nome | — |
| L-18 | Portal do cliente | Novo | AUS | P2 | A | Diferencial do mercado | Login exclusivo, só casos autorizados, mensagens | Matriz de permissão do cliente |
| L-19 | Relatórios e exportação | Reporting | vazio; telas demo | P2 | M | Gestão sem números reais | Painel real de prazos, carga, publicações; CSV/XLSX | Consultas com índices; teste de volume |
| L-20 | Limite de custo de IA por usuário e escritório | IA | só registro de uso | P1 | B | Fatura imprevisível | Orçamento mensal; 402/429 com mensagem clara | Teste de estouro do limite |
| L-21 | Observabilidade da IA e da fila | IA/Infra | métricas só em `GET /pecas/admin/metricas` (Drafting); Prometheus no Core | P2 | M | Falha silenciosa na fila | Métricas, alertas e painel: idade da fila, falhas, custo, tempo de partida da GPU | Teste de alerta |
| L-22 | Teste de restauração de backup | Infra | pg_dump diário sem restauração testada | P1 | B | Backup que não restaura | Restauração mensal automatizada com checagem | — |
| L-23 | Testes de carga | Qualidade | nenhum encontrado | P2 | M | Gargalo só aparece em produção | Cenário de 50 usuários por escritório e 20 escritórios | k6/NBomber no pipeline noturno |
| L-24 | Resposta a incidentes | Governança | AUS | P1 | B | Prazo de 3 dias úteis da ANPD sem processo | Runbook, contatos, modelo de comunicação | Simulado semestral |
| L-25 | Assinatura eletrônica, calendário externo, webhooks/API pública | Integrações | AUS | P3 | A | — | — | — |
| L-26 | Documentação divergente | Docs | README fala em "React + Vite" (o web é Next.js 16); `security-hardening.md` fala em app Swift (ADR-067 trocou por Flutter); checklist de LGPD aberto apesar de backup existir | P2 | B | Decisões erradas por leitura de doc velha | Docs reconciliadas; ADR índice | Link-check |
| L-27 | Telas de demonstração sem sinalização uniforme | Web | `useMocks` em 19 arquivos; Athena, Documentos, Indicadores e Financeiro são demo; a landing (PR #12) marca Documentos/Indicadores/Financeiro como "Em evolução" mas **não cita a Athena** | P1 | B | Expectativa falsa ao usuário | `DemoNotice` em toda tela demo; landing coerente com a navegação | Teste do conteúdo da landing contra `navigation.ts` |

---

## 4. Riscos críticos (resumo)

1. **Legal/produto — L-01.** Enquanto o enquadramento for "portfólio", não se vende. Se for vender, o DataJud sai e a
   alternativa precisa existir antes.
2. **Qualidade — L-02/L-06.** Nada garante, a cada merge, que o repositório compila e que os testes passam. O Drafting
   (93 arquivos, 353 testes) entrou sem esse portão.
3. **Sigilo — L-03/L-08.** Duas fugas possíveis: entre colegas do mesmo escritório (sem escopo por caso) e para
   terceiros (GPU alugada e API de IA nos EUA) sem pseudonimização nem contrato.
4. **Confiabilidade do prazo — L-04/L-13/L-14.** O valor central do produto depende de aviso entregue e de data certa;
   hoje o aviso por e-mail não existe e a data é sugestão de IA sem calculadora.
5. **Conformidade — L-05/L-24.** Sem direitos do titular, retenção, consentimento, política e processo de incidente.

---

## 5. Riscos de segurança e LGPD

| Tema | Situação | Evidência | Ação |
|---|---|---|---|
| Autenticação/MFA | JWT ok; MFA não exigido | `security.md` | L-09 |
| Autorização granular / IDOR | só papel+tenant | filtros por responsável opcionais (`ProcessoRepository:82`, `PublicationRepository:160`) | L-03 |
| Isolamento multi-tenant | forte no Postgres (RLS + testes) | `002_rls.sql`, `validate-rls.sql` | No Qdrant o isolamento é só filtro por `tenantId` (`QdrantService`): testar com tenant forjado |
| Rate limit | por IP/usuário, em memória | `RateLimitingConfiguration`; "distribuído" pendente | Redis quando houver >1 instância |
| Proxy confiável | master confia em qualquer proxy | `SecurityConfiguration.AddAdvocaciaForwardedHeaders` (limpa `KnownProxies`) | L-07 |
| Tokens no navegador | cookie HttpOnly exige BFF | `security-hardening.md`, ADR-070 | CSP com nonce já existe; BFF é decisão |
| Prompt injection | só no Drafting (filtro, não garantia) | `PromptInjectionGuard` | Manter + teste adversarial |
| SSRF | sem ocorrências no Core; o Drafting restringe o endpoint do Ollama (`TransportGuard`) e `git`/storage vêm só de configuração | — | Revisar qualquer URL vinda do usuário |
| Upload malicioso | upload do acervo da IA valida extensão e tamanho e recusa PDF sem texto; **sem scan de malware** | `DocumentIngestionService`, `DocumentTextExtractor` | L-10/L-11 |
| Criptografia | campos sensíveis cifrados; TLS depende da infra | runbook de rotação | Chaves no cofre; teste de rotação |
| Backup | diário; restauração não testada | `backup.yml` | L-22 |
| Retenção e exclusão | AUS | `lgpd.md` | L-05 |
| Transferência internacional | Anthropic (EUA), Supabase (região a confirmar), Vast.ai (host de terceiro) | contrato anexado: não | L-08 |
| Auditoria de acesso a documento sensível | auditoria genérica; leitura de documentos não existe | `AuditLog` | Registrar leitura quando houver documentos |
| Segredos | Key Vault no Terraform; `Anthropic:ApiKey`, `VASTAI__APIKEY` por ambiente | `scripts/keyvault-set-secrets.sh` | Em execução local, usar `.env` fora do Git e rotação |

## 6. Riscos jurídicos e éticos da IA

1. **Responsabilidade final é do advogado** (Código de Ética; Provimento CFOAB 271/2025 citado em `oab.md`). O desenho
   com revisão humana está certo; falta a **tela de revisão** da minuta e do registro de quem revisou (hoje só há a flag
   `ExportacaoBloqueada`; a tela `PecasView` existe, mas sem registro de revisor).
2. **Alucinação de citação.** Drafting: prompt de 15 regras, contexto recuperado, validação pós-geração e trava. É redução de
   risco, **não garantia**. Um modelo de 7–8B erra com frequência; precisa de avaliação com peças reais antes de liberar.
3. **Jurema-7B.** Não há referência no código. Validar: existe versão utilizável no Ollama? licença permite uso
   comercial? desempenho em peças? Enquanto isso, o modelo configurável (Qwen3 8B) está correto como alternativa.
4. **Sigilo profissional × GPU de terceiros.** Fatos do caso vão no prompt. Mitigar: pseudonimizar nomes, CPF/CNPJ, endereços
   e valores antes do envio e reidentificar depois; escolher oferta de datacenter; registrar aceite; contrato.
5. **Transparência ao cliente final** (Recomendação OAB 001/2024): item aberto no checklist de `oab.md`.
6. **Conteúdo do acervo.** `ctresb/leis` se declara informativo, derivado de conversão automática/OCR, e não substitui o
   Diário Oficial. Mantido nos avisos; cada citação deve levar o link da fonte.
7. **Prazos sugeridos por IA** (`DeadlineSuggestionService`) são sugestão; a decisão deve vir de regra determinística
   (L-13) e confirmação humana.

## 7. Débitos técnicos

- Módulos-fantasma (`Documents`, `Reporting`, `Notifications`, `Athena`, `Identity`, `Api`): ou implementar ou remover do
  README/solução, para não prometer o que não existe.
- `Workflow` ainda sem fluxos (só calendário, tarefas e etiquetas).
- `EmailService`/`PushService` como stubs registrados na DI, o que dá a falsa impressão de funcionamento.
- 19 arquivos web com `useMocks`: risco de enviar tela demo à produção sem sinalização (existe `DemoNotice`).
- Documentação conflitante (L-26).
- Duas implementações concorrentes de `ForwardedHeaders` (master × branch do tunnel).
- Terraform/Azure sem uso se o alvo for execução local.
- Drafting: `Enabled=false`; testes sem evidência de execução; nomes de pasta do acervo dependentes de caixa (`municipais/CE/fortaleza`).
- Dois conceitos de tarefa (`WorkTask` e `CalendarEventType.Tarefa`).
- Sem teste de carga, sem teste de restauração, sem CI de build.

## 8. Plano de priorização

**P0 (bloqueiam qualquer uso com dados reais):** L-01, L-02, L-03, L-04, L-05, L-06, L-07, L-08.
**P1 (MVP comercial):** L-09 a L-16, L-20, L-22, L-24, L-27.
**P2:** L-17, L-18, L-19, L-21, L-23, L-26. **P3:** L-25.

Ordem recomendada dentro do P0: **L-02 → L-06 → L-07 → L-01 → L-03 → L-04 → L-08 → L-05.** Primeiro o CI (barato, desbloqueia o
resto), depois validar o Drafting já mergeado, depois decidir a topologia e o enquadramento (decisões de produto), e só então as correções
que dependem delas.

## 9. Roadmap por fases (ajustado ao que existe)

| Fase | Entrega | Já existe | Falta |
|---|---|---|---|
| 1 — Fundação | CI, escopo por caso, MFA, e-mail real, política/termos, restauração de backup, topologia | RLS, auditoria, criptografia, backup | L-02, L-03, L-04, L-05 (mínimo), L-07, L-09, L-22 |
| 2 — Operação jurídica | calculadora de prazo, prazo automático, comentários e prioridade em tarefas, push, fluxos | agenda, lembretes, publicações, alertas, **tarefas + Kanban + etiquetas** | L-12, L-13, L-14 e push |
| 3 — Documentos | módulo, upload seguro, versões, busca | — | L-10, L-11 |
| 4 — RAG jurídico | rodar o módulo já mergeado; ingestão do acervo; avaliação de qualidade | escrito e mergeado, desligado, sem validação de ponta a ponta | L-06; medir chunks e custo de GPU; reranker/BM25 reais só se a avaliação exigir |
| 5 — IA de redação | Jurema-7B (ou Qwen3), tela de revisão, pseudonimização, limite de custo | fila, GPU sob demanda, streaming, validação de citações | L-08, L-20, UI de revisão, exportação DOCX/PDF |
| 6 — Financeiro e portal | honorários, cobrança, portal, relatórios | telas demo | L-15, L-18, L-19 |
| 7 — Escala e conformidade | carga, observabilidade da IA, incidentes, integrações, mobile completo | Prometheus/OTel no Core; app Flutter parcial | L-21, L-23, L-24, L-25 |

Observação: **inverti a ordem do prompt** (RAG antes de documentos de produto não faz sentido sem o módulo de documentos
alimentando o acervo do escritório, nem sem a fundação de segurança). A IA de redação (fases 4–5) é a parte mais
adiantada em código, mas a menos pronta para ser usada.

## 10. Critérios de aceite do MVP comercial

1. `master` com CI verde (build, testes .NET, vitest, eslint, tsc, Trivy).
2. Usuário A não acessa dado de B dentro e entre escritórios, provado por teste automatizado em todos os endpoints.
3. Convite e lembrete de prazo chegam por e-mail; falhas geram retry e alerta.
4. Prazo calculado com feriados e recesso, validado contra casos reais pela advogada responsável.
5. Documento enviado passa por scan, é versionado e só é lido por quem tem permissão.
6. Minuta gerada tem todas as citações classificadas, fica bloqueada para exportação até a revisão e registra quem revisou.
7. Política de privacidade, termos e aviso de IA publicados; exportação e exclusão de dados de titular funcionando.
8. Backup restaurado com sucesso ao menos uma vez, com o procedimento documentado.
9. Decisão escrita sobre DataJud, hospedagem e transferência internacional.

## 11. Métricas de sucesso

- **Prazo:** % de prazos com lembrete entregue; nenhum prazo perdido por falha do sistema; tempo entre publicação capturada e prazo
  confirmado.
- **IA:** % de citações `Validada`; % de minutas com citação `PossivelAlucinacao`; tempo até a primeira palavra (com GPU fria e quente);
  custo de GPU por minuta; horas de GPU ligada por dia útil; taxa de aceitação da sugestão de classificação.
- **Operação:** disponibilidade da API; p95 de listagens; idade da fila de geração; falhas por 1.000 jobs.
- **Segurança:** testes de autorização verdes; vulnerabilidades críticas abertas (alvo zero); tempo de restauração (RTO) e perda
  tolerada (RPO) medidos.
- **Produto:** escritórios ativos; tempo até o primeiro prazo cadastrado; uso semanal de publicações e agenda.

## 12. Perguntas para o dono do produto

1. **Vai comercializar?** Se sim, aceita remover o DataJud do fluxo e com qual alternativa de preenchimento de processo?
2. **Onde roda em produção:** Azure (como o Terraform) ou execução própria com Cloudflare Tunnel? O banco continua no Supabase
   (nuvem) ou migra para PostgreSQL próprio?
3. **Quem é o primeiro cliente?** Um escritório piloto muda a prioridade entre documentos, tarefas e financeiro.
4. **Aceita enviar fatos de caso a uma GPU de terceiros?** Com pseudonimização? Só ofertas de datacenter?
5. **A Anthropic continua?** O contrato de não-treinamento foi confirmado? Ou toda a IA vai para o modelo local?
6. **Jurema-7B é requisito** ou o Qwen3 8B serve para começar? Quem avalia a qualidade das minutas?
7. **MFA obrigatório** para quais papéis? **Estagiário** pode ver o quê?
8. **O que é "caso"** para o escritório: agrupamento de processos de um cliente, ou pasta de qualquer matéria (inclusive extrajudicial)?
9. **Financeiro:** qual provedor de cobrança (PIX/boleto) e qual emissão fiscal? Há contador integrado?
10. **Quem é o DPO/encarregado** e quem responde a incidentes?
11. **Mobile:** o que precisa estar no app além de agenda, processos e clientes?

## 13. Recomendações imediatas (próximas duas semanas)

1. Criar o workflow de CI (build + testes .NET, vitest, eslint, tsc) e tornar obrigatório nos PRs. **Esforço baixo, destrava tudo.**
2. Rodar a suíte inteira (principalmente os 353 testes do Drafting) e fazer um teste manual com Ollama e Qdrant locais **antes** de ligar `Drafting:Enabled`.
3. Escolher a topologia (Azure ou local + Tunnel) e reconciliar `ForwardedHeaders`: manter a versão restrita da branch do tunnel
   e remover a que confia em qualquer proxy.
4. Decidir o enquadramento (portfólio ou comercial) e, se comercial, planejar a remoção do DataJud.
5. Implementar e-mail transacional real (convite e lembrete de prazo) e remover o stub da DI.
6. Desenhar a regra de acesso por caso/cliente e cobrir com teste de matriz antes de construir Documentos.
7. Pedir e anexar o contrato/DPA da Anthropic ou decidir descontinuar; definir pseudonimização para a IA local.
8. Na landing (PR #12), citar a Athena como demonstração ou omiti-la; rever o selo "Beta" da criação de peças enquanto o módulo estiver desligado.
9. Reconciliar a documentação (README, security-hardening, lgpd).
10. Rodar uma restauração de backup e registrar o resultado.

---

### Anexo — como esta auditoria foi feita

Buscas por palavra-chave (backend, web, mobile, docs), leitura de entidades de domínio, endpoints, migrações SQL, workflows de CI,
configurações de host, documentos de conformidade (`docs/compliance/*`), contagem de testes por projeto e conferência do `master` após o merge do PR #11.
Contagens (métodos `[Fact]/[Theory]`, sem expandir linhas de `Theory`): 1.391 em .NET — arquitetura 63, integração 267, unidade ~1.060, dos quais 353 só do Drafting;
116 arquivos de teste web, 16 e2e, 13 arquivos de teste do app Flutter.
**Não** foram lidos: `docs/requisitos/*` (não versionados), código do Bot de WhatsApp (outro repositório), branches não listadas.
