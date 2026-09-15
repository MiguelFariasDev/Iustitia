# ADR-003: Supabase Auth em vez de ASP.NET Core Identity

**Status:** Aceito

## Contexto

O sistema precisa de autenticação com MFA (TOTP), OAuth social (para
onboarding facilitado) e emissão/renovação de JWT, tanto para o frontend
React quanto para o app mobile Swift. Implementar e manter esses fluxos com
ASP.NET Core Identity exigiria construir (ou integrar) MFA, gestão de
refresh tokens e fluxos OAuth do zero, além de manter esse código seguro e
atualizado ao longo do tempo.

Como o banco de dados já é o Supabase (ADR-002), aproveitar o Supabase Auth,
que roda sobre o mesmo Postgres, evita duplicar a fonte de verdade de
usuários e credenciais.

## Decisão

Usar **Supabase Auth** para: login, MFA (TOTP), OAuth, emissão de JWT e
gestão de refresh tokens (via SDKs oficiais Supabase JS e Supabase Swift). O
backend .NET **nunca** gerencia senhas ou emite tokens — ele apenas **valida**
o JWT emitido pelo Supabase, usando o endpoint JWKS para verificar a
assinatura. Claims customizadas (`tenant_id`, `role`, `permissions`) são
injetadas via Custom Access Token Hook do Supabase, para que o backend não
precise consultar o banco a cada requisição só para saber o tenant do
usuário.

## Consequências

**Positivas:**
- Menos código de segurança sensível para manter e auditar no backend.
- MFA e OAuth "de fábrica", sem esforço de implementação.
- Claims customizadas no próprio JWT reduzem round-trips ao banco.

**Negativas / trade-offs:**
- Dependência do Supabase Auth para disponibilidade de login (mitigado pelo
  SLA do provedor e por ser um serviço gerenciado maduro).
- Qualquer regra de autorização mais fina (permissões granulares por módulo)
  precisa ser resolvida no backend a partir das claims, não delegada ao
  Supabase.
