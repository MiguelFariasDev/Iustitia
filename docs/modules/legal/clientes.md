# Clientes

Pessoas físicas e jurídicas atendidas pelo escritório. Todo processo é cadastrado sob um
cliente — é o cliente que dá sentido ao caso.

## Modelo de domínio

`Cliente` é um **`AggregateRoot<ClienteId>`** que implementa `IAuditable`, `ISoftDeletable`
e `IHasTenant`.

| Campo | Tipo | Observação |
|---|---|---|
| `TenantId` | `Guid` | escritório dono do cadastro |
| `Tipo` | `TipoCliente` | `PessoaFisica` ou `PessoaJuridica` — **imutável** |
| `Nome` | `NomeCliente` | nome ou razão social, espaços colapsados |
| `Documento` | `Documento` | CPF ou CNPJ conforme o tipo — **imutável** |
| `Contato` | `Contato` | e-mail e telefone, ambos opcionais |
| `Endereco` | `Endereco` | todos os campos opcionais |
| `DataNascimento` | `DateOnly?` | só pessoa física, nunca no futuro |
| `Observacoes` | `string?` (2000) | anotações internas |
| `IsActive` | `bool` | desativado ≠ excluído |
| `NomeBusca`, `DocumentoBusca` | `string` | **colunas derivadas** — ver abaixo |

### Tipo e documento não são alteráveis

`Cliente.Update` não os aceita. Trocar o CPF de um cliente é cadastrar outro cliente, e
permitir isso silenciosamente reescreveria o vínculo de todos os processos já atribuídos a
ele. Cadastrou errado, cadastra de novo — atrito deliberado.

### Colunas derivadas de busca

`NomeBusca` (minúsculas) e `DocumentoBusca` (só dígitos) são mantidas em sincronia pelo
próprio agregado.

Existem porque `Nome` e `Documento` são value objects mapeados por `HasConversion`, e o EF
Core **não traduz** busca textual sobre valor convertido: `cliente.Nome.Value.Contains(x)`
compila e falha em runtime, e `EF.Property<string>(c, "Nome")` é pior — o EF aplica o
conversor ao *parâmetro* e tenta transformar o termo digitado em `NomeCliente`,
estourando `InvalidCastException`.

É denormalização deliberada: uma coluna a mais por uma busca indexável (pg_trgm, ver
`012_clientes_processos.sql`). `QueryTranslationTests` guarda contra a regressão.

### Value objects

**`Documento`** — CPF (11) ou CNPJ (14), com dígitos verificadores conferidos e sequências de
dígitos iguais recusadas. Ver **ADR-061**. `ToString()` devolve o documento **parcialmente
mascarado**: CPF é dado pessoal (LGPD) e `ToString()` é o caminho mais fácil para ele vazar
num log.

**`Contato`** — e-mail normalizado em minúsculas, telefone com 10 ou 11 dígitos (DDD
obrigatório). `ToString()` só informa presença/ausência, nunca os valores.

**`Endereco`** — nunca falha: endereço parcialmente preenchido é o normal. Persistido em
colunas separadas (não JSON) porque "clientes da comarca de X" é uma pergunta real.

## Desativação, não exclusão

`DELETE /api/v1/clientes/{id}` **desativa**. Um cliente com processo tem histórico que
precisa continuar consultável para prestação de contas e prazo prescricional; apagá-lo
levaria junto a referência de todo processo dele.

Um cliente inativo continua aparecendo nos processos e no histórico — só deixa de ser
oferecido no cadastro de processo novo.

## Endpoints

| Método | Rota | Policy |
|---|---|---|
| `POST` | `/api/v1/clientes` | `LawyerOrAbove` |
| `GET` | `/api/v1/clientes` | `LawyerOrAbove` |
| `GET` | `/api/v1/clientes/{id}` | `LawyerOrAbove` |
| `PUT` | `/api/v1/clientes/{id}` | `LawyerOrAbove` |
| `DELETE` | `/api/v1/clientes/{id}` | `PartnerOrAbove` |
| `POST` | `/api/v1/clientes/{id}/reactivate` | `PartnerOrAbove` |

Desativar exige `PartnerOrAbove`: tirar um cliente de circulação afeta os processos dele.

O documento sai **mascarado** nas respostas; o valor cru não deixa a API.

## Índices e isolamento

| Índice | Serve |
|---|---|
| `ux_clientes_tenant_documento` UNIQUE, `WHERE is_deleted = false` | um documento por escritório |
| `ix_clientes_tenant_ativo` parcial | listagem padrão (só ativos) |
| `ix_clientes_tenant_nome` | ordenação alfabética |
| `ix_clientes_nome_trgm` (GIN) | busca fuzzy por nome |

O índice único é **parcial**: um cliente excluído logicamente não impede o recadastro da
mesma pessoa. A unicidade é **por escritório** — dois escritórios podem atender a mesma
pessoa.

RLS em `clientes` com `USING` e `WITH CHECK` separados
(`012_clientes_processos.sql`), usando `NULLIF(current_setting(...), '')` — ver
`publications.md` para o porquê do `NULLIF`.

## Telas

| Rota | Conteúdo |
|---|---|
| `/clientes` | listagem paginada, busca por nome ou documento, filtro por tipo e por inativos |
| `/clientes/novo` | cadastro |
| `/clientes/[id]` | detalhe, com desativar/reativar |
| `/clientes/[id]/editar` | edição (tipo e documento desabilitados) |

A busca aceita nome ou documento: 3 dígitos ou mais são tratados como documento (prefixo),
o resto como nome (contém). A máscara é aplicada enquanto se digita e o valor é enviado
só com dígitos.

## Códigos de erro

Faixa **CLIENT (1100–1199)** — ver `docs/errors/catalog.md`.
