# ADR-068 — Autenticação no mobile: sessão no armazenamento seguro e bloqueio por biometria

**Status:** aceito · **Relacionado:** [ADR-003](./003-supabase-auth.md), [ADR-029](./029-custom-access-token-hook.md),
[ADR-065](./065-alertas-in-app-e-realtime.md), [ADR-067](./067-flutter-para-o-app-mobile.md)

## Contexto

O app guarda a sessão do Supabase no aparelho para o advogado não digitar a senha toda vez. O
**refresh token dá acesso a dados sob sigilo profissional**. Um aparelho perdido, com backup
extraído ou com root, não pode entregá-lo em texto puro. Ao mesmo tempo, sessão guardada significa
que quem pegar o celular desbloqueado abre o app.

## Decisão

1. **A sessão fica no Keychain (iOS) / Keystore (Android)** — `SecureLocalStorage`, sobre
   `flutter_secure_storage`, plugado em `FlutterAuthClientOptions(localStorage:)`. O padrão do
   `supabase_flutter` grava em `shared_preferences`, que é texto puro. `allowBackup="false"` no
   Android evita que a sessão vá para o backup em nuvem. `shared_preferences` guarda só o que
   não é segredo (tema, interruptores).
2. **Sessão e desbloqueio são coisas diferentes** (`SessionState`): `hasSession` (há token
   restaurado) e `locked` (o usuário ainda não provou quem é *agora*). `isSignedIn = hasSession &&
   !locked`. Com o bloqueio biométrico ligado o app **abre bloqueado**, na tela de login, mesmo
   com sessão restaurada; só a biometria ou uma senha nova libera o conteúdo.
3. **A biometria destrava uma sessão que já existe — não faz login.** Sem sessão guardada não há
   o que liberar, então o botão "Entrar com Face ID / biometria" só aparece com sessão restaurada
   *e* biometria disponível. (O Supabase não nos dá a senha, e guardá-la seria pior que o
   problema que se quer resolver.)
4. **Ligar o bloqueio exige uma biometria na hora** e biometria disponível: ativar um bloqueio
   que depois ninguém consegue passar trancaria o usuário para fora. Desligar não pede nada.
5. **Falha de login não diz qual campo errou** ("E-mail ou senha incorretos."): dizer
   confirmaria a um estranho que o e-mail tem conta no escritório.
6. **O token é lido do JWT só para exibir** (nome, papel, escritório). **Nunca para autorizar**;
   quem valida assinatura e permissão é o backend.
7. **Sessão expirada derruba o usuário**: 401 → uma tentativa de renovação → se falhar, volta ao
   login. O retry usa o token novo e não depende de `readToken` devolver o valor atualizado.

## Alternativas consideradas

- **Bloqueio por PIN próprio do app.** Rejeitada: o aparelho já tem biometria e PIN do sistema;
  um segundo PIN é só mais coisa para esquecer.
- **Exigir biometria a cada retorno do segundo plano.** Adiada: aumenta o atrito e pede uma
  política de tempo limite que o produto ainda não definiu.

## Consequências

- O usuário com bloqueio ligado passa pela tela de login a cada abertura a frio, e é nisso que a
  segurança consiste.
- **Não há logout remoto do aparelho.** Se o aparelho for perdido, a sessão só acaba quando o
  refresh token for revogado no Supabase. Pendente: endpoint de dispositivos (ver
  `docs/mobile/README.md`, "Lacunas do backend") que permita revogar por aparelho.
