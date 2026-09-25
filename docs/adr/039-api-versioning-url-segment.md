# ADR-039: API Versioning com URL Segment

**Status:** Aceito — implementado na Etapa 0.7

## Contexto

A API precisa evoluir (novos campos, novos endpoints, mudanças de contrato)
sem quebrar clientes existentes (frontend React, app iOS, integrações
futuras). Desde a Etapa 0.3, os endpoints já usavam o prefixo literal
`/api/v1/...`, mas isso era só uma convenção de nomenclatura — nada
validava a versão, nada reportava quais versões existem, e adicionar uma
v2 exigiria duplicar rotas manualmente sem nenhum suporte de framework.

## Decisão

Usar **URL Path Versioning** via `Asp.Versioning.Http` +
`Asp.Versioning.Mvc.ApiExplorer`:

- Toda rota de Platform passa a usar o template
  `/api/v{version:apiVersion}/<recurso>`, montado por um único ponto de
  entrada — `VersionedEndpointExtensions.MapVersionedGroup(prefix, tag)` —
  que já aplica `HasApiVersion(1)`, `ReportApiVersions()` e a tag do
  Swagger, para que nenhum endpoint precise repetir esse boilerplate.
- `ApiVersionReader = UrlSegmentApiVersionReader` (lê a versão do próprio
  segmento da URL, não de header/query string).
- `DefaultApiVersion = 1.0`, `AssumeDefaultVersionWhenUnspecified = true`,
  `ReportApiVersions = true` (toda resposta inclui o header
  `api-supported-versions`, mesmo sem o cliente pedir).
- Versão atual: **v1** (única existente). Uma v2 futura é só um novo
  `MapVersionedGroup` com `HasApiVersion(2)` num novo conjunto de
  endpoints — os endpoints v1 existentes não precisam mudar uma linha.

## Consequências

**Positivas:**
- Testado (Etapa 0.7): `/api/v1/health/live` responde 200 e inclui
  `api-supported-versions` no header; `/api/v2/...` (versão inexistente) é
  rejeitado antes do handler rodar.
- URL versioning é o padrão mais simples de consumir por qualquer cliente
  (React, iOS, curl) — não exige lidar com headers de versão.
- Todos os 71+ testes de integração pré-existentes continuaram passando
  sem alteração de asserção (só a implementação por trás de `/api/v1/...`
  mudou; a URL pública ficou idêntica).

**Negativas / trade-offs:**
- O Swagger gerado ainda expõe um único documento "v1" fixo
  (`ServiceCollectionExtensions.AddPlatformApi`) em vez de descobrir
  dinamicamente as versões via `IApiVersionDescriptionProvider` — like
  para quando existir v2, será preciso trocar para geração dinâmica de
  `SwaggerDoc` por versão (padrão documentado da própria
  Asp.Versioning.Mvc.ApiExplorer). Não fizemos isso agora porque só existe
  uma versão — não haveria nada para o código dinâmico realmente descobrir.
- O header `Location` retornado por `POST /api/v1/tenants` (201 Created)
  continua com `/api/v1/tenants/{id}` literal, não gerado a partir da rota
  versionada — aceitável enquanto só existir v1, mas precisa ser revisto
  junto com a v2.
