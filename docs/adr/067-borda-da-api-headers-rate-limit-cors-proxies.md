# ADR-067 — Borda da API: headers, rate limiting, CORS e proxies

**Status:** aceito · **Contexto:** Etapa 3.9 (hardening)

## Decisão

1. **Headers OWASP em toda resposta** (`SecurityHeadersMiddleware`, escritos em `OnStarting`
   para cobrir também 401/429/erros): `nosniff`, `X-Frame-Options: DENY`, HSTS 1 ano,
   `Referrer-Policy`, `Permissions-Policy`, `Cache-Control: no-store` (dado de cliente) e CSP
   `default-src 'none'` na API JSON (mais frouxa só para Swagger em dev e dashboard do Hangfire).
   `Server` e `X-Powered-By` removidos.
   **Desvio do pedido:** `X-XSS-Protection: 0`, não `1; mode=block` — o filtro dos navegadores
   foi removido por introduzir vulnerabilidades (OWASP/Mozilla recomendam desligá-lo). A defesa
   é a CSP.
2. **`UseForwardedHeaders` primeiro no pipeline.** Atrás de Front Door → ingress do Container
   Apps o `RemoteIpAddress` seria o do proxy; o limite de login (5/5 min por IP) viraria um
   balde global e cinco erros de qualquer pessoa bloqueariam o login de todos. `ForwardLimit`
   configurável (`Security:ForwardedHeaders:ForwardLimit`, 2 em produção).
   **Pendência de infra (já listada em docs/devops/infrastructure.md):** restringir o ingress
   do Container App ao Front Door (`X-Azure-FDID`). Sem isso um cliente que fale direto com o
   Container App forja `X-Forwarded-For` e escapa do limite por IP.
3. **Rate limiting** conforme `docs/api/conventions.md`. Corrigido um defeito existente:
   `UseRateLimiter` rodava *antes* de `UseAuthentication`, então o limite "por usuário" era
   por IP. Agora vem depois. Login/forgot-password usam janela deslizante; IPv6 agrupa no /64.
   Contadores são **em memória por réplica** (limite efetivo ≤ N× com N réplicas); limite
   distribuído em Redis fica como evolução.
4. **CORS:** sem `AllowAnyOrigin` nem `AllowCredentials` (a API usa Bearer, não cookie);
   origens validadas na subida (sem curinga, `https` obrigatório exceto localhost).

## Consequências
Testes: `RateLimitingTests`, `SecurityHeadersTests`, `ApiConventionTests` (ordem do pipeline),
`SqlInjectionGuardTests` (proíbe `AllowAnyOrigin` e SQL concatenado).
