# App mobile (Flutter)

Fundação do aplicativo iOS/Android do escritório. Código em [`/mobile`](../../mobile) — instruções
de execução em [`mobile/README.md`](../../mobile/README.md). Decisões: [ADR-067](../adr/067-flutter-para-o-app-mobile.md)
e [ADR-068](../adr/068-autenticacao-mobile-sessao-segura-e-biometria.md). Rotas: [`navigation/mobile-sitemap.md`](../navigation/mobile-sitemap.md).

## Telas

| Tela | O que mostra | Dados |
|---|---|---|
| **Login** | E-mail e senha; "Entrar com Face ID / biometria" quando há sessão guardada e biometria no aparelho | Supabase Auth |
| **Painel** | Saudação ("Bom dia, Dr(a). Ana" — não presume gênero), cartões clicáveis, últimas movimentações, pull-to-refresh | **Real**: `/publications/summary`, `/publications`, `/processos` |
| **Processos** | Busca (CNJ, cliente, parte contrária), filtros Ativos/Arquivados, rolagem infinita | **Real**: `/processos` |
| **Processo** | Dados · Publicações · Movimentos; compartilhar; ver no site do tribunal | **Real** (Movimentos: "em breve") |
| **Clientes** | Busca por nome ou CPF/CNPJ (com ou sem máscara) | **Real**: `/clientes` |
| **Cliente** | Dados e processos vinculados | **Real** |
| **Agenda** | Mês e semana; toque no dia lista os eventos; toque no evento abre os detalhes | **Demonstração** |
| **Perfil** | Dados do usuário, tema (sistema/claro/escuro), bloqueio por biometria, sair | JWT + preferências locais |

### Cartões do painel

O pedido listava *Tarefas pendentes*, *Publicações não lidas* e *Próximos prazos*. **Tarefas e
prazos não existem no backend**, e um número inventado leva a decisão errada. O painel mostra:

| Cartão | Valor | Leva a |
|---|---|---|
| Processos ativos | real | `/processos` |
| Publicações não lidas | real | `/processos` |
| Próximos prazos | **"Em breve"**, sem número | `/agenda` |

## Acessibilidade

Todo cartão é um botão com rótulo falado completo ("Processo 5001…, cliente Maria, Cível, Ativo");
estados (ativo, suspenso, tipo de evento) vêm **por texto**, nunca só por cor; alvos de toque de
48 dp no mínimo; erros e o banner de alerta são `liveRegion` (anunciados na hora); contraste do
tema claro e do escuro medido sobre a renderização real (texto dos botões selecionados: 5,17:1 e
6,78:1; mínimo AA 4,5:1). No tema escuro a cor primária é um azul claro — o azul-marinho sumiria
sobre `#121212`.

## Notificações

- **Em tempo real (in-app):** canal privado `tenant:{tenantId}:alerts:{userId}` do Supabase
  Realtime, evento `alert.created` (ADR-065). Banner no topo, que some em 8 s; atualiza o painel
  e o selo do ícone Painel. Sem Realtime, o contador converge por polling (1 min).
- **Push (FCM/APNs):** só liga com as quatro chaves do Firebase (`FIREBASE_*`); sem elas o app
  roda normalmente, sem push. Em primeiro plano mostra uma notificação local; ao tocar, abre a
  rota do `data.route` (validada). **Registro do aparelho:** `POST /devices/register`.
- **Lembretes de prazo:** `DeadlineReminderScheduler` agenda "24 h antes" para prazos e
  audiências (reuniões não). Pronto e testado, mas **desligado** até a agenda ter dados reais.

## Lacunas do backend (o app já está pronto para elas)

| Falta | Efeito hoje |
|---|---|
| `POST /api/v1/devices/register` | O app tenta e, em 404, segue **sem push** (não falha). O backend precisa definir o que o campo `token` carrega: o token FCM clássico (`getToken`, o que o app envia hoje) ou o Installation ID (`register()`/`onRegistered`, o caminho novo do Firebase 17). |
| Revogar sessão por aparelho | Aparelho perdido: a sessão só acaba revogando o refresh token no Supabase (ver ADR-068). |
| Agenda, prazos e tarefas (Fases 3–4) | Agenda de demonstração; cartão de prazos "Em breve". |
| OAB no JWT ou em `/me` | O Perfil mostra "Não informada" (lê a claim `oab`, se existir). `/me` hoje só devolve id, e-mail, escritório e papéis. |
| Movimentações do tribunal | Aba "Movimentos" diz que está por vir. |

## Fora do escopo desta etapa

Cadastro de processos, importação de planilha e financeiro avançado ficam no web. **Cache em
disco e modo offline** também ficaram de fora (ADR-020 segue em aberto): hoje o Riverpod mantém o
que já foi carregado durante a sessão, e só.
