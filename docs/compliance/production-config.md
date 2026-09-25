# Configuração de produção

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md).

## Azure Key Vault

`KeyVaultConfiguration.AddAdvocaciaKeyVault` (BuildingBlocks.Infrastructure/
Observability) adiciona o Key Vault como fonte de configuração quando
`KeyVault:Uri` está definido (normalmente via variável de ambiente em
produção/staging, nunca em appsettings versionado). Em dev local, sem essa
variável, é um no-op — os segredos continuam vindo do
`appsettings.Development.json`.

Segredos a armazenar no Key Vault (mapeados 1:1 para as chaves de
configuração que já existem no código, seguindo a convenção do Key Vault
provider do .NET: `--` no nome do segredo vira `:` na configuração):

| Segredo no Key Vault                        | Chave de configuração                     |
|----------------------------------------------|--------------------------------------------|
| `ConnectionStrings--DefaultConnection`         | `ConnectionStrings:DefaultConnection`        |
| `Supabase--Url`                                | `Supabase:Url`                               |
| `Supabase--AnonKey`                            | `Supabase:AnonKey`                           |
| `Supabase--ServiceRoleKey`                     | `Supabase:ServiceRoleKey`                    |
| `Messaging--AzureServiceBusConnectionString`   | `Messaging:AzureServiceBusConnectionString`  |
| `ApplicationInsights--ConnectionString`        | `ApplicationInsights:ConnectionString`       |
| `Anthropic--ApiKey` (Fase 3)                   | `Anthropic:ApiKey`                           |
| `Cnj--ApiKey` (Fase 1)                         | `Cnj:ApiKey`                                 |

## Managed Identity

`AddAdvocaciaKeyVault` usa `DefaultAzureCredential` (Azure.Identity) — em
produção, dentro de um Azure Container App com Managed Identity habilitada,
essa credencial resolve automaticamente, sem nenhuma credencial armazenada
em configuração/código. A Managed Identity do Container App precisa ter a
role `Key Vault Secrets User` (ou equivalente) atribuída no Key Vault —
provisionamento de infra (`infra/terraform`), fora do escopo de código.

## Variáveis de ambiente

| Variável                     | Exemplo                        | Observação |
|-------------------------------|----------------------------------|------------|
| `ASPNETCORE_ENVIRONMENT`      | `Production`                    | Seleciona `appsettings.Production.json` |
| `ASPNETCORE_URLS`              | `http://+:8080`                 | Porta que o Kestrel escuta dentro do container |
| `APP_NAME` / `AppName`         | `Iustitia`                       | Nome exibido na UI — a marca só existe aqui, nunca em namespace/recurso (ADR-023) |
| `KeyVault__Uri`                | `https://kv-advocacia-prod.vault.azure.net/` | Habilita `AddAdvocaciaKeyVault` |

## appsettings.Production.json

Ambos os Hosts (Api/Worker) têm um `appsettings.Production.json` com:
`Messaging:Transport = "AzureServiceBus"` (dev usa RabbitMQ — ver ADR-016),
níveis de log mais conservadores (`Warning` para `Microsoft`/`System`), e
os campos sensíveis (`ConnectionStrings`, `Supabase`, `ApplicationInsights`)
deixados como string vazia — são **sempre** sobrescritos pelo Key Vault em
produção real; a string vazia aqui é só para o schema ficar visível no
arquivo versionado, nunca um valor funcional.

## Dockerfile

`src/Hosts/Api/Dockerfile` e `src/Hosts/Worker/Dockerfile`: build multi-stage
(SDK para compilar, runtime/aspnet para rodar — a imagem final não carrega o
SDK), usuário não-root (`advocacia`, uid/gid 1500), `HEALTHCHECK` no Dockerfile
da Api apontando para `/health/live` (o Worker não expõe HTTP, então não tem
`HEALTHCHECK` próprio — a saúde dele se observa pelo heartbeat do Hangfire
nos logs). Testado nesta etapa: `docker build` + `docker run` de ambos,
Api respondendo `/health/live` com 200 dentro do container.
