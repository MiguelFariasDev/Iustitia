# Sitemap Mobile (Flutter)

Mantido em sincronia com `mobile/lib/app/router.dart`. Requisitos de produto completos em
`docs/requisitos/07-navegacao-mobile.md` (não versionado — uso interno). Decisões em
[ADR-067](../adr/067-flutter-para-o-app-mobile.md) e [ADR-068](../adr/068-autenticacao-mobile-sessao-segura-e-biometria.md).

## Rotas

| Rota | Tela | Observação |
|---|---|---|
| `/login` | `LoginPage` | Única rota pública. Aceita `?from=` (destino pós-login), **validado por `safeInternalRoute`**. |
| `/` | — | Redireciona para `/dashboard`. |
| `/dashboard` | `DashboardPage` | Aba **Painel**. Selo com os alertas não lidos. |
| `/processos` | `ProcessosPage` | Aba **Processos**. |
| `/processos/:id` | `ProcessoDetalhePage` | Abas Dados, Publicações, Movimentos. |
| `/clientes` | `ClientesPage` | Aba **Clientes**. |
| `/clientes/:id` | `ClienteDetalhePage` | Dados + processos vinculados. |
| `/agenda` | `AgendaPage` | Aba **Agenda** — **dados de demonstração**. |
| `/perfil` | `PerfilPage` | Aba **Perfil**. |

As cinco abas são um `StatefulShellRoute.indexedStack`: cada uma **guarda a própria pilha** (voltar
de um detalhe preserva busca e rolagem). Tocar na aba ativa volta à raiz dela.

## Regras do redirect global

```
sem sessão liberada  →  /login   (guarda o destino em ?from=, exceto a raiz)
sessão liberada em /login → ?from= (se for rota interna válida) senão /dashboard
```

"Sessão liberada" = há sessão **e** o app não está bloqueado pela biometria (ver ADR-068).

## Deep links de notificação

O payload de push/notificação local traz `data.route`. Ele só navega se passar por
`safeInternalRoute`, que aceita exclusivamente:

`/dashboard` · `/processos` · `/processos/{id}` · `/clientes` · `/clientes/{id}` · `/agenda` · `/perfil`

Qualquer outra coisa (outro esquema, `//host`, `/login`, caminhos com mais segmentos, `\`) é
**ignorada**. Ids aceitam só letras, dígitos e hífen. Se o usuário não estiver logado, o destino
fica em `?from=` e abre depois do login.

Alerta em tempo real: o banner resolve o destino antes de aparecer. Alerta de publicação aponta
para a publicação (`referenceId`); como o app não tem tela de publicação, resolve o **processo**
dela (`GET /publications/{id}` → `/processos/{processId}`). Sem processo, o banner aparece sem o
botão "Ver".
