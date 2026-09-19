# Grupos de Erro

Cada `ErrorCode` pertence a um `ErrorGroup` (`BuildingBlocks.Domain/Errors/ErrorGroup.cs`)
e a uma faixa numérica fixa (`BuildingBlocks.Domain/Errors/ErrorCode.cs`). Ver
`conventions.md` para como escolher grupo/faixa ao adicionar um código novo.

## Grupos implementados (Etapa 0.4)

| Grupo | Faixa | Descrição |
|---|---|---|
| `Common` | 000–099 | Erros genéricos que não pertencem a nenhum domínio específico (não encontrado, já existe, operação inválida, timeout, indisponível). |
| `Validation` | 100–199 | Falhas de validação de forma/formato de entrada (FluentValidation), nunca regra de negócio. |
| `Auth` | 200–299 | Autenticação: credenciais, tokens, confirmação de e-mail, MFA, bloqueio/desativação de conta. |
| `Authorization` | 300–399 | Autorização: papel insuficiente, tenant não identificado, violação de policy. |
| `Tenant` | 400–499 | Escritório (Tenant): não encontrado, CNPJ duplicado/inválido, suspenso/cancelado. |
| `User` | 500–599 | Usuário: não encontrado, e-mail duplicado/inválido, OAB inválida, papel inválido, estado ativo/inativo, convite, regras de remoção do último Owner. |
| `Audit` | 600–699 | Registro de auditoria: não encontrado, imutabilidade. |
| `Settings` | 700–799 | Configuração por tenant: chave não encontrada/inválida, valor inválido. |
| `FeatureFlags` | 800–899 | Feature flag por tenant: não encontrada, chave inválida/duplicada. |
| `Backup` | 900–999 | Falhas no processo de backup (execução, criptografia, upload). |
| `Integration` | 1000–1099 | Falhas de integrações externas (indisponibilidade, timeout, autenticação, rate limit, resposta inválida). |
| `Internal` | 9000–9999 | Erros internos genuinamente inesperados (bug, banco de dados, serialização) — normalmente originados de uma exceção não tratada, não de um `Result.Failure` deliberado. |

## Grupos reservados (fases futuras — nenhum `ErrorCode` implementado ainda)

Faixas reservadas para não colidir quando esses módulos forem implementados.
Nenhuma dessas faixas tem código algum hoje — só a reserva numérica.

| Grupo | Faixa | Fase prevista |
|---|---|---|
| `Client` | 1100–1199 | Fase 2 |
| `Process` | 1200–1299 | Fase 2 |
| `Publication` | 1300–1399 | Fase 2 |
| `Task` | 1400–1499 | Fase 3 |
| `Review` | 1500–1599 | Fase 3 |
| `AI` | 1600–1699 | Fase 3 |
| `CNJ` | 1700–1799 | Fase 1 |
| `Document` | 1800–1899 | Fase 4 |
| `Signature` | 1900–1999 | Fase 4 |
| `Calendar` | 2000–2099 | Fase 4 |
| `CRM` | 2100–2199 | Fase 4 |
| `Financial` | 2200–2299 | Fase 8 |
| `Athena` | 2300–2399 | Fase 5 |
| `Api` | 2400–2499 | Fase 5 |
