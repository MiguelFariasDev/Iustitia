# ADR-071 — Prompts versionados em arquivos .md e testes de regressão de prompt

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.2

## Contexto

O prompt é parte da lógica do sistema: mudar uma frase muda classificações e prazos. Precisa de
versão, revisão em PR e testes — e a sugestão gravada precisa dizer **qual prompt a produziu**.

## Decisão

1. **Um arquivo `.md` por prompt** em `Workflow.Infrastructure/Prompts` (`classification.md`,
   `deadline.md`), com `version:` semântica no front matter e as seções `# SYSTEM` e `# USER`
   (placeholders `{raw_content}`, `{tribunal}`, `{area}`, `{act_type}`). Comentários HTML são
   ignorados. **Mudou o texto, sobe a versão** e roda a regressão.
2. **Embarcados no assembly** (`EmbeddedResource`), lidos por `EmbeddedPromptCatalog` — não
   dependem do diretório de trabalho do host nem de volume em container.
3. **Porta `IPromptCatalog` em `Integrations.Anthropic.Application`.** Os arquivos ficam no
   `Workflow.Infrastructure` (onde o prompt de revisão vai morar), mas o serviço que os usa está
   em `Legal.Infrastructure`; a regra do projeto proíbe um módulo de negócio depender da
   infraestrutura de outro. A porta, num projeto neutro, resolve sem criar a dependência.
4. **A versão vai para a sugestão** (`prompt_version`, `"<classificação>/<prazo>"`) e para
   `ai_usage_logs`.
5. **Regressão de prompt** em `tests/RegressionTests/Prompts`: 10 publicações de classificação e
   10 de prazo, contra a **API real**. `Category=PromptRegression`; **fora do CI** por custo
   (`dotnet test --filter Category=PromptRegression`); pulam sozinhos sem `ANTHROPIC_API_KEY`.
   Quando existir pipeline de testes, ele deve excluir a categoria (`Category!=PromptRegression`).
6. **Os textos de teste são sintéticos**, no formato do DJEN e com partes fictícias — o
   repositório não recebe conteúdo processual de cliente. Casos reais anonimizados podem ser
   acrescentados pelo escritório.

## Consequências

- (+) Auditável: dado uma sugestão, sabe-se o texto exato do prompt que a gerou (git + versão).
- (+) Trocar de modelo é configuração + rodar a regressão.
- (−) A regressão é manual: depende de alguém lembrar de rodá-la ao mudar prompt/modelo.
- (−) Testes com LLM real são probabilísticos; um caso pode oscilar. O critério é tendência, não
  um único vermelho — repetir antes de concluir que o prompt regrediu.
