# Prova de Conceito CNJ — Lições Aprendidas

Etapa 1.3, setembro de 2026. Complementa o relatório executivo
([`cnj-poc-report.md`](cnj-poc-report.md)) e as métricas
([`cnj-poc-metrics.md`](cnj-poc-metrics.md)).

## A lição principal

**Três defeitos passaram por 920 testes automatizados e só apareceram na
primeira chamada à API real.** Nenhum deles era sutil; os três eram invisíveis
por construção:

| Defeito | Por que os testes não pegaram |
|---|---|
| `count` mentiroso truncando a paginação | O dublê devolvia `count` correto — porque foi escrito a partir da documentação, não da observação |
| Rate limit de 20 req/janela | Um dublê em memória não tem rate limit |
| Índice parcial com a coluna errada | Em 43 linhas o planner escolhe seq scan; o índice nunca foi exercitado |

O padrão comum: **o dublê codifica o que acreditávamos sobre a dependência.**
Testar contra ele confirma nossa crença, não a realidade. Isso não torna os
dublês inúteis — eles pegam regressão de lógica com rapidez e determinismo, e
continuam sendo a maioria da suíte. Mas eles **não substituem** tocar a
dependência real pelo menos uma vez, com volume, antes de confiar nela.

## O que funcionou bem

**A arquitetura resistiu.** Os três defeitos foram corrigidos sem mudar nenhuma
fronteira: `HasMorePages` é uma propriedade de um record da Application; o rate
limiter é um `DelegatingHandler` registrado no host; o índice é uma linha de
Fluent API mais uma migration. A porta `ICnjClient` isolou o módulo Legal de
tudo isso — nenhum arquivo de `Legal.Domain` ou `Legal.Application` foi tocado.

**A deduplicação por `ExternalId` (ADR-056) provou-se sob concorrência real.**
Duas instâncias do Worker capturaram simultaneamente por acidente e colidiram no
índice único (`23505`). O dado ficou íntegro. A decisão de colocar a garantia
definitiva no banco, e não na checagem em código, foi o que salvou.

**A captura por processo é claramente o modo certo para o produto.** 43/43 de
recall, 15 s, histórico completo — incluindo publicações de 2023, 3,5 anos fora
de qualquer janela de varredura. A varredura por tribunal é cara (14 mil
publicações/dia só no TJCE) e traz quase tudo que não interessa.

**Os índices parciais entregaram o prometido.** 40 KB e 56 KB — 0,1% do conjunto
de índices — servindo as duas consultas mais frequentes do produto.

## O que precisa melhorar

**A persistência é o gargalo, não o CNJ.** Medimos 0,11 req/s contra um teto de
4 req/s: a captura opera a 3% do que o CNJ permite. O tempo vai em 100 comandos
MediatR por página, cada um com transação e escrita de outbox próprias. Funciona
para um job noturno (~85 min para 4 tribunais), mas foi otimizado o lado errado
— gastamos esforço em resiliência de rede quando o custo estava no banco.

**O fake precisa nascer da observação, não da documentação.** `FakeDjenServer`
foi escrito a partir do contrato esperado. Agora replica o `count` mentiroso
(`ReportingPageSizeAsTotalCount`) porque medimos. Regra para as próximas
integrações: **escrever o dublê depois de uma sessão com a API real**, e tratar
cada comportamento estranho observado como um caso do dublê.

**Faltou observabilidade de negócio desde o início.** As métricas existentes
(`publications.count`, `duplicates.count`) não teriam detectado o defeito do
`count`: capturar 100 de 14.138 aparece como "100 publicações capturadas, zero
erros" — sucesso. Falta uma métrica de **cobertura**: capturado vs. disponível
na origem.

## O que mudou em relação ao esperado

| Esperávamos | Realidade |
|---|---|
| O CNJ seria instável e lento | 100% de sucesso, P50 de 0,47 s — mais confiável que o previsto |
| Rate limit provavelmente existiria, sem saber o valor | Existe, é baixo (20/janela), determinístico e **sem `Retry-After`** |
| `count` seria confiável | Mente exatamente no tamanho de página que escolhemos usar |
| Dígitos verificadores CNJ poderiam rejeitar dado real | **0 de 1.243 publicações rejeitadas** — a validação está correta |
| O gargalo seria a rede | É o banco, por uma ordem de grandeza |
| BRIN ajudaria em filtros por data | Nunca escolhido — o B-tree composto já cobre |

A inversão mais útil: **o risco não estava onde a Etapa 1.1 supôs.** Investimos
em retry, circuit breaker e timeout para um serviço que respondeu 100% das
requisições; o que quase quebrou o produto foi um campo JSON com valor errado.

## Recomendações para a FASE 2

1. **Persistir em lote por página.** Um `AddRange` + um `SaveChanges` por página,
   em vez de 100 comandos. É onde está a única melhoria de performance que vale
   fazer agora — potencial de 10× na varredura.
2. **Criar métrica de cobertura** (capturado ÷ disponível na origem) e alertar
   sobre queda. É a única métrica que teria detectado o defeito do `count`.
3. **Trocar a lista de processos do `appsettings` pelo cadastro real.** O
   `CnjCaptureJobOptions.CnjNumbers` é andaime da PoC; a Fase 2 deve resolver os
   processos de interesse por tenant, a partir da tabela `processes`.
4. **Vincular publicação a processo na captura.** Hoje `ProcessId` fica nulo. Com
   o cadastro existindo e a captura já sendo dirigida por número CNJ, o vínculo
   passa a ser direto — e a fila de revisão por processo passa a funcionar.
5. **Escrever o dublê da próxima integração depois de medir a API real.**
6. **Não escalar o Worker sem rever o rate limiter.** Ele é por processo; o
   limite do CNJ é por origem. `DisableConcurrentExecution` resolve hoje.
7. **Revisar o índice trigram sobre `raw_content`** (16 MB para 50 mil linhas)
   quando houver dado real em volume — pode não valer o custo se a busca
   full-text já atender.

## Uma nota sobre método

O maior valor desta etapa não foi a captura funcionar — foi **descobrir que ela
não funcionava** de um jeito que nenhum teste mostrava e que nenhum usuário
reportaria. Uma publicação não capturada não gera erro, não gera log e não gera
reclamação: ela simplesmente não existe, até o prazo vencer.

Em um sistema jurídico, o modo de falha que mais importa é o silencioso. A PoC
se pagou na primeira hora.
