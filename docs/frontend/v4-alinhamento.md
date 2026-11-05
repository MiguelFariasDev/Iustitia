# Alinhamento ao protótipo Iustitia v4

Documento de trabalho do alinhamento do app (`web/`) ao protótipo v4. **O arquivo do protótipo
(`Iustitia v4.dc.html`) não estava disponível durante a implementação**: o que está aqui vem
do texto do pedido (tokens, comportamento, medidas), não de comparação visual com o protótipo.
Quando o arquivo estiver no repositório, conferir cada tela contra ele.

## Tokens: como o v4 foi mapeado sobre o que já existia

Em vez de renomear classes em todos os componentes, os valores do v4 foram colocados nos nomes
semânticos do shadcn que o código já usa (`web/src/app/globals.css`). Colisão de nome a ter em
mente: no protótipo `--accent` é o **violeta da IA**; no shadcn `accent` é o **fundo de hover**.
Aqui `accent` continua sendo o hover, e o violeta é `ia`.

| Protótipo | Token no app (classe Tailwind) |
| --- | --- |
| `--bg` | `--background` (`bg-background`) |
| `--panel` | `--card` / `--popover` |
| `--surface` | `--muted` (`bg-muted`) |
| `--text` / `--mute` | `--foreground` / `--muted-foreground` |
| `--line` / `--line-strong` | `--border` / `--line-strong` (`border-line-strong`) |
| `--hover` | `--accent` (`bg-accent`) |
| `--accent` / `--accent-text` / `--accentSoft` (IA) | `--ia` / `--ia-text` / `--ia-soft` (`text-ia-text`, `bg-ia-soft`) |
| `--brand` / `--brandSoft` | `--brand` / `--brand-soft`; `--primary` e `--secondary` também apontam para a marca |
| `--danger`, `--ok`, `--warn` (+ Soft) | `--destructive`, `--success`, `--warning` (+ `-soft`) |
| `--side*` | `--sidebar*`; `--sidebar-ia` é o violeta legível sobre o fundo escuro |
| `--sh` | `--sh` (usada por `shadow-sm` e `shadow-md`) |

`--gold` (o dourado da marca antiga) agora carrega o **violeta da IA**, para o selo ✨ existente
continuar funcionando sem reescrever os componentes. `--info` também é violeta (o v4 não tem azul).
Peças fixas nos dois temas (logo, painel da Athena, toast) têm tokens próprios.

Contraste: todos os pares texto/fundo do pedido foram calculados e passam em AA (o menor é
4,56:1, `--warn` sobre `--bg` no claro).

## Movimento

Três keyframes (`iu-in`, `iu-drawer`, `iu-pulse`) e as durações como variáveis
(`--dur-palette` 0,2s, `--dur-layer` 0,25s, `--dur-tab` 0,3s, `--dur-screen` 0,35s; hover 150ms),
expostas como `animate-iu-in`, `animate-iu-in-palette`, `animate-iu-in-layer`,
`animate-iu-in-tab`, `animate-iu-drawer` e `animate-iu-pulse`, mais os utilitários `iu-lift`
(KPI sobe 2px) e `iu-grow` (botão da Athena). Sob `prefers-reduced-motion` tudo aparece direto, o
pulso vira opacidade fixa e os hovers não deslocam. Só `opacity` e `transform` são animados.

## Decisões que divergem do pedido ou dependem do dono do produto

- **Permissões (RBAC).** O pedido define os perfis Sócio, Advogado(a), Secretária e Financeiro
  com telas próprias. O backend tem `UserRole` Owner, Partner, Lawyer, Intern, Admin e Financial,
  e a API hoje responde **403** a Admin, Intern e Financial em processos, publicações, clientes e
  alertas (`LawyerOrAbove`). A matriz do app (`web/src/lib/auth/access.ts`, ADR-068) espelha a API.
  Adotar a tabela do pedido exige **mudar as policies da API primeiro**, e decidir como os perfis
  do protótipo se mapeiam nos papéis reais (Secretária ≈ Admin? Estagiário?). Não foi alterado.
- **Nome da marca.** O app mostra `NEXT_PUBLIC_APP_NAME` (padrão "Advocacia", pela regra de Marca
  Desacoplada). Para o texto "Iustitia" do protótipo, definir `NEXT_PUBLIC_APP_NAME=Iustitia`.
