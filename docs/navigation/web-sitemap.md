# Sitemap Web (placeholder)

> Conteúdo completo (sitemap, rotas, fluxos e wireframes) já existe em
> `docs/requisitos/06-navegacao-web.md`. Este arquivo é o placeholder
> oficial de `/docs/navigation` pedido na estrutura de documentação do
> `claude.md` — deve ser promovido a documento definitivo (ou apenas
> referenciar o de requisitos) conforme as rotas forem implementadas de
> verdade a partir da Fase 2.

TODO: manter este documento sincronizado com as rotas React realmente
implementadas (`web/src/app`), não apenas com o planejado.

## Telas de autenticação — backend pronto na Etapa 0.3

As rotas abaixo (já mapeadas em `docs/requisitos/06-navegacao-web.md`,
seção 6.1) agora têm o endpoint de backend correspondente implementado —
ver `docs/compliance/auth-flow.md` para o fluxo completo. O frontend React
ainda não existe (Fase 2); esta seção só amarra rota planejada ↔ endpoint
já disponível, para quando a tela for construída.

| Rota (React, planejada) | Endpoint (Platform.Api, implementado) | Observação |
|---|---|---|
| `/cadastro` *(não estava no sitemap original — adicionar)* | `POST /api/v1/tenants` | Cadastro self-service de escritório; cria Tenant + usuário Owner |
| `/login` | `POST /api/v1/auth/login` | — |
| `/login` (token expirado) | `POST /api/v1/auth/refresh` | Renovação silenciosa de sessão |
| *(logout, sem tela própria)* | `POST /api/v1/auth/logout` | Disparado pela ação "Sair" no app shell |
| `/convite/:token` → `/onboarding` | `POST /api/v1/users/accept-invite` | `:token` é o link enviado pelo Supabase Auth (`InviteUserAsync`); o SDK client-side troca o token por uma sessão provisória antes de chamar este endpoint |
| `/configuracoes/usuarios` (convidar usuário) | `POST /api/v1/users/invite` | Requer papel Partner ou superior |
| `/esqueci-senha` / `/redefinir-senha` | *(delegado ao SDK do Supabase Auth diretamente do frontend — sem endpoint próprio no backend)* | Fora do escopo desta etapa |

`/configuracoes/auditoria` (listagem/detalhe de audit log) também já tem
backend pronto: `GET /api/v1/audit` e `GET /api/v1/audit/{id}` (requer
Partner ou superior).

## Telas de apoio com dados de demonstração (Fase 2, frontend adicional)

Telas implementadas em `web/src/app/(app)`, **sem backend correspondente**: usam repositório em
memória e exibem aviso permanente de demonstração. Decisão e checklist de migração em
[ADR-066](../adr/066-telas-de-apoio-com-dados-de-demonstracao.md).

| Rota | Tela | Backend | Observação |
|---|---|---|---|
| `/contatos` | Listagem (busca, filtro por tipo, ordenação, paginação) | pendente | Partes contrárias, testemunhas, fornecedores e outros — não são clientes |
| `/contatos/novo` | Cadastro | pendente | CPF/CNPJ com máscara automática |
| `/contatos/[id]/editar` | Edição | pendente | — |
| `/documentos` | Central de arquivos em grid, upload, download, exclusão | pendente (`Documents.*` vazio) | Arquivos enviados só vivem na memória da aba |
| `/indicadores` | KPIs, gráficos (área, status, publicações) e produtividade | pendente (módulo Reporting) | Prazos e tarefas só existem a partir das Fases 3 e 4 |
| `/financeiro` | Resumo, abas (honorários, a pagar, a receber, fluxo de caixa), novo lançamento, exportar CSV | pendente (Fase 4/8) | Exportar em PDF ainda não existe |

O painel (`/painel`) aponta para as quatro telas pelos atalhos de `DashboardAtalhos`, e o menu
lateral as marca com o selo **Demo**.
