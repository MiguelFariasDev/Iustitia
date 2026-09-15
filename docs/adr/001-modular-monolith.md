# ADR-001: Modular Monolith em vez de Microserviços

**Status:** Aceito

## Contexto

O Iustitia atende escritórios pequenos e médios (1 a 15 advogados). A equipe de
desenvolvimento é pequena nesta fase inicial, e o sistema precisa evoluir
rapidamente através de várias fases (Fase 0 a Fase 9) sem o overhead
operacional de uma arquitetura distribuída. Ao mesmo tempo, o domínio tem
fronteiras claras (Identity, Legal, Workflow, Notifications, Integrations,
Reporting, CRM, Documents, Athena) que merecem isolamento lógico para evitar
acoplamento excessivo entre módulos de negócio distintos.

Microserviços trariam custo de infraestrutura (orquestração, service mesh,
observabilidade distribuída, transações distribuídas) desproporcional ao
tamanho da equipe e ao estágio do produto.

## Decisão

Adotar um **Modular Monolith**: uma única solution/deploy (`Advocacia.sln`),
mas organizada em módulos com fronteiras de código explícitas
(`/src/Modules/{Identity,Legal,Workflow,...}`), cada um com suas próprias
camadas Domain/Application/Infrastructure/Api. Módulos se comunicam
preferencialmente por eventos de domínio/integração (in-process via MediatR,
ou assíncrono via Outbox + MassTransit), nunca acessando diretamente o
Domain interno de outro módulo.

A extração de um módulo para um serviço independente no futuro (se a escala
exigir) fica facilitada pela ausência de acoplamento direto entre Domains.

## Consequências

**Positivas:**
- Deploy único, mais simples de operar e depurar nesta fase.
- Transações ACID naturais dentro de um módulo (mesmo banco físico).
- Menor custo de infraestrutura (um Container App, não N serviços).
- Refatoração entre módulos ainda é possível sem contratos de rede.

**Negativas / trade-offs:**
- Exige disciplina de equipe para não vazar dependências entre módulos
  (mitigado por `ArchitectureTests` — ver `ModuleBoundaryTests.cs`).
- Escala horizontal é do monólito inteiro, não por módulo — aceitável para
  o público-alvo (escritórios pequenos/médios).
- Um bug em um módulo pode, em tese, afetar o processo inteiro (mitigado por
  boas práticas de tratamento de erro e testes).
