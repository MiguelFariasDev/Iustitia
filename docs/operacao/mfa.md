# MFA para sócios e titulares

## Como funciona

1. O Supabase Auth marca cada sessão com `aal` no JWT: `aal1` (senha) ou `aal2` (senha + TOTP verificado).
2. Com `Security:Mfa:Enforce=true`, a API (`MfaEnforcementMiddleware`) exige `aal2` dos papéis em
   `Security:Mfa:RequiredRoles` (padrão `Owner` e `Partner`). Sem isso responde **401 `AUTH_MFA_REQUIRED`**.
3. O frontend trata esse erro sem renovar token nem encerrar a sessão: leva a `/mfa?next=<rota>`, onde a pessoa
   cadastra o autenticador (QR code) ou digita o código, e volta para onde estava.
4. Ficam fora da exigência: `/api/v1/auth/*`, `/api/v1/me` (o app precisa dela para saber quem é o usuário) e os health checks.

## Ativação em produção (ordem importa)

1. Confirme que **MFA/TOTP está habilitado** no projeto Supabase (Authentication → Multi-Factor).
2. Faça cada sócio/titular **entrar uma vez e cadastrar o autenticador** em `/mfa` (a rota funciona mesmo com a
   exigência desligada: basta abri-la logado).
3. Só então defina `Security__Mfa__Enforce=true` no ambiente da API.
4. Se alguém for trancado para fora: desligue a variável, ou remova o fator do usuário no painel do Supabase
   (Authentication → Users → fatores) e peça um novo cadastro.

## Pendências conhecidas

- Não há códigos de recuperação nem política de "lembrar este dispositivo".
- O papel `Admin` não é exigido por padrão; inclua-o em `RequiredRoles` se o escritório quiser.
