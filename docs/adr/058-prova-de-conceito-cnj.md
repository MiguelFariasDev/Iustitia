# ADR-058: Prova de Conceito da Captura do CNJ com Processos Reais

**Status:** Aceito — executado na Etapa 1.3

## Contexto

A FASE 1 construiu a captura de publicações do DJEN/CNJ (Etapas 1.1 e 1.2) com
testes de unidade e de integração — mas sempre contra um DJEN de mentira
(`FakeDjenServer`) ou contra uma amostra pequena da API real.

Testes com dublê provam que o **nosso** código faz o que pensamos. Não provam
nada sobre o que a API do CNJ realmente faz: qual o rate limit, se o contrato
documentado corresponde ao comportamento, se os dados chegam íntegros, se os
índices que projetamos servem as consultas em volume real.

As FASES 2 a 9 — todo o resto do produto — dependem dessa captura funcionar.
Investir nelas sem essa validação seria construir sobre uma suposição.

## Decisão

Executar uma prova de conceito contra a **API pública real do DJEN**, com:

- **7 processos reais**, em 4 tribunais (TJCE, TJRJ, TJMG, TJSP), escolhidos por
  terem histórico de publicações suficiente para validar a captura.
- **Medição direta da API** (latência, rate limit, taxa de erro) separada da
  medição do nosso código, para não confundir as duas.
- **Validação de qualidade** dos dados persistidos: HTML, encoding, truncamento,
  metadados, datas.
- **Validação de performance** com volume sintético de 50 mil publicações — sem
  volume, um `EXPLAIN ANALYZE` não diz nada, porque o planner escolhe seq scan
  corretamente em tabela pequena.
- **Validação de isolamento** por RLS com dois tenants, conectando como
  `app_user` (a role da aplicação), não como dono das tabelas.

### Fonte dos processos

Os processos são **reais e públicos**, obtidos do próprio DJEN — que é um diário
oficial de acesso público. **Nenhum é processo de cliente do escritório.**

Essa escolha é deliberada e não é apenas conveniência: usar processo de cliente
exigiria autorização expressa do escritório e traria dado sob sigilo
profissional para um ambiente de desenvolvimento, sem que isso acrescentasse
nada à validação técnica. A API não se comporta diferente conforme quem
acompanha o processo.

### Ambiente

A PoC roda **localmente** (Postgres/Redis/RabbitMQ em Docker) contra a **API
real do CNJ**. Supabase e Azure não foram provisionados e **não influenciam nada
do que está sendo medido**: o objeto da prova é a integração com o CNJ, não a
hospedagem nem o fluxo de autenticação.

O que fica genuinamente por validar em staging real está listado como pendência
em `docs/poc/cnj-poc-report.md`, sem ser mascarado como concluído.

## Consequências

**Positivas:**

- A PoC encontrou **três defeitos que nenhum teste com dublê encontraria**, dois
  deles graves e silenciosos (ver `docs/poc/cnj-poc-report.md`): o `count`
  mentiroso do DJEN que truncava a paginação, o rate limit não documentado, e um
  índice parcial com a coluna errada.
- As decisões das ADRs 056 (deduplicação) e 057 (índices) passaram a ter respaldo
  em medição, não em expectativa — e a 057 foi **corrigida** por causa disso.
- Existe agora uma linha de base numérica (latência, volume, tempo de captura)
  contra a qual regressões futuras podem ser comparadas.

**Negativas / trade-offs:**

- A medição é de um único ponto de rede, em um único horário, contra um serviço
  público cujo comportamento varia. Os números são uma amostra, não um SLA.
- O volume sintético aproxima a forma do dado real, mas não sua distribuição:
  textos sintéticos são uniformes, o que distorce a seletividade de índices de
  texto. Onde isso afeta a conclusão, está dito explicitamente no relatório.
- Sem Application Insights, não há medição de CPU/memória em condições de
  produção nem traces distribuídos — pendência declarada, não resolvida.

## Alternativas rejeitadas

- **Pular a PoC e ir para a FASE 2:** teria levado os três defeitos para dentro
  do produto. O do `count` só apareceria quando um advogado perdesse um prazo por
  uma publicação que o sistema silenciosamente não capturou.
- **PoC apenas com dublê, em staging:** não mediria nada sobre o CNJ, que é
  exatamente a fonte de risco desta fase.
