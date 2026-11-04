# ADR-066 — Telas de apoio entregues com dados de demonstração

**Status:** aceito · **Contexto:** Fase 2, frontend adicional (Contatos, Documentos,
Indicadores e Financeiro)

## Contexto

O painel de gestão aponta para quatro destinos que ainda eram placeholders ("Em breve"):
`/contatos`, `/documentos`, `/indicadores` e `/financeiro`. A advogada precisa percorrer a
navegação inteira e validar a **experiência** de cada tela antes de o backend existir:

- **Contatos** e **Documentos** não têm endpoint (a pasta `Documents.*` está vazia e não há
  storage de arquivos);
- **Indicadores** depende de prazos e tarefas, que só passam a existir nas Fases 3 e 4;
- **Financeiro** é da Fase 4/8.

Ligar a API real hoje devolveria 404, e preencher as telas com números que parecem do
escritório leva a decisão errada (a nota em `features/dashboard/api.ts` já dizia: *"um cartão
com número inventado é pior que cartão nenhum"*).

## Decisão

1. **As telas são reais; os dados são de demonstração.** Cada feature
   (`web/src/features/{contatos,documentos,indicadores,financeiro}`) tem a mesma estrutura das
   features com backend — `types`, `schemas` (Zod), `api`, `queries`, `hooks`, `components` —,
   de modo que **trocar o mock pelo servidor muda só `api.ts`** (e apaga `mocks.ts`).
2. **O mock é um repositório em memória**, não um array estático: criar, editar e excluir
   funcionam e refletem na lista, nos totais e nos gráficos, e reimplementam as regras que a API
   terá (ordenação, filtros, campos vazios viram `null`). Recarregar volta ao estado inicial.
3. **Sempre mock, independente de `NEXT_PUBLIC_API_MOCKS`.** Nas features que já têm backend a
   variável escolhe entre mock e API; aqui não, porque `false` levaria a 404. Cada `api.ts` e
   `mocks.ts` carrega um `TODO(<modulo>-backend)` dizendo o que trocar.
4. **O aviso de demonstração é permanente e não dispensável** (`DemoNotice`, `role="note"`), e o
   menu lateral mostra o selo **Demo** (`NavItem.demo`) no lugar de "Em breve". Quem olha a tela
   sabe, sem precisar adivinhar, que nada ali é do escritório.
5. **Os dados de demonstração são coerentes entre si**: a soma das áreas é a soma dos status; os
   processos ativos por advogado somam o KPI; datas são relativas a "hoje" (datas fixas já
   faziam "faturamento do mês" mentir). Testes guardam essas invariantes.
6. **Honestidade nos limites**: documentos enviados ficam só na memória da aba; o download de um
   documento de exemplo entrega um `.txt` que diz que não há conteúdo (e não um PDF vazio com o
   nome do original); a exportação em PDF avisa que não existe em vez de baixar um arquivo
   vazio; o CSV, esse sim, é real.
7. **Recharts carrega sob demanda** (`next/dynamic`, `ssr: false`) em Indicadores e no Fluxo de
   caixa: é a maior dependência da tela e fica abaixo da dobra. O Next exige que as opções do
   `dynamic` sejam um objeto *literal* — não dá para compartilhá-las numa constante.
8. **Gráficos são acessíveis por texto**: cada um é um `role="img"` cujo `aria-label` lista os
   valores (`descreverSerie`), e a camada de acessibilidade interna do Recharts
   (`accessibilityLayer`) fica desligada — o `role="application"` focável que ela injeta, dentro
   de uma imagem, é redundante e confunde leitores de tela.

## Alternativas consideradas

- **Manter "Em breve" até o backend existir.** Rejeitada: a advogada não consegue validar o
  fluxo (cadastro, filtros, modais) e o feedback de UX só chegaria depois do backend pronto,
  quando mudar custa mais.
- **Compor Indicadores no cliente a partir de `/processos` e `/publications`.** Rejeitada: cobre
  parte dos números (prazos e tarefas não existem) e mistura dado real com inventado na mesma
  tela. O endpoint agregado pertence ao módulo Reporting.
- **MSW/interceptação de rede em vez de repositório em `api.ts`.** Rejeitada por ora: as demais
  features do projeto já usam o padrão `env.useMocks` em `api.ts`; manter o mesmo padrão é
  mais barato de entender e de remover.

## Consequências

- **Positivas:** navegação completa e testável (unitário, E2E e axe WCAG 2.1 AA em todas as
  telas e nos dois modais); troca para o backend localizada em `api.ts`.
- **Negativas:** há código de mock a manter e a apagar; e os dados de demonstração **não são
  persistidos** — quem testa precisa saber disso (por isso o aviso).
- **Risco:** o aviso e o selo "Demo" precisam sair **junto** com o mock de cada módulo. Ao ligar o
  backend de um módulo, remover `DemoNotice`, o `demo: true` em `config/navigation.ts` e o texto
  "demonstração" do atalho do painel (`DashboardAtalhos`).

## Checklist ao ligar um módulo ao backend

1. Trocar o corpo de cada função de `api.ts` por chamadas ao `apiClient` e declarar os caminhos
   em `lib/api/endpoints.ts`.
2. Apagar `mocks.ts` (e `mocks.test.ts`, se o teste só cobre o mock).
3. Remover `<DemoNotice />`, `NavItem.demo` e o estado "demonstração" do atalho do painel.
4. Revisar `queries.ts`: paginação e ordenação passam a ser do servidor (`DataTable` já aceita
   `serverPagination` e `serverSorting`).
