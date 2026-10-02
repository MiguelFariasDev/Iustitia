# Processos

Processo judicial acompanhado pelo escritório. É o agregado que **dirige a captura de
publicações**: o que o sistema busca no diário oficial é exatamente o conjunto de processos
cadastrados aqui — nada além disso entra na base.

## Modelo de domínio

`Processo` é um **`AggregateRoot<ProcessoId>`** que implementa `IAuditable`, `ISoftDeletable`
e `IHasTenant`.

| Campo | Tipo | Observação |
|---|---|---|
| `TenantId` | `Guid` | escritório dono do processo |
| `CnjNumber` | `CNJNumber` | número único, mascarado — **imutável** |
| `CnjDigits` | `string` | **coluna derivada**, 20 dígitos sem máscara |
| `ClienteId` | `Guid` | referência por id, nunca por objeto |
| `ResponsavelUserId` | `Guid` | usuário da Platform, por id |
| `Area` | `AreaDireito` | Cível, Trabalhista, Criminal, … , Outros |
| `Status` | `StatusProcesso` | `Ativo`, `Suspenso`, `Encerrado`, `Arquivado` |
| `Foro` | `DadosForo` | tribunal, comarca, vara, órgão julgador |
| `Assunto`, `ParteContraria`, `Observacoes` | `string?` | normalizados (trim, vazio vira `null`) |
| `ValorCausa` | `decimal?` | nulo quando inestimável; nunca negativo |
| `DataDistribuicao` | `DateOnly?` | |

### Referências por id, não por navegação

`ClienteId` e `ResponsavelUserId` são `Guid`. Cliente é **outro agregado** (carregar o objeto
inteiro para validar um vínculo arrasta uma transação maior do que a regra precisa), e o
usuário responsável pertence ao módulo **Platform**, que o módulo Legal não conhece
(ADR-055). A composição acontece na camada de aplicação e na tela.

### O número CNJ não é alterável

`Processo.Update` não o aceita. É a identidade do processo perante o Judiciário e a chave
pela qual as publicações já capturadas foram vinculadas; trocá-lo orfanaria essas
publicações sem deixar rastro. Ver **ADR-062**.

`CnjDigits` é derivado e mantido pelo próprio agregado, pelo mesmo motivo de
`Cliente.NomeBusca` (o EF Core não traduz busca textual sobre value object convertido) — e
porque é o formato que o DJEN exige em `numeroProcesso`, então o job usa a coluna direto.

### `DadosForo`

Value object com tribunal, comarca, vara e órgão julgador, todos opcionais: no cadastro
manual raramente se tem os quatro, e exigi-los travaria o cadastro de um processo que já
precisa ser acompanhado hoje.

## Situação e captura

```
Ativo ⇄ Suspenso ⇄ Encerrado  →  Arquivado (terminal)
```

`ChangeStatus` recusa transição para o mesmo status e qualquer saída de `Arquivado`
(`PROCESS_ALREADY_ARCHIVED`).

```csharp
public bool GeraCaptura => !IsDeleted && Status is StatusProcesso.Ativo or StatusProcesso.Suspenso;
```

**Suspenso continua sendo consultado.** Um processo sobrestado volta a publicar sem aviso, e
deixar de consultá-lo é exatamente como se perde um prazo. `Encerrado` e `Arquivado` saem da
captura.

Essa propriedade tem par no banco — `ProcessosParaCapturaSpecification` e o índice parcial
`ix_processos_tenant_captura` filtram pelos mesmos dois status. Divergir os dois é o tipo de
bug que não aparece em teste e aparece em prazo perdido; `CapturaDirigidaPorProcessoTests`
guarda a equivalência.

## Captura dirigida por processo

O `CnjCaptureJob` resolve seus alvos a partir de
`ProcessoRepository.ListParaCapturaAsync`, por tenant. Não há varredura de tribunal ligada
por padrão — ela existe apenas como ferramenta de diagnóstico, desabilitada em produção.

A consequência prática: **a base só contém publicações de processos do escritório**. Ver
`cnj-capture.md` para o fluxo completo e `publications.md` para o vínculo
publicação → processo.

## Endpoints

| Método | Rota | Policy |
|---|---|---|
| `POST` | `/api/v1/processos` | `LawyerOrAbove` |
| `GET` | `/api/v1/processos` | `LawyerOrAbove` |
| `GET` | `/api/v1/processos/{id}` | `LawyerOrAbove` |
| `PUT` | `/api/v1/processos/{id}` | `LawyerOrAbove` |
| `PATCH` | `/api/v1/processos/{id}/status` | `LawyerOrAbove` |
| `POST` | `/api/v1/processos/{id}/archive` | `PartnerOrAbove` |

Arquivar exige `PartnerOrAbove`: é terminal e tira o processo da captura.

A listagem filtra por situação, área, cliente e responsável, e busca por número CNJ
(prefixo de dígitos) ou parte contrária.

## Índices e isolamento

| Índice | Serve |
|---|---|
| `ux_processos_tenant_cnj` UNIQUE, `WHERE is_deleted = false` | um processo por número, por escritório |
| `ix_processos_tenant_captura` parcial (`Ativo`, `Suspenso`) | o job de captura |
| `ix_processos_tenant_cliente` | processos de um cliente |
| `ix_processos_tenant_responsavel` | carteira de cada advogado |
| `ix_processos_tenant_area_status` | filtros combinados da listagem |
| `ix_processos_cnj_digits_trgm`, `ix_processos_parte_contraria_trgm` (GIN) | busca parcial |

O índice único é **parcial** e **por escritório**: dois escritórios podem acompanhar o mesmo
processo (polo ativo e passivo), e um processo excluído logicamente não impede o recadastro.

RLS em `processos` com `USING` e `WITH CHECK` separados (`012_clientes_processos.sql`).

## Telas

| Rota | Conteúdo |
|---|---|
| `/processos` | listagem paginada, busca, filtros de situação e área |
| `/processos/novo` | cadastro, com seleção de cliente ativo |
| `/processos/[id]` | detalhe, com mudança de situação e arquivamento |
| `/processos/[id]/editar` | edição (número CNJ desabilitado) |

O número CNJ é validado **enquanto se digita** (ADR-062): recusá-lo no formulário é a única
chance de evitar um processo que nunca receberá publicação nenhuma.

O contrato do frontend trata status desconhecido como `Ativo` e área desconhecida como
`Outros` — um enum novo no backend não pode quebrar a tela nem esconder o processo da lista.

## Códigos de erro

Faixa **PROCESS (1200–1299)** — ver `docs/errors/catalog.md`.
