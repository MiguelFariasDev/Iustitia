# Referências visuais

Decisão registrada em
[ADR-058](../adr/058-referencias-visuais-concorrencia.md).

## Postura

O público-alvo já usa software jurídico. Reposicionar os elementos que essas
pessoas esperam não é diferencial — é atrito na migração. Por isso o
**layout e o vocabulário** seguem a convenção do mercado, enquanto a
**identidade visual e os textos** são inteiramente próprios.

> Nenhum ativo, ícone, código, captura de tela ou texto de terceiros foi
> copiado para este repositório.

## Astrea — referência principal

Concorrente direto no mercado brasileiro. O que foi tomado como referência:

### Publicações

- Cartões de resumo no topo: "não tratadas de hoje" e total acumulado, este
  com mini gráfico de tendência.
- Filtro por status em chips, abrindo já filtrado pelo que exige ação.
- Colunas que o usuário já procura: divulgado em, tipo, processo, diário,
  nome pesquisado, status.
- Texto da publicação expansível por linha ("Ler mais") e em lote
  ("Expandir todos").
- O caso real de **"Processo não encontrado"**, com ação de iniciar a busca
  — é a situação cotidiana de quem recebe captura automática.

### Alertas

- Abas Importantes / Todos / Publicações / Outros, com contagem em cada uma.
- Card por alerta com ações diretas e descarte.
- Destaque para o que a IA classificou como relevante.

### Agenda

- Grade semanal de domingo a sábado, régua de horas na lateral.
- Blocos coloridos por tipo de atividade e destaque do dia atual.
- Filtro entre "todas as atividades" e "minhas atribuições".

### Navegação

- Barra lateral com os módulos do escritório, contador vermelho de
  pendências em Publicações e Alertas, suporte no rodapé.

### Vocabulário do domínio

"Não tratada", "divulgado em", "diário", "nome pesquisado", "tratar
andamento", "seccional da OAB". Termo errado aqui custa retrabalho nas
Fases 1 e 2.

## O que é nosso

- Paleta, tipografia, logo, espaçamentos, raios e sombras
  ([design-system.md](./design-system.md)).
- Todos os textos de interface, escritos do zero.
- A postura sobre IA, visível na própria interface: selo "Sugestão de IA",
  aviso de onboarding explicando a classificação automática e aceite
  explícito, no cadastro, de que toda sugestão exige revisão humana.
- Acessibilidade verificada (WCAG 2.1 AA com axe-core) — não há garantia de
  que a referência atenda a isso, então nenhuma decisão visual foi copiada
  sem passar pela auditoria.

## Outras referências

| Produto | O que observamos |
|---|---|
| Linear | Densidade de informação, paleta de comandos, atalhos |
| Vercel | Escala tipográfica, sutileza das sombras, tema escuro |
| Stripe | Clareza de formulários e mensagens de erro |

Usadas como referência de artesania de interface, não de estrutura.

## Limites

Boa parte do Astrea foi observada apenas em capturas de tela, sem acesso ao
comportamento real. Onde houve dúvida, decidimos pelo que é acessível e
testável — por exemplo, marcar a régua de horas como decorativa, já que cada
evento anuncia o próprio horário.
