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

## Painel (item 5.1) — o que é real e o que é demonstração

O painel muda por perfil (`web/src/features/dashboard/persona.ts`): **Sócio** (Owner, Partner),
**Advogado(a)** (Lawyer **e** a conta Solo, que trabalha os próprios casos), **Secretária** (Admin,
Intern) e **Financeiro** (Financial). Papel desconhecido cai na visão de menor privilégio.

| Bloco | Origem |
| --- | --- |
| Prazos hoje, prazos da semana, fila de prazos, agenda de hoje, riscos de prazo | **Real** (agenda) |
| Para revisar, Alertas, publicações "Sugeridas pela IA" | **Real** (publicações e alertas) |
| Tarefas atrasadas, Prazos cumpridos | **"—"** — sem módulo de tarefas; não se inventa número |
| Receita, em atraso, aprovações, carga da equipe, financeiro inteiro, painel da Secretária | **Demonstração** (selo na tela; dados em `dashboard/demo.ts`) |

Divergências do protótipo:

- **Sem "Atrasado" na fila.** A agenda não guarda se um compromisso foi cumprido: um prazo de
  ontem sem baixa é indistinguível de um prazo perdido, e marcar tudo o que passou como atrasado
  inventaria um alarme. O nível `overdue` existe nos tipos para quando houver tarefas com conclusão.
- **Só prazo "vence hoje".** Audiência, reunião e tarefa aparecem como "Hoje".
- **Coluna do processo com 190px** (no protótipo, 150px): um número CNJ em mono tem ~180px e
  cortado deixa de ser útil. A coluna do prazo ficou com 72px.
- **Sem coluna de cliente na fila:** o evento da agenda só guarda o processo.
- **Saudação dinâmica** ("Bom dia/Boa tarde/Boa noite"), em vez de "Bom dia" fixo.
- **"Athena monitora o CNJ desde as 06:00"** virou "A Athena acompanha as publicações do CNJ": não
  há horário fixo de captura que a tela possa afirmar.
- **Cada perfil só busca o que a API lhe permite ler.** Secretária e Financeiro não disparam
  nenhuma consulta (a API lhes recusaria publicações e processos); o Sócio só busca a agenda.
- A variação A/B do advogado é guardada no navegador (`uiStore`).

## Publicações (item 5.2)

- **Cartões:** "Não tratadas hoje", "Aguardando revisão" e "Tratadas na semana", no estilo do v4. Cada
  um leva à lista com o **mesmo número**: o link de "hoje" filtra também pela data de hoje, e o de
  "semana", desde a segunda-feira (no fuso local). Antes o clique abria o total, um número diferente do
  do cartão. Sem "agora" (antes de hidratar) os links saem sem data.
- **Visões rápidas** `Todas | Não lidas | Aguardando revisão | Tratadas`, derivadas da URL (status e
  `isRead`). "Aguardando revisão" é o `untreated` do domínio. Os chips que já existiam ficaram só com
  o que as visões não cobrem (Descartada, Arquivada, Lidas), para não duplicar controles.
- **Linha:** o bloco inteiro é o link para a publicação (antes só o número era link, e uma publicação
  **sem processo não tinha como ser aberta**), bolinha violeta e texto em destaque quando não lida,
  tempo relativo, e a coluna Status passou a caber na tela larga.
- **Atualizar** agora diz o que aconteceu: "N publicações novas" ou "Nenhuma publicação nova desde
  HH:mm", com a hora em que a contagem anterior foi carregada (o protótipo fixa "06:00").
- **Quem revisa:** só Owner, Partner e Lawyer abrem uma publicação em revisão da IA; os demais recebem
  "Revisão da IA é feita por advogado ou sócio" e não navegam. Hoje essa regra raramente dispara, porque
  a API já não admite esses papéis em publicações (ver RBAC acima).

### O selo "IA sugeriu prazo" não existe — de propósito

O contrato da API de publicações **não informa, por publicação, se a IA sugeriu um prazo**. O app sabe
o estado (`untreated`) e a flag `ai.classification` do escritório. Por isso:

| Situação | Selo |
| --- | --- |
| Pendente, com processo, IA ligada | **Revisão da IA** (violeta, ✨) — não afirma que um prazo foi sugerido |
| Pendente, com processo, IA desligada | **Aguardando revisão** (sem ✨) |
| Pendente, sem processo | **Sem processo** |
| Tratada | **Tratada** |

A mesma regra vale no **Painel**: a fila chamava toda pendência de "sugestão de prazo" com ✨, o que era
uma afirmação sem lastro (corrigido). Sem a flag, o item é só "Revisar publicação", sem ✨, e o filtro
"Sugeridos pela IA" nem aparece. Quando a API expuser a sugestão por publicação, `statusBadgeOf`
(`publicacoes/view.ts`) e `buildQueue` são os dois únicos pontos a ajustar. No mock do tenant a flag
está **ligada**, para a demonstração mostrar a revisão da IA.

## Revisão da IA (item 5.3) — `/publicacoes/revisao`

- Abas por publicação (`01 Contestação` …, com ✓ quando decidida), variações A (dividida, com o trecho
  gerador destacado em `<mark>`) e B (leitura com métricas e barra de decisão grudada na base), botões
  contornados Rejeitar / Corrigir / Aceitar, **Corrigir** com campo numérico e **Confirmar**, resultado no
  lugar dos botões e **avanço automático** para a próxima ainda não decidida.
- **Decisão real, sugestão de demonstração.** O backend já tem o fluxo de revisão
  (`PendingReview → Approved/Corrected/Rejected`, com nota). Aceitar e Corrigir **salvam o prazo na
  agenda e mudam o status** (Approved/Corrected, com a nota do valor sugerido e do corrigido, para
  auditoria); Rejeitar muda para Rejected. O que **não existe** é a sugestão da IA por publicação: em
  modo de demonstração ela é calculada a partir do texto (`sugestao.ts`), e **sem mocks a tela diz que a
  revisão da IA ainda não está disponível** em vez de inventar um prazo para um escritório real.
- **Dias úteis ignoram feriados, recesso e suspensões.** A data vem marcada como estimativa na tela e na
  descrição do evento. Nunca é a fonte de um prazo processual.
- Só **Owner, Partner e Lawyer** acessam (nova tela `review` na matriz de acesso).
- Defeito achado pelo teste e corrigido: campo de prazo **vazio** valia 0 dia e liberava "Confirmar".
- O fundamento legal de um prazo lido do texto cita só a origem ("fixado no próprio texto"): colar o artigo
  da tabela por tipo podia contradizer o prazo (ex.: art. de 5 dias ao lado de um prazo de 15).

## Criação de peças (item 5.9) — `/criacao-de-pecas`

- Tipo de peça (Contestação, Réplica, Apelação, Contrarrazões) + processo → **Gerar minuta com IA** →
  estado de carregamento → minuta com as **citações legais destacadas** (`art. 335 do CPC`) →
  **Salvar como rascunho**, que cria o arquivo em Documentos vinculado ao processo. Trocar o tipo ou o
  processo descarta a minuta (ela era de outra peça).
- **Não existe API de geração.** Em demonstração a "minuta" é um **esqueleto**: estrutura e artigos
  conferidos, com lacunas `[...]` (fatos, direito, nome do cliente, data, advogado e a data de início da
  contagem do prazo) para o advogado preencher. Não é texto jurídico pronto, a tela diz isso, e o que o
  processo não informa vira lacuna, nunca um valor inventado. **Sem mocks a tela diz que a geração não está
  disponível** em vez de entregar uma peça de aparência pronta a um escritório real.
- O rascunho é salvo como **`.txt`**: exportar para **PDF/DOCX** não existe (o botão aparece desabilitado e
  rotulado "em breve", sem fingir). Documentos também é demonstração (memória do navegador).
- O item do menu só deixa de ser "Em breve" quando há mocks (`soon: !env.useMocks`).

## Tarefas / Kanban de equipe (item 5.11) — `/tarefas`

- Quatro colunas (A fazer, Em andamento, Em revisão, Concluído), cartão com título, **anel de progresso do
  prazo** (38px, r = 15, `stroke-dasharray` 94,2 e `dashoffset = 94,2 × (1 − pct/100)`; ≥ 90 % vermelho,
  ≥ 70 % violeta, senão verde; rótulo "2d", "0d", "1s" ou "ok"; dica "N % do prazo decorrido"), processo em
  mono, responsável e botões ‹ › desabilitados nas pontas.
- **Arrastar e soltar** além dos botões (melhoria permitida): os botões seguem sendo o caminho por teclado
  e leitor de tela, e cada movimento é anunciado numa região `aria-live`.
- **Não existe módulo de tarefas no backend.** Tudo é demonstração em memória (zerada ao recarregar), com
  selo no menu e aviso na tela. Quando existir, o painel passa a mostrar "Tarefas atrasadas" e "Prazos
  cumpridos" de verdade (hoje aparecem como "—").
- Acesso: todos, menos o financeiro (não há policy de API a espelhar). O cabeçalho "Equipe e gestão" só
  aparece enquanto houver Tarefas ou Indicadores a mostrar.
- O prazo `yyyy-MM-dd` é lido como dia **local** (`new Date('2026-11-05')` seria meia-noite UTC, o dia
  anterior no Brasil e um prazo "vencido" um dia antes).

## Camadas globais (5.13)

**Gaveta de notificações.** O sino abre uma gaveta modal à direita (400px; 0,25s abrindo, 0,15s fechando), com foco devolvido ao sino. Divergências: sem legenda "Sistema/IA" (o alerta não traz origem) e alertas lidos não ficam a 35% de opacidade (não passaria no contraste AA).

**Athena.** Botão flutuante + painel escuro de 410px. É DEMONSTRAÇÃO: só aparece com mocks ligados e para quem acessa processos (a API de IA exige LawyerOrAbove). "Prazos de hoje" lista os prazos reais da agenda; qualquer outra pergunta recebe a resposta honesta de que a IA não está conectada. Precisa de backend: serviço de IA, histórico de conversa e leitura de publicações/processos.

## Agenda (5.4)

Visão Mês/Semana/Dia virou `SegmentedControl`; a grade entra com `animate-iu-in` a cada troca de visão ou período; o dia de hoje ganha fundo `brand-soft`. **Semana segue começando na segunda** (decisão do produto; o protótipo usa domingo). Ao filtrar por tipo, só o ponto colorido dos tipos fora do filtro esmaece a 50% — o texto do chip fica pleno para manter contraste AA. Tudo já funcionava com a API real; nada aqui é só demonstração.

## Atendimentos (5.5)

A inbox de três colunas e o fluxo (assumir, devolver ao bot, encerrar, vincular cliente) já existiam e seguem iguais. Ajustes v4: conversa e contexto entram com `animate-iu-in`, mensagens novas também, linha selecionada com borda `brand`, leve elevação no hover (só com movimento permitido). **Não feito, por falta de dado:** contador de "Aguardando" na aba (a lista já vem filtrada pela aba; a API não devolve totais por status) e indicador de não lidas (sem campo na API). Ambos dependem de backend.

## Processos (5.6)

Situação virou controle segmentado (Todos · Ativos · Suspensos · Encerrados · Arquivados) e Lista/Quadro também; a tabela entra com `animate-iu-in` e as linhas ganham hover. **Divergência:** o protótipo tem 4 filtros (sem "Encerrados"), mas o backend tem 4 situações reais (Ativo, Suspenso, Encerrado, Arquivado); omitir uma esconderia processos. O painel de movimentações já existe na aba "Movimentos" do detalhe (consulta ao DataJud, ver ADR-064) e não foi alterado. Tudo usa a API real; a tela não tem mocks.

## Contatos (5.7)

O filtro por tipo trocou os chips alternáveis por controle segmentado (Todos · Parte contrária · Testemunha · Fornecedor · Outros), com "Todos" explícito em vez de "clicar de novo para desfazer"; a lista entra com `animate-iu-in`. O aviso de dados de demonstração e a exclusão com confirmação continuam como estavam. **Precisa de backend:** o módulo CRM em `src/Modules/CRM` nunca foi compilado nem migrado (ver pendências); sem mocks a tela depende dele.

## Financeiro (5.8)

Abas na ordem do v4: Honorários · A receber · A pagar · Em atraso (+ Fluxo de caixa, que já existia). "Em atraso" cruza os três tipos e lista só o que está com status Atrasado (com coluna "Tipo"); nela o filtro de status some por redundância. Status é controle segmentado; o CSV exporta o que a aba mostra (nome `financeiro-atraso.csv` na nova aba). Os cartões de resumo ganharam a borda superior de cor do v4, mas **mantêm o número em `text-2xl`, não nos 45px do `KpiCard`**: "R$ 11.700,00" em 45px estoura a coluna em quatro cartões. Exportação em PDF segue indisponível (avisado ao usuário). **Tudo é demonstração:** não existe backend financeiro; o aviso de dados fictícios permanece e lançamentos novos somem ao recarregar. O status "Atrasado" vem pronto do mock — com backend real, quem calcula o vencimento é o servidor.

## Documentos e Alertas (5.10)

**Alertas.** Abas na ordem do v4: Importantes · Publicações · Prazos · Todos (a "Outros" saiu; aniversários e "outros" só aparecem em Todos). Duas decisões: (1) **"Prazos" lista alertas do tipo `Task`**, porque a API de alertas não tem um tipo "prazo" (`Publication | Task | Birthday | Other`); se o backend ganhar um tipo próprio, só `tabs.ts` muda. (2) **"Publicações" agora inclui as já lidas** (antes era "Publicações não tratadas", só não lidas) para o nome da aba dizer o que ela contém; as não lidas continuam em Importantes. As contagens das abas saem do resumo (total por tipo). O painel lateral de abas rola na horizontal no celular.

**Documentos.** Filtros de tipo de arquivo e de vínculo viraram controles segmentados com "Todos" explícito; a grade entra com `animate-iu-in` e os cartões ganham elevação no hover. Segue em demonstração: arquivos ficam só em memória e somem ao recarregar (aviso mantido). Precisa de backend: armazenamento real, tamanho/tipo validados no servidor e vínculo com processo/cliente.

## Indicadores (5.12)

Período virou controle segmentado (Mês atual · Últimos 3 meses · Ano) e o conteúdo entra com `animate-iu-in` a cada carga. Gráficos, cartões e tabela de produtividade ficaram como estavam. **Tudo é demonstração:** não existe API de indicadores; os números são fictícios e o aviso permanece (o texto do aviso ainda diz que prazos e tarefas "só existem nas próximas fases", o que já não é verdade para a Agenda — decisão do produto se o aviso deve ser reescrito ou se a tela passa a calcular prazos a partir da agenda real). Precisa de backend: consultas agregadas por período, produtividade por advogado e tarefas concluídas.

## Paleta de comandos (5.13)

Janela de 560px a 14vh do topo, centrada só na horizontal (confirmado no navegador: x e largura exatos em 1440px e 390px, sem o desvio do `translateX`). Enter executa o primeiro resultado (o cmdk já seleciona o primeiro item); digitar "agenda" + Enter abre `/agenda`. **Mudança de conteúdo:** a seção de páginas só tinha o painel e as configurações; agora lista todas as telas do menu que o papel pode abrir ("Ir para …", mesma matriz de acesso do menu, sem itens "em breve"). **Limitação:** a busca global de processos, clientes e documentos devolve vazio (não existe `/search` no backend, de propósito: resultado inventado levaria a um processo inexistente); por isso a paleta só navega e executa ações. Mantive o botão de fechar (X), que o protótipo não tem, para quem usa toque. A animação de entrada continua a do diálogo padrão (fade + zoom, 200ms), não o `animate-iu-in-palette`.

## Mobile (item 8)

**Barra inferior** (`BottomNav`, só abaixo de `md`, 56px): Painel · Revisão · Atender. Cada aba só aparece se o papel pode abrir a tela (mesma matriz do menu); Revisão também exige mocks, como o link no painel. Financeiro, por exemplo, só vê "Painel". O resto da navegação continua no ☰. O conteúdo ganhou `pb-14` para a barra não cobrir a última linha. **Ações fixas:** a barra de decisão da Revisão grudava em `bottom-0` e ficaria sob a barra de abas; agora gruda em `bottom-14` no celular. O campo de resposta dos Atendimentos terminava 55px abaixo da barra (escondido); a altura da inbox no celular foi reduzida em 3,5rem e o campo termina exatamente acima da barra (verificado no navegador, 390×844). As tabelas rolam na horizontal dentro do cartão (DataTable) em vez de estourar a página; **não** viraram lista de cartões — decisão do produto se vale o esforço. Athena (botão flutuante) fica acima da barra, mas pode cobrir um valor na lateral direita de alguns cartões (ex.: "confiança" na Revisão); não há solução sem esconder o botão em telas específicas. **Não feito:** caixa de entrada mobile (inbox) própria e skeletons com a forma de cada tela — os skeletons atuais são genéricos.
