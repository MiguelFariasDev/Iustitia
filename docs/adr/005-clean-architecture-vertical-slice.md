# ADR-005: Clean Architecture + Vertical Slice

**Status:** Aceito

## Contexto

O sistema combina regras de negócio sensíveis (prazos jurídicos, revisão
humana obrigatória, auditoria) com integrações voláteis (Supabase, Azure,
Anthropic, CNJ/DJEN). É preciso que a lógica de domínio permaneça testável e
estável mesmo quando essas integrações mudam, e que múltiplos
desenvolvedores (ou agentes de IA) consigam localizar rapidamente todo o
código relacionado a um caso de uso específico sem navegar por camadas
técnicas dispersas.

Uma Clean Architecture "por camada horizontal" pura (todas as entidades
juntas, todos os handlers juntos) tende a espalhar um único caso de uso por
muitas pastas. Uma organização puramente "por feature" sem camadas
disciplinadas tende a acoplar domínio e infraestrutura.

## Decisão

Combinar as duas abordagens:

1. **Clean Architecture (camadas)** por módulo: `Domain` (entidades, value
   objects, eventos, regras puras, sem dependências externas) →
   `Application` (casos de uso via MediatR, interfaces) → `Infrastructure`
   (implementações concretas: EF Core, HTTP clients, Supabase) →
   `Api`/`Presentation`. A regra de dependência é sempre para dentro:
   Domain não conhece nada externo (validado por
   `tests/ArchitectureTests/LayerDependencyTests.cs`).

2. **Vertical Slice** dentro da camada Application: cada caso de uso vive em
   sua própria pasta sob `Features/{Grupo}/{CasoDeUso}/`, contendo
   Command/Query, Handler, Validator e Response juntos (ver
   `src/Platform/Platform.Application/Features`), em vez de agrupados por
   tipo técnico (todos os Commands numa pasta, todos os Handlers noutra).

## Consequências

**Positivas:**
- Domain permanece puro e testável sem mocks de infraestrutura.
- Cada caso de uso é fácil de localizar, entender e modificar isoladamente
  — importante tanto para humanos quanto para um agente de IA implementando
  features incrementalmente por fase.
- Testes de arquitetura automatizam a garantia da regra de dependência.

**Negativas / trade-offs:**
- Mais arquivos pequenos por caso de uso (Command, Handler, Validator,
  Response) em vez de um único arquivo — mitigado pela navegação por pasta.
- Exige disciplina para não vazar tipos de Infrastructure (ex.: `DbContext`)
  para dentro de Handlers de Application além do necessário via abstrações
  (`IRepository`, `IUnitOfWork`).
