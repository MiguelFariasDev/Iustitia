# Convenções do Catálogo de Erros

> Ver ADR-032 para o racional completo da decisão de centralizar o catálogo.

## Nomenclatura: `GRUPO_DESCRICAO`

Todo `ErrorCode` segue o padrão `GRUPO_DESCRICAO`, maiúsculo, com `_` como
separador (ex.: `TENANT_NOT_FOUND`, `VALIDATION_REQUIRED_FIELD`). O prefixo
antes do primeiro `_` corresponde ao grupo (ver `groups.md`) — não
literalmente ao valor do enum `ErrorGroup` (que é PascalCase, ex.:
`FeatureFlags`), mas ao nome usado nos códigos daquele grupo (ex.:
`FEATURE_FLAG_*`, no singular, para os códigos individuais de uma flag).

Um teste de arquitetura (`ErrorCatalogTests.Definition_CodeString_
FollowsGroupDescricaoConvention`, em `BuildingBlocks.Domain.UnitTests`)
garante que todo `ErrorDefinition.Code` é uma sequência de letras
maiúsculas e `_`.

## Faixas numéricas por grupo

Cada grupo tem uma faixa reservada de 100 (ou 1000, para `Internal`)
valores — ver a tabela completa em `groups.md`. Ao adicionar um código
novo dentro de um grupo já existente, use o próximo valor livre dentro da
faixa dele; nunca reaproveite um valor de outro grupo.

## Como escolher o grupo

1. O erro é sobre uma entidade/conceito de negócio específico já com um
   grupo próprio (Tenant, User, Audit, Settings, FeatureFlags)? Use esse
   grupo.
2. É sobre autenticação (identidade, token, sessão)? `Auth`.
3. É sobre "você não pode fazer isso" por papel/policy, mas não é
   específico de uma entidade? `Authorization`.
4. É sobre formato/forma de um campo de entrada, detectado pelo
   `ValidationBehavior`? `Validation` — nunca misture validação de forma
   com regra de negócio (isso é `Common`/grupo específico).
5. É sobre uma dependência externa (Supabase, CNJ, Anthropic, ...) que
   falhou de um jeito genérico (indisponível, timeout, autenticação,
   rate limit, resposta inesperada)? `Integration`.
6. É genuinamente inesperado (bug, infra) e não se encaixa em nenhum
   conceito de negócio? `Internal` — normalmente vem de uma exceção não
   tratada capturada por `ExceptionHandlingMiddleware`, não de um
   `Result.Failure` deliberado.
7. Não se encaixa em nada acima, mas é um conceito realmente genérico
   ("não encontrado", "já existe", "operação inválida no estado atual")
   sem uma entidade de negócio clara por trás? `Common` — mas prefira um
   grupo mais específico sempre que possível; `Common` é o fallback, não
   o padrão.

## Como escolher o `ErrorType` (e o `HttpStatus` correspondente)

`ErrorType` (`BuildingBlocks.Domain.Results.ErrorType`) tem seis valores,
cada um com um HTTP status fixo (a mesma tabela usada em
`ResultExtensions.ToHttpResult`):

| `ErrorType` | HTTP Status | Quando usar |
|---|---|---|
| `Validation` | 400 | Entrada malformada (formato, obrigatoriedade, tamanho, enum). |
| `Unauthorized` | 401 | Falta de identidade válida (não autenticado, token inválido/expirado). |
| `Forbidden` | 403 | Identidade válida, mas sem permissão para a ação. |
| `NotFound` | 404 | Recurso não existe (ou não existe *para este tenant/usuário* — nunca revele que existe em outro tenant). |
| `Conflict` | 409 | Estado atual do recurso impede a operação (já existe, já está nesse estado, transição inválida). |
| `Failure` | 500 | Qualquer coisa que não se encaixe nos cinco acima — inclusive falhas de integração/infraestrutura que não têm um HTTP status mais específico disponível nesta tabela (ver ADR-032, "Negativas/trade-offs", sobre não introduzir 503/504 aqui). |

Nunca introduza um HTTP status fora dessa tabela de seis valores em uma
nova `ErrorDefinition` — mantenha a tabela como a única fonte de mapeamento
`ErrorType → HttpStatus`.

## Mensagens

- Sempre em pt-BR, amigáveis, sem jargão técnico nem stack trace.
- Podem usar placeholders posicionais (`{0}`, `{1}`, ...) para os `args`
  de `ErrorFactory.From(code, args)` — útil quando o mesmo código serve
  vários call sites que precisam citar um campo/valor específico (ex.:
  `VALIDATION_REQUIRED_FIELD` → `"O campo '{0}' é obrigatório."`).
- Nunca inclua dados sensíveis na mensagem (senha, token, dados pessoais
  além do estritamente necessário para o usuário entender o erro).

## Nunca hardcodar strings de erro fora do catálogo

Em `Platform.Application` e `Platform.Domain`, toda construção de `Error`
para uma falha de negócio passa por `ErrorFactory.From(ErrorCode.X, ...)`
— nunca `Error.Validation("codigo.livre", "mensagem")`/`Error.NotFound(...)`/
etc. diretamente. Um teste de arquitetura
(`ErrorCatalogConventionTests.PlatformApplicationSourceFiles_
NeverConstructErrorDirectly` e o equivalente para `Platform.Domain`) varre
o código-fonte procurando por essas chamadas proibidas.

**Exceção deliberada:** `Platform.Infrastructure.Auth.SupabaseAuthService`
continua construindo `Error.X(...)` diretamente ao repassar respostas de
erro do Supabase Auth — a mensagem vem de uma API externa em tempo real,
não é um texto fixo que caiba num molde do catálogo (ver ADR-032, seção
"Escopo").
