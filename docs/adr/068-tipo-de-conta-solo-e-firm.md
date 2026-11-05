# ADR-068: Tipo de conta (advogado solo × escritório) e acesso às telas

**Status:** Aceito — frontend e backend escritos; backend ainda não compilado neste ambiente
(ver "Pendências")

## Contexto

Todo cadastro era tratado como escritório: `Tenant` com CNPJ obrigatório, e todos os perfis
(`UserRole`: Owner, Partner, Lawyer, Intern, Admin, Financial) viam o mesmo menu. Dois problemas:

1. O advogado autônomo não tem CNPJ nem equipe, e hoje não consegue se cadastrar.
2. O menu mostra telas que a API recusaria (403). `Policies.LawyerOrAbove` é só
   Owner, Partner e Lawyer: estagiário, administrativo e financeiro recebem 403 em publicações,
   processos, clientes e alertas, mas viam todos esses itens.

## Decisão

### Solo é um Tenant, não um modo à parte

O advogado autônomo é um `Tenant` com `AccountType = Solo`, um único usuário (o Owner) e
identificado por **CPF** em vez de CNPJ. Isso mantém o isolamento por tenant (RLS, ADR-004)
exatamente como está: nenhuma tabela, policy ou filtro global muda. Tratar o solo como "usuário
sem tenant" obrigaria todo módulo a ter dois caminhos de isolamento.

- `Tenant.AccountType { Firm, Solo }`; tenants existentes viram `Firm` (valor padrão da coluna).
- `Tenant.Cnpj` passa a ser opcional e há `Tenant.Cpf` (value object `CPF`, com dígitos
  verificadores). Um e só um dos dois vale, conforme o tipo; `Tenant.CreateSolo` e
  `Tenant.Create` impõem isso. Índices únicos nos dois (Postgres aceita vários NULL).
- `POST /tenants` recebe `AccountType` ("Firm" por padrão, então clientes antigos não quebram) e
  `Cpf`. Cada tipo valida o seu documento e **ignora** o do outro.
- O CPF é dado pessoal: não entra em evento de domínio, `ToString()` não o expõe e a resposta
  de `GET /tenants/{id}` devolve só o tipo de conta, não o CPF.

### A claim `account_type`

O Custom Access Token Hook (`015_account_type_claim.sql`) emite `account_type` no JWT, ao lado de
`tenant_id` e `user_role` (ADR-029). Sem a claim (tenants antigos, hook não atualizado), o
frontend trata como `Firm`: é o comportamento que sempre existiu.

### Matriz de acesso às telas (frontend)

Uma única tabela, `web/src/lib/auth/access.ts`, usada pelo menu lateral, pela paleta de comandos,
pelas configurações e por uma guarda de rota (`AccessGuard`) que cobre quem digita o endereço.
Ela **espelha as policies da API**:

| Tela | Papéis |
|---|---|
| Área de trabalho, Agenda, Documentos, Perfil, Notificações, Integrações | todos |
| Publicações, Alertas, Processos, Clientes, Contatos, Atendimentos, Criação de peças | Owner, Partner, Lawyer |
| Financeiro | Owner, Partner, Financial |
| Indicadores, Dados do escritório, Auditoria | Owner, Partner |
| Usuários | Owner, Partner — **só conta Firm** |

Conta **Solo** vê tudo o que o titular vê, menos:
- **Usuários** (não há equipe);
- o filtro e o campo de **responsável** na agenda e nos processos (há uma pessoa só);
- "Escritório" vira **"Dados profissionais"**.

Papel desconhecido (claim ausente) só enxerga o que é de todos: negar por padrão.

### Isto é interface, não segurança

Esconder uma tela evita mostrar um menu que só responderia 403. Quem barra de verdade são as
policies da API e o RLS. A matriz é mantida por teste (`access.test.ts` lista cada tela por
papel): mudou uma policy no backend, o teste e a tabela acima são o que acusa a divergência.

### Cadastro

O assistente `/onboarding` ganha, na primeira etapa, a pergunta "Como você atua?" com as duas
opções. Solo: CPF obrigatório, sem nome de escritório (a conta leva o nome do titular), e a
pergunta de tamanho de equipe some (vale 1). A opção Solo só aparece com
`NEXT_PUBLIC_SOLO_SIGNUP=true` ou com os mocks ligados, para não oferecer ao usuário um cadastro
que um backend antigo recusaria.

## Consequências e pendências

- **Gerar a migration do EF** do `PlatformDbContext`
  (`dotnet ef migrations add AddTenantAccountType --project src/Platform/Platform.Infrastructure
  --startup-project src/Hosts/Api`): cria `account_type` (padrão `Firm`), `cpf` e torna `cnpj`
  anulável. Só depois aplicar `015_account_type_claim.sql` e atualizar o hook no Dashboard.
- **Não há conversão Solo → Firm** (um autônomo que abre escritório). É o próximo passo natural;
  hoje exigiria cadastrar outra conta.
- **Estagiário, administrativo e financeiro quase não têm acesso hoje**, porque a API só os
  admite em poucas rotas. A matriz reflete isso. Se o produto quer que o estagiário trabalhe
  em processos, a mudança é na policy da API (e depois na tabela), não só na tela.
- **A agenda aceita qualquer usuário autenticado** (`RequireAuthorization()` sem policy), então
  estagiário e financeiro leem eventos, inclusive os vinculados a processos. Decidir se isso é
  desejado; o mais coerente com o resto seria `LawyerOrAbove`.
- **Divergência anterior, não tratada aqui:** o assistente do frontend faz `signUp` no Supabase e
  depois chama `POST /tenants` com `ownerName`/`ownerEmail`, enquanto o comando do backend espera
  `adminName`/`adminEmail`/`adminPassword` e faz o próprio `SignUp`. Os dois fluxos precisam ser
  reconciliados antes de o cadastro funcionar de ponta a ponta fora dos mocks.
