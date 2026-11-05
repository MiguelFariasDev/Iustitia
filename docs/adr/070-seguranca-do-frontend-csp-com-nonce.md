# ADR-070 — Segurança do frontend: CSP com nonce e tokens

**Status:** aceito · **Contexto:** Etapa 3.9

## Decisão
- **CSP por requisição, com nonce + `strict-dynamic`**, gerada em `src/proxy.ts`. O pedido
  sugeria `script-src 'self' 'unsafe-inline'`, que anula a proteção contra XSS. O Next.js carimba
  o nonce nos próprios scripts; o script de tema do layout o recebe via `x-nonce`.
  `style-src` mantém `'unsafe-inline'` (Radix/Tailwind/next-font) — risco residual menor,
  aceito. `unsafe-eval` só em dev. `frame-ancestors 'none'`, `connect-src` restrito à API e ao
  Supabase. Consequência: toda página é renderizada sob demanda (já eram, pelo proxy).
- **XSS:** não há `dangerouslySetInnerHTML` com dado de usuário; teste
  (`no-raw-html.test.ts`) falha em qualquer novo uso fora da lista. Se um dia for necessário
  renderizar HTML (ex.: texto de publicação), usar DOMPurify e registrar na lista.
- **Tokens:** cookies com `Secure` (prod) e `SameSite=Lax`; nenhum token em
  `localStorage` (teste). **`HttpOnly` não é viável hoje:** o cliente Supabase de navegador lê o
  token em JS para chamar a API com Bearer. Alcançar HttpOnly exige um BFF (route handlers do
  Next intermediando a API) — evolução recomendada antes de dados reais em larga escala.
- `middleware.ts` → `proxy.ts` (Next 16) continua só como roteamento otimista; a fronteira de
  segurança é a validação do JWT na API.

## Pendente
`npm audit` aponta vulnerabilidades em produção, incluindo `next` (crítica) — atualizar.
