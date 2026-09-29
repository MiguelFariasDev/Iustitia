# ADR-055: Módulo Legal como Bounded Context

**Status:** Aceito — implementado na Etapa 1.2

## Contexto

Até a Fase 0, todo o código de negócio vivia em `Platform.*`: identidade,
tenancy, auditoria, configurações, feature flags. Esse é o núcleo que
sustenta o sistema, não o domínio jurídico.

A Fase 1 introduz o primeiro conceito que é jurídico de verdade — a
**publicação** capturada de um diário oficial — e a Fase 2 trará clientes e
processos. Esses conceitos têm vocabulário, ciclo de vida e regras próprias
(número CNJ, prazo, revisão obrigatória por advogado), que não têm nada a
ver com o vocabulário da plataforma (tenant, usuário, papel, log).

Havia três caminhos:

1. Continuar em `Platform.*`, adicionando `Platform.Domain/Publications`.
2. Criar um módulo `Legal` com camadas próprias.
3. Extrair um serviço separado para o domínio jurídico.

## Decisão

Criar o módulo **Legal** como bounded context dentro do monolito modular
(ver ADR-001), com as quatro camadas do padrão adotado no projeto:

```
/src/Modules/Legal
  /Legal.Domain          -> agregados, value objects, eventos, specifications
  /Legal.Application     -> casos de uso (CQRS/MediatR), portas, mapeamentos
  /Legal.Infrastructure  -> LegalDbContext, repositórios, jobs
  /Legal.Api             -> endpoints (a partir da Fase 2)
```

Regras de dependência, verificadas por teste de arquitetura
(`LegalModuleConventionTests`):

- `Legal.Domain` → `BuildingBlocks.Domain` apenas.
- `Legal.Application` → `Legal.Domain` + `BuildingBlocks.Application`.
- `Legal.Infrastructure` → `Legal.Application` + `BuildingBlocks.Infrastructure`
  + **`Integrations.CNJ.Application`** (a porta, nunca a implementação).
- **Nenhuma camada de Legal depende de `Platform.*`.** O módulo consome a
  plataforma através de `BuildingBlocks`, não do núcleo de identidade.

Cada módulo tem seu **próprio DbContext sobre o mesmo banco físico**.
`LegalDbContext` herda de `BaseDbContext` (ganhando os filtros globais de
tenant e soft delete) e mapeia também `outbox_messages` — excluída das suas
migrations, porque a tabela pertence ao Platform — para que os eventos de
domínio de Legal sejam gravados na mesma transação do `SaveChanges` (ver
ADR-008).

Como consequência do DbContext próprio, Legal tem sua própria fronteira
transacional: `ILegalUnitOfWork`. O `IUnitOfWork` registrado por padrão
resolve para o `PlatformDbContext`, e um handler de Legal que o usasse
salvaria no contexto errado — silenciosamente.

## Consequências

**Positivas:**

- Vocabulário separado: `Publication`, `CNJNumber` e `PublishedAt` vivem
  onde fazem sentido, sem poluir o núcleo da plataforma.
- Testabilidade: `Legal.Domain` não tem nenhuma dependência de
  infraestrutura, então as regras de negócio (transições de status,
  validação de CNJ, normalização de conteúdo) são testadas sem banco, sem
  mock e sem container.
- Evolução independente: a Fase 2 (clientes, processos) e a Fase 3
  (classificação por IA) crescem dentro de `Legal` sem tocar em `Platform`.
- O limite é **executável**, não uma convenção documentada: os testes de
  arquitetura falham se alguém acoplar Legal a Platform ou à infraestrutura
  de outro módulo.

**Negativas / trade-offs:**

- Um DbContext por módulo significa que uma operação que atravesse dois
  módulos **não** compartilha transação automaticamente. Hoje isso não
  ocorre (Legal não escreve em tabelas do Platform), mas quando ocorrer
  precisará de coordenação explícita — ou, preferencialmente, de eventos via
  outbox, que é o mecanismo já adotado.
- O `TransactionBehavior` do MediatR (ver ADR-030) abre transação no
  `IUnitOfWork` padrão (Platform). Para comandos de Legal, isso resulta em
  uma transação vazia no `PlatformDbContext` além do `SaveChanges` atômico
  no `LegalDbContext`. A atomicidade que importa (publicação + outbox) está
  garantida, mas a transação extra é desperdício — o ponto natural para
  resolver isso é quando um segundo módulo de negócio entrar (Fase 2),
  fazendo o behavior enxergar os unit of works de todos os módulos.
- O mapeamento de `outbox_messages` existe em dois lugares
  (`OutboxMessageConfiguration` no Platform, `OutboxMessageMappingConfiguration`
  no Legal) e os dois precisam descrever a mesma tabela. Mudar a forma da
  outbox exige atualizar ambos.

## Alternativas rejeitadas

- **Manter em `Platform.*`:** o núcleo passaria a carregar regra jurídica e
  cresceria sem limite claro; a separação viraria apenas uma convenção de
  nomes de pasta, sem nada que a sustente.
- **Serviço separado:** custo operacional (deploy, rede, consistência
  distribuída) sem nenhum ganho para um produto de escritórios pequenos e
  médios. Ver ADR-001.
