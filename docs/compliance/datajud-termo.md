# DataJud — Termo de Uso e a decisão deste projeto

## O que é

A **API Pública do DataJud** (`api-publica.datajud.cnj.jus.br`) expõe os metadados
processuais que os tribunais enviam ao CNJ: classe, assuntos, órgão julgador, data de
ajuizamento e movimentos. É um Elasticsearch com uma chave de API pública, divulgada pelo
próprio CNJ na wiki do DataJud.

Não confundir com o **DJEN/Comunica**, de onde vêm as publicações (o texto do diário). São
serviços diferentes — ver `docs/integrations/cnj.md`.

## O que o Termo de Uso proíbe

Duas cláusulas decidem este caso:

| Cláusula | Proibição |
|---|---|
| 3.3 | Uso comercial da API |
| 3.8 | Explorar comercialmente **qualquer informação derivada** dos dados |

A 3.8 é a mais ampla: não basta não cobrar pela consulta — não se pode cobrar por um produto
construído sobre o que ela devolve.

E **consumir a API já implica aceitar os termos.** Não há cadastro, não há aceite explícito;
a primeira requisição é o aceite.

## A decisão

O Iustitia usa o DataJud **exclusivamente para fins de portfólio**: demonstração técnica, sem
comercialização.

Isso significa, em termos concretos:

- O sistema **não é vendido**, nem por assinatura, nem por licença, nem por uso.
- Não há cobrança por nenhuma funcionalidade que dependa desses dados.
- O uso por uma advogada, no escritório dela, é **teste de usabilidade do protótipo** — não
  prestação de serviço remunerada.

## O risco não é zero

Para uso não comercial o risco jurídico é **menor**, não inexistente:

- O CNJ pode revogar ou restringir o acesso a qualquer momento, sem aviso.
- A chave pública pode ser trocada sem aviso — e trocá-la é o único caminho de volta.
- A fronteira entre "portfólio" e "produto" é de interpretação. Um sistema em uso real por
  uma profissional, ainda que sem cobrança, é mais difícil de enquadrar como demonstração do
  que um sistema rodando só na máquina do desenvolvedor.

## O que muda se for comercializado

Se o Iustitia virar produto, **o DataJud sai** — não há como manter a integração e cumprir a
cláusula 3.8. As alternativas, em ordem de viabilidade:

1. **Provedor comercial** de dados processuais (Escavador, Judit, Digesto e similares), que
   já licencia os dados para uso comercial. É custo recorrente, e é o caminho normal.
2. **Autorização expressa do CNJ**, por convênio. Processo lento e de resultado incerto.
3. **Consulta direta aos sistemas dos tribunais** (PJe, Projudi, e-SAJ), caso a caso. Cada
   tribunal tem regra própria, e vários proíbem automação.

O que **não** é alternativa: raspagem dos portais. Os termos de uso dos tribunais em geral a
proíbem, e a troca seria de um problema jurídico por outro maior.

## Como a arquitetura protege essa troca

A porta `IConsultaProcessoTribunal` vive no módulo **Legal**, não no módulo de integração.
O DataJud fica atrás do adaptador `DataJudConsultaProcessoTribunal`. Trocar de fonte é
escrever outro adaptador — o handler, o endpoint e a tela não mudam.

Foi uma decisão deliberada justamente porque esta fonte é provisória. Ver **ADR-064**.

## Onde os avisos aparecem

Três lugares, e nenhum deve ser removido:

| Onde | Texto |
|---|---|
| `README.md` | Seção sobre uso do DataJud e não comercialização |
| Rodapé da aplicação (`AppLayout.tsx`) | "Projeto de portfólio. Não comercializado. Dados de processos obtidos do DataJud (CNJ) sob os termos de uso da API Pública." |
| `docs/compliance/portfolio.md` | O enquadramento completo do projeto |

## Medidas técnicas de contenção

Não são exigência do termo, mas são o que torna o uso defensável na prática:

- **Cache de 1 hora** por processo (Redis), no backend, compartilhado pelo escritório.
- **10 consultas/min por usuário** (`RateLimitingConfiguration.ExternalQueryPolicy`).
- **Uma consulta por vez**, disparada por clique — não há consulta em massa nem em lote.
- **Circuit breaker** que abre cedo: uma API pública instável não deve ser martelada.
- Nenhum payload de resposta é registrado em log (pode conter processo em segredo de justiça).

## Referências

- API Pública do DataJud: https://api-publica.datajud.cnj.jus.br
- Wiki do DataJud: https://datajud-wiki.cnj.jus.br/api-publica/
- ADR-064 — Integração com a API Pública do DataJud (uso portfólio)
