# ADR-058: Referências visuais da concorrência (Astrea)

**Status:** Aceito — aplicado na Etapa 0.8

## Contexto

O público-alvo são advogados e secretariado de escritórios que já usam
software jurídico — Astrea, ADVBOX, Projuris, SAJ. Essas pessoas têm um
modelo mental formado: esperam uma barra lateral com os módulos, uma lista
de publicações com colunas específicas (divulgado em, tipo, processo,
diário), uma central de alertas e uma agenda semanal.

Inovar na disposição desses elementos não é diferencial: é atrito na
migração. Ao mesmo tempo, copiar um concorrente é problema jurídico e de
posicionamento — e não faz sentido para um produto que quer identidade
própria.

## Decisão

Usar o Astrea (e correlatos) como referência de **layout e fluxo de
trabalho**, com identidade visual, textos e marca inteiramente próprios.

**O que foi tomado como referência — estrutura e vocabulário do domínio:**

- Barra lateral com os módulos do escritório e contador de pendências
  (vermelho) em Publicações e Alertas.
- Publicações: cartões de resumo no topo ("não tratadas de hoje" e total,
  com tendência), filtros por status em chips, tabela com as colunas que o
  usuário já espera, texto da publicação expansível ("Ler mais" / "Expandir
  todos") e o caso real de "Processo não encontrado" com ação de busca.
- Alertas: abas Importantes / Todos / Publicações / Outros, com contagem, e
  ações por alerta.
- Agenda: grade semanal de domingo a sábado, régua de horas, blocos
  coloridos por tipo e destaque do dia atual.
- O vocabulário do domínio: "não tratada", "divulgado em", "diário",
  "nome pesquisado", "tratar andamento".

**O que é nosso:**

- Paleta, tipografia, logo, espaçamentos, raios e sombras (ADR-043).
- Todos os textos de interface, escritos do zero.
- A postura de produto sobre IA: nenhuma sugestão automática é aplicada sem
  revisão humana, e isso aparece na própria interface (selo "Sugestão de
  IA", aviso de onboarding, aceite explícito no cadastro).
- Nenhum ativo, ícone, código, captura de tela ou texto de terceiros foi
  copiado.

## Consequências

**Positivas:**
- Curva de aprendizado curta para quem vem de um concorrente: os módulos
  estão onde a pessoa procura.
- Termos do domínio corretos desde o início, o que reduz retrabalho de
  nomenclatura nas Fases 1 e 2.
- Os casos de borda que importam (publicação sem processo vinculado,
  andamento classificado como importante) já estão modelados na interface.

**Negativas / trade-offs:**
- Semelhança estrutural com o concorrente pode ser lida como falta de
  originalidade. Aceito conscientemente: em software jurídico, familiaridade
  vale mais que novidade visual, e a diferenciação do produto está na IA com
  revisão humana e na captura automática, não no desenho da tabela.
- Referência não é especificação. Vários detalhes do Astrea foram vistos
  apenas em capturas de tela, sem acesso ao comportamento real; onde houve
  dúvida, decidimos pelo que é acessível e testável (ex.: régua de horas
  marcada como decorativa, já que cada evento anuncia o próprio horário).
- Risco de copiar também os defeitos do concorrente. Mitigação: toda tela
  passa por auditoria WCAG 2.1 AA automatizada (axe-core) antes de ser dada
  como pronta.
