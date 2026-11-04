# ADR-069 — Cliente HTTP da Anthropic e auditoria de uso de IA (`ai_usage_logs`)

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.2

## Contexto

claude.md (seções 2 e 16) exige logar **toda** chamada à Anthropic (tokens, custo) e proíbe expor
conteúdo processual. O módulo `Integrations.Anthropic` ainda era só uma pasta vazia quando a
Etapa 3.2 começou, então esta etapa entregou o mínimo da integração de que precisava.

## Decisão

1. **Cliente HTTP customizado** (permitido pelo claude.md) sobre `POST /v1/messages`, com o mesmo
   pipeline de resiliência do CNJ (timeout, retry com jitter, circuit breaker; 429/5xx/529
   retentados, demais 4xx não). Porta `IAnthropicClient` devolve `Result`; mapeia HTTP para os
   códigos `INTEGRATION_*` do catálogo. Sem `temperature`/`top_p` (rejeitados pelos modelos
   atuais). `output_config.effort` configurável (padrão `medium`). Só os blocos `text` da
   resposta são lidos (com thinking ligado vêm também blocos `thinking`).
2. **Prompt caching** (ADR-024): o system prompt é enviado com `cache_control: ephemeral`; os
   tokens de escrita e leitura de cache são contados e precificados à parte.
3. **Custo por tabela de preços configurável** (`Anthropic:Pricing`, USD por milhão de tokens,
   chave = ID exato do modelo). Modelo sem preço ⇒ custo 0 com aviso; os tokens continuam sendo a
   fonte de verdade para reconciliar com a fatura.
4. **`ai_usage_logs`** (tabela do `LegalDbContext` nesta etapa; promover para a plataforma quando
   a Athena precisar): uma linha por chamada — finalidade, provedor, modelo, versão do prompt,
   publicação (`reference_id`), os quatro contadores de tokens, custo, latência, desfecho
   (`Succeeded`/`InvalidResponse`/`Failed`), código de erro e `request-id`. **Sem conteúdo**:
   nem a publicação nem a resposta do modelo. RLS por tenant; `app_user` só tem
   `SELECT, INSERT` (trilha append-only).
5. **Toda chamada é auditada, inclusive a de resposta inválida** — ela consumiu tokens e foi
   cobrada. Por isso quem audita é o serviço de IA (que conhece o desfecho), e não o job. A
   gravação é um `SaveChanges` próprio e imediato; falha ao gravar a auditoria **propaga** (uma
   chamada paga sem registro é pior que uma execução de job que falha e é retentada).
6. **Nunca logar prompt, publicação ou resposta** do modelo; os logs carregam modelo, status,
   tokens e `request-id`.
7. **Modelo:** `claude-sonnet-5-5` para classificação e prazo (claude.md: "Sonnet 4.5 ou mais
   recente"; ADR-025). O `claude-3-5-sonnet-20241022` do pedido original foi **aposentado** pela
   Anthropic e a API o recusaria. Trocar é configuração (`Anthropic:Models`) + regressão de prompt
   (ADR-068).
8. **Contrato de não-treinamento** (ADR-014) segue como pré-requisito de produção: a chave só
   entra por Key Vault (`Anthropic--ApiKey`).

## Consequências

- (+) Custo, latência e taxa de resposta inválida passam a ser consultáveis por escritório.
- (+) Resposta inválida cobrada fica visível e limitada (ADR-067).
- (−) Cliente próprio é código a manter; migrar para o SDK oficial .NET é possível sem mudar
  nenhuma porta.
- (−) A tabela de preços é manual e pode ficar desatualizada.
