# Como adicionar um novo código de erro

Guia passo a passo — ver `conventions.md` para os critérios de decisão
usados em cada passo, e `catalog.md` para ver o resultado final (a lista
completa e navegável de códigos).

## Passo a passo

1. **Escolha o grupo.** Existe um `ErrorGroup` que já cobre o conceito
   (Tenant, User, Auth, ...)? Use-o. Se não, e o novo grupo pertence a um
   módulo de negócio futuro (Legal, Workflow, ...), pode já estar
   reservado em `groups.md` — confira antes de inventar um novo.

2. **Escolha a faixa numérica.** Use o próximo valor livre dentro da
   faixa reservada do grupo (ver `groups.md`). Nunca reaproveite um valor
   já usado por outro `ErrorCode`, mesmo que ele pareça "desativado".

3. **Adicione o valor no enum `ErrorCode`**
   (`src/BuildingBlocks/BuildingBlocks.Domain/Errors/ErrorCode.cs`), na
   seção do grupo correspondente, seguindo o padrão `GRUPO_DESCRICAO`:

   ```csharp
   // USER (500-599)
   ...
   USER_PHONE_INVALID = 511,
   ```

4. **Adicione a definição em `ErrorCatalog`**
   (`src/BuildingBlocks/BuildingBlocks.Domain/Errors/ErrorCatalog.cs`):

   ```csharp
   [ErrorCode.USER_PHONE_INVALID] = new(ErrorGroup.User, nameof(ErrorCode.USER_PHONE_INVALID),
       "Número de telefone inválido.", 400, ErrorType.Validation),
   ```

   Escolha `ErrorType`/`HttpStatus` seguindo a tabela de `conventions.md`
   — nunca um HTTP status fora dos seis valores já usados.

5. **Use no handler (ou Value Object)** via `ErrorFactory.From`:

   ```csharp
   return Result.Failure<User>(ErrorFactory.From(ErrorCode.USER_PHONE_INVALID));
   ```

6. **Atualize `docs/errors/catalog.md`** com a nova linha na tabela do
   grupo correspondente.

7. **Adicione um teste** cobrindo o novo código, no mínimo:
   - No handler/Value Object que agora o retorna (ver os `*HandlerTests.cs`/
     `*Tests.cs` já existentes como referência de padrão).
   - Os testes gerais do catálogo (`ErrorCatalogTests`,
     `ErrorCatalogConventionTests`) já cobrem automaticamente qualquer
     `ErrorCode` novo (via `Enum.GetValues<ErrorCode>()`), sem precisar de
     alteração manual neles.

## Checklist rápido

- [ ] Valor adicionado em `ErrorCode` (grupo certo, faixa numérica livre).
- [ ] Definição adicionada em `ErrorCatalog` (mensagem em pt-BR, `ErrorType`/`HttpStatus` corretos).
- [ ] Call site usa `ErrorFactory.From(ErrorCode.X, ...)`, nunca `Error.X("...", "...")` direto.
- [ ] `docs/errors/catalog.md` atualizado.
- [ ] Teste cobrindo o novo código.
- [ ] `dotnet test` passando (inclui os testes de convenção do catálogo).
