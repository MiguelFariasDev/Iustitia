# Frontend

Documentação da aplicação web (`/web`). Decisões arquiteturais em
[`docs/adr/`](../adr): 042 (stack), 043 (design system), 044 (estado),
045 (skills da Vercel) e 058 (referências visuais).

| Documento | Conteúdo |
|---|---|
| [structure.md](./structure.md) | Árvore de pastas, regras de dependência, anatomia de uma rota |
| [design-system.md](./design-system.md) | Tokens de cor, tipografia, espaçamento e componentes compartilhados |
| [state-management.md](./state-management.md) | TanStack Query para dados remotos, Zustand para interface |
| [performance.md](./performance.md) | Regras aplicadas e onde aparecem no código |
| [accessibility.md](./accessibility.md) | Meta WCAG 2.1 AA, convenções e verificação automatizada |
| [references.md](./references.md) | Referências visuais e o limite entre inspiração e cópia |

## Comandos

```bash
cd web
npm run dev           # desenvolvimento
npm run build         # build de produção (faz typecheck)
npm run lint          # ESLint
npm run type-check    # typegen de rotas + tsc --noEmit
npm run test          # Vitest (unitários)
npm run test:e2e      # Playwright (E2E + acessibilidade)
```

O E2E sobe o próprio servidor com autenticação e dados simulados
(`NEXT_PUBLIC_AUTH_MODE=mock`, `NEXT_PUBLIC_API_MOCKS=true`), compilando em
`.next-e2e` para não conflitar com um `next dev` em andamento. Na primeira
execução, instale o navegador: `npx playwright install chromium`.
