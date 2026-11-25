# Juscraper — portais dos tribunais

Serviço HTTP (.NET 10) que consulta processos e andamentos direto nos portais públicos dos tribunais (e-SAJ e
a nova consulta pública do PJe). No Iustitia é a **fonte principal** da consulta de processo no cadastro e do
acompanhamento noturno de movimentações; o DataJud é o **fallback**. Decisão e regras: **ADR-075**.

| | |
|---|---|
| Base | configurável — `http://juscraper:8080/` no docker-compose, a URL interna no Azure Container Apps |
| Região | **Brazil South**: vários tribunais recusam acesso de fora do Brasil |
| Autenticação | header `X-Api-Key` (uma das chaves de `Juscraper__Api__ApiKeys` no serviço); sem chave válida, 401 |
| Formato | JSON camelCase; datas `yyyy-MM-dd`; data/hora ISO 8601 com fuso |
| OpenAPI | `GET /openapi/v1.json` |
| Health | `GET /health`, sem autenticação. **Não** entra na readiness do Iustitia (dependência externa, como o CNJ) |

## Onde está no código

```
Legal.Application/Abstractions/IConsultaProcessoTribunal       ← porta da consulta (cadastro)
Legal.Application/Abstractions/IAcompanhamentoProcessual       ← porta do acompanhamento
Legal.Infrastructure/Integrations/Juscraper/                    ← cliente HTTP, opções, adaptador da consulta
Legal.Infrastructure/Integrations/ConsultaProcessoTribunalComFallback  ← juscraper → DataJud (consulta)
Legal.Infrastructure/Integrations/Acompanhamento/               ← juscraper → DataJud (acompanhamento, 6.4)
Legal.Infrastructure/Jobs/AcompanhamentoMovimentacoesJob        ← job noturno (desligado por padrão)
Legal.Api/MovimentacoesEndpoints                                ← timeline, sincronizar, fila de avisos
```

## Configuração

```json
"Juscraper": {
  "BaseUrl": "http://juscraper:8080/",
  "ConsultaTimeout": "00:01:00",
  "AcompanhamentoTimeout": "00:03:00",
  "FallbackDataJud": true,
  "FallbackQuandoNaoEncontrado": false
},
"Jobs": {
  "AcompanhamentoMovimentacoes": {
    "Enabled": false, "CronExpression": "30 2 * * *",
    "JanelaDias": 3, "PausaEntreProcessosMs": 1500, "TenantIds": []
  }
}
```

- `BaseUrl` **vazio** (o padrão do `appsettings.json`) desliga o juscraper: só DataJud, como antes. Em
  desenvolvimento o `appsettings.Development.json` aponta para `http://localhost:8000/`; se o serviço não
  estiver no ar, a consulta cai no DataJud sozinha.
- `FallbackDataJud`: `false` se o produto for comercializado (Termo do DataJud, ADR-064).
- A **chave nunca vai no appsettings versionado**:

  ```bash
  # dev
  dotnet user-secrets set "Juscraper:ApiKey" "<uma das chaves do serviço>" --project src/Hosts/Api
  dotnet user-secrets set "Juscraper:ApiKey" "<a mesma>" --project src/Hosts/Worker
  # produção: Key Vault, segredo Juscraper--ApiKey
  ```

- O job só roda com `Jobs:AcompanhamentoMovimentacoes:Enabled=true` **no Worker**.

### Subir em desenvolvimento

```bash
export JUSCRAPER_API_KEY="$(openssl rand -hex 24)"
JUSCRAPER_PATH=~/caminho/para/juscraper docker compose -f infra/docker/docker-compose.yml --profile juscraper up -d juscraper
curl -s localhost:8000/health
dotnet user-secrets set "Juscraper:ApiKey" "$JUSCRAPER_API_KEY" --project src/Hosts/Api
```

## Endpoints usados

| Endpoint | Quem usa | Observação |
|---|---|---|
| `GET /v1/processos/{numero}?max_movimentacoes=10` | consulta do cadastro | metadados do 1º grau; movimentos de todas as instâncias, nome com o complemento (até 300 caracteres) |
| `GET /v1/processos/{numero}/movimentacoes?desde=yyyy-MM-dd` | acompanhamento | id estável por andamento; **nunca** com `max_movimentacoes` |
| `GET /v1/tribunais` | (ainda não usado) | cobertura: `validado`, `experimental`, `indisponivel` |

`{numero}` vai com os 20 dígitos (`CNJNumber.Digits`); o tribunal é derivado do número pelo próprio serviço.

## Erros (ProblemDetails)

| `codigo` | HTTP | Iustitia | Consulta | Acompanhamento |
|---|---|---|---|---|
| `NUMERO_CNJ_INVALIDO` | 400 | `CNJ_INVALID_REQUEST` | erro, sem fallback | erro |
| `TRIBUNAL_NAO_SUPORTADO` | 422 | `CNJ_TRIBUNAL_NOT_SUPPORTED` | DataJud | DataJud (1ª vez) |
| `CAPTCHA_EXIGIDO` | 503 | `CNJ_TRIBUNAL_NOT_SUPPORTED` | DataJud | DataJud (1ª vez) |
| `ACESSO_BLOQUEADO` | 503 | `CNJ_TRIBUNAL_NOT_SUPPORTED` | DataJud | DataJud (1ª vez) |
| `TRIBUNAL_INDISPONIVEL` | 502 | `CNJ_UNAVAILABLE` | DataJud | pula a noite |
| `RESPOSTA_INESPERADA` | 502 | `CNJ_INVALID_RESPONSE` | DataJud | pula a noite |
| `NAO_AUTORIZADO` | 401 | `CNJ_AUTH_FAILED` | DataJud + log Error | pula a noite + log Error |
| timeout / rede / circuito aberto | — | `CNJ_TIMEOUT` / `CNJ_UNAVAILABLE` | DataJud | pula a noite |

## Logs e privacidade

Nunca se loga o corpo de uma resposta de sucesso (partes e andamentos). Nos erros: tribunal, status HTTP e
código. Os `avisos` de resultado parcial (uma instância falhou) são mensagens do serviço e vão para o log como
Warning. O alerta ao advogado leva só o número do processo e a quantidade de andamentos; o texto do andamento
fica no banco, sob RLS.

## Cobertura (nov/2026, segundo o serviço)

| Situação | Tribunais |
|---|---|
| Validados | TJSP, TJAL, TJDFT |
| Experimentais | TJCE, TJAC, TJMS, TJAM |
| Indisponíveis | TJMG, TJBA, TJPE, TJMA, TJRO, TJPB, TJAP, TJES, TJRN, TJSC, TJRS |
| Sem scraper | os demais, inclusive TRFs, TRTs e tribunais superiores (o DataJud cobre) |

A lista muda pela configuração do serviço; não a fixe no código.

## Testes contra o serviço real

Opcionais (a CI não depende de rede). Com o juscraper no ar:

```bash
ADVOCACIA_TEST_JUSCRAPER_URL=http://127.0.0.1:8000 ADVOCACIA_TEST_JUSCRAPER_KEY=<chave> \
  dotnet test tests/UnitTests/Legal.Infrastructure.UnitTests --filter "FullyQualifiedName~JuscraperRealTests"
```

Consultam o processo público de exemplo do contrato (TJAL) e um do TRF1 (sem scraper → fallback).
