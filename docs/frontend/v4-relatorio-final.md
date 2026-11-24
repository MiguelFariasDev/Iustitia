# Alinhamento ao protótipo Iustitia v4 — relatório final

Branch `feat/ui-v4-alinhamento` (20 commits, 130 arquivos no `web/`, +7.076/−830 linhas em relação à base comum com a `master`). Este relatório cumpre a seção 11 do prompt. O diário de decisões, tela a tela, está em [`v4-alinhamento.md`](./v4-alinhamento.md).

## 0. Leia primeiro: limites desta entrega

- **O arquivo do protótipo nunca esteve disponível.** Tudo foi feito a partir do texto do prompt (tokens, medidas, comportamentos). Onde o prompt era silencioso, escolhi o mais próximo e registrei. Uma comparação lado a lado com o protótipo ainda precisa ser feita por quem o tem.
- **Verificação em navegador real (Playwright)** foi feita em: Painel, Publicações, Revisão da IA, Criação de peças, Tarefas, Financeiro › Honorários, paleta de comandos e navegação mobile (Painel, Revisão, Atendimentos). **Não** foi feita em Agenda, Processos, Contatos, Alertas, Documentos e Indicadores, nem nos Atendimentos em desktop: nessas telas a verificação foi type-check, lint, testes e `next build`. Capturas de tela ficaram só no ambiente da sessão (não estão no repositório).
- **Backend escrito nesta sessão nunca foi compilado nem testado** (não há SDK .NET no ambiente): módulos CRM e Workflow, tipo de conta Solo/Firm, migrações SQL 014/015. As migrações do EF **não foram geradas**.
- Estado dos checks no último commit: type-check limpo; ESLint 0 erros (3 avisos que já existiam); **838 testes passando em 114 arquivos**; `next build` ok.

## 1. Tela → arquivos alterados

Caminhos relativos a `web/src/`. "Primitivos" são componentes novos reutilizados por várias telas.

| Tela / camada | Arquivos principais |
|---|---|
| **Fundação** (tokens, fonte, animações) | `app/globals.css`, `app/layout.tsx`, `config/theme.ts`, `components/ui/{sonner,skeleton,sheet}.tsx`, `lib/notify.ts` |
| **Primitivos** | `components/shared/{KpiCard,SegmentedControl,UrgencyBadge,ToneOutlineButton,Logo}.tsx`, `hooks/useNow.ts` |
| **Shell** (sidebar, topbar, breadcrumbs) | `components/layout/{Sidebar,SidebarNav,Topbar,Breadcrumbs,CreateButton,AccessGuard,AppLayout,MobileNav}.tsx`, `config/{navigation,routes}.ts`, `lib/auth/access.ts` |
| **Painel** (5 perfis) | `features/dashboard/**` (`persona.ts`, `workQueue.ts`, `time.ts`, `hooks.ts`, `demo.ts`, `components/{DashboardView,AdvogadoPanel,SocioPanel,SecretariaPanel,FinanceiroPanel,WorkQueue,DemoSection}.tsx`), `stores/uiStore.ts` |
| **Publicações** | `features/publicacoes/{view.ts,components/*}` |
| **Revisão da IA** | `features/revisao/**`, `app/(app)/publicacoes/revisao/page.tsx` |
| **Criação de peças** | `features/pecas/**`, `app/(app)/criacao-de-pecas/page.tsx` |
| **Tarefas** (kanban de equipe) | `features/tarefas/**`, `app/(app)/tarefas/page.tsx` |
| **Agenda** | `features/agenda/components/{AgendaView,MonthGrid,TimeGrid}.tsx` |
| **Atendimentos** | `features/atendimentos/components/{AtendimentosView,ConversaList,ChatPanel,ContextPanel,MessageBubble}.tsx` |
| **Processos** | `features/processos/components/ProcessosView.tsx` |
| **Contatos** | `features/contatos/components/ContatoList.tsx` |
| **Financeiro** | `features/financeiro/components/{FinanceiroTabs,FinanceiroResumo,LancamentosTable}.tsx`, `components/shared/DataTable.tsx` |
| **Documentos** | `features/documentos/components/{DocumentosView,DocumentoGrid,DocumentoCard}.tsx` |
| **Alertas** | `features/alertas/{tabs.ts,types.ts,components/{AlertaTabs,AlertaList,AlertasView}.tsx}` |
| **Indicadores** | `features/indicadores/components/IndicadoresView.tsx` |
| **Camadas globais** | gaveta de notificações: `components/layout/NotificationsDropdown.tsx`; Athena: `features/athena/**`; paleta: `components/layout/CommandPalette.tsx`; toast: `components/ui/sonner.tsx`, `lib/notify.ts` |
| **Mobile** | `components/layout/{BottomNav,AppLayout}.tsx`, `features/revisao/components/RevisaoView.tsx`, `features/atendimentos/components/AtendimentosView.tsx` |

Cada tela alterada tem testes novos ou atualizados ao lado do código.

## 2. Diferenças em relação ao protótipo, e por quê

| # | Diferença | Motivo |
|---|---|---|
| 1 | Semana da Agenda começa na **segunda** (protótipo: domingo) | Decisão do produto, para o Brasil |
| 2 | Processos: filtro tem **5** situações (inclui "Encerrados") | O backend tem 4 situações reais além de "Todos"; omitir uma esconderia processos |
| 3 | Alertas: aba **"Prazos" = alertas do tipo `Task`** | A API não tem tipo "prazo" |
| 4 | Alertas: aba "Publicações" inclui as já lidas | O nome da aba deve dizer o que ela contém; não lidas continuam em Importantes |
| 5 | Financeiro: número dos cartões em `text-2xl`, não nos 45px do KpiCard | "R$ 11.700,00" em 45px estoura a coluna com 4 cartões |
| 6 | Notificações: sem legenda "Sistema/IA"; alertas lidos **não** ficam a 35% | O alerta não traz origem; 35% reprova no contraste AA |
| 7 | Agenda: tipos fora do filtro esmaecem só o **ponto**, não o texto | Contraste AA |
| 8 | "Clientes" permanece no menu (no protótipo fica dentro de Contatos) | É tela real, com backend |
| 9 | Painel/Publicações/Revisão: sem "a IA sugeriu prazo X" afirmado | A API não expõe a sugestão por publicação; uso o flag `ai.classification` como indício e rotulo como demonstração |
| 10 | Prazos em dias úteis ignoram feriados (rotulado) | Não há calendário de feriados |
| 11 | Paleta de comandos mantém o botão X; animação é a do diálogo padrão (200 ms), não a `animate-iu-in-palette` | Toque no celular; trocar a animação exige mexer no diálogo compartilhado |
| 12 | Paleta não mostra resultados de processos/clientes/documentos | Não existe busca global no backend; resultado inventado levaria a um registro inexistente |
| 13 | Mobile: tabelas rolam na horizontal; não viraram lista de cartões; sem inbox própria; skeletons genéricos | Não feito (esforço); decisão do produto |
| 14 | Atendimentos: sem contador na aba "Aguardando" e sem indicador de não lidas | A API não devolve esses dados |
| 15 | Exportação PDF de Financeiro e de Peças indisponível | Não há gerador de PDF; a tela avisa em vez de baixar arquivo vazio |

## 3. Decisões para o dono do produto

1. **Controle de acesso (RBAC).** O prompt dá à Secretária e ao Financeiro telas que a API hoje responde com 403. A matriz do front (`lib/auth/access.ts`) espelha as policies reais. Decidir: mudar as policies da API ou aceitar a matriz atual?
2. **Nome do produto:** definir `NEXT_PUBLIC_APP_NAME=Iustitia` no ambiente de produção.
3. **Protótipo no repositório:** colocar o arquivo do protótipo em `docs/` para futuras comparações.
4. **Cadastro:** persistir ou remover tamanho da equipe, áreas de atuação e consentimentos (hoje coletados e não gravados).
5. **Agenda:** a política atual é `LawyerOrAbove`; a secretaria pode precisar agendar.
6. **Conversão Solo → Firm:** não existe fluxo; definir se haverá.
7. **Texto do aviso de Indicadores:** diz que prazos e tarefas "só existem nas próximas fases", o que já não vale para a Agenda. Reescrever ou calcular a partir da agenda real.
8. **Tabelas no celular:** vale transformar em cartões?
9. **Athena:** só em demonstração; decidir provedor de IA, retenção de conversas e escopo de dados que ela pode ler.
10. **Tarefas:** hoje só no navegador; decidir modelo de dados e API.

## 4. O que é demonstração e o que depende de backend

**Funciona com a API real (quando os mocks estão desligados):** Agenda, Processos (lista, quadro, importação, DataJud), Clientes, Publicações (lista, filtros, mudança de status), Alertas, Atendimentos, notificações.

**Demonstração (dados fictícios ou só no navegador), sinalizada na tela com "DEMO"/aviso:**

| Tela | Falta no backend |
|---|---|
| Painel — blocos de sócio, secretaria e financeiro; indicadores | Agregações por perfil; receita; produtividade |
| Revisão da IA | Sugestão da IA por publicação (prazo, tipo, confiança, fundamento); hoje calculada no front a partir do texto |
| Criação de peças | Geração por IA; hoje usa modelos-esqueleto com lacunas; exportação PDF/DOCX |
| Tarefas | Entidade, API e persistência (hoje em memória) |
| Financeiro | Módulo inteiro: lançamentos, vencimento calculado no servidor, fluxo de caixa |
| Documentos | Armazenamento real, validação de tipo/tamanho, vínculos |
| Contatos | Módulo CRM (escrito, **nunca compilado nem migrado**) |
| Indicadores | Consultas agregadas por período |
| Athena | Serviço de IA, histórico, leitura de dados; só "Prazos de hoje" usa dado real (agenda) |
| Atendimentos | Totais por status e não lidas |

Itens de demonstração aparecem **só com mocks ligados** quando a tela depende de IA (Revisão, Peças, Athena); sem mocks mostram "ainda não disponível" em vez de fingir.

## 5. Pendências técnicas antes de integrar

1. Rodar `dotnet build` e `dotnet test` (nada do backend desta sessão foi compilado).
2. Gerar migrações do EF: `dotnet ef migrations add AddTenantAccountType` e as iniciais de CRM e Workflow; depois aplicar `infra/supabase/migrations/014_calendar_contacts.sql` e `015_account_type_claim.sql`.
3. Aplicar o patch do `/health` (`bug1-health-inner-handler.patch`, na área temporária da sessão — pedir para regerar se sumiu).
4. Mesclar a `master` na branch (a `master` já recebeu os PRs #4 e #7) e resolver conflitos.
5. O filtro de cliente em Publicações chama `/clientes` real mesmo em modo demo (erro de conexão no console); não corrigido.
6. Rodar a verificação em navegador nas telas listadas na seção 0 como não verificadas.

## 6. Critérios de aceite (seção 10 do prompt) — situação

| Critério | Situação |
|---|---|
| Tokens, fonte e animações do v4 aplicados | Feito (fundação) |
| Shell: sidebar 236px, topbar, breadcrumbs | Feito |
| Todas as telas da seção 5 alinhadas | Feito, com as divergências da seção 2 |
| Camadas globais (paleta, notificações, Athena, toast) | Feito; Athena em demonstração |
| Mobile: barra de 3 abas e ações fixas | Feito; lista em cartões **não** feita |
| Sem rolagem horizontal da página no celular | Verificado em Painel, Revisão, Atendimentos, Agenda e Financeiro; não nas demais |
| Contraste AA | Respeitado nas decisões 6 e 7; **não** auditado com ferramenta (axe) tela a tela |
| `prefers-reduced-motion` respeitado | Feito nas regras globais; não testado por tela |
| Testes, lint, type-check, build | Passando (seção 0) |
