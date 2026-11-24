# O que falta para produção — e como o Iustitia se compara ao Astrea

**Data:** 2026-11-24 · Complementa `relatorio-lacunas-2026-11.md` (que continua valendo; aqui o foco é "produto vendável").
**Base do código:** `origin/master` em `bbcbbce` + as branches de design (`feat/design-system-iustitia`).
Existe também a branch `feature/drafting-rag-juridico` ("RAG jurídico por caso"), **não auditada**.

**Sobre os concorrentes.** Só consegui confirmar, por fontes públicas, o que consta abaixo para o **Astrea**. Para Projuris, Legal One e
Advbox encontrei pouco além de preço e público-alvo; onde não confirmei, está escrito. Preços mudam e divergem entre páginas: confira
nas páginas oficiais antes de decidir.

## 1. Veredito

O Iustitia **não está pronto para produção paga**. Está num ponto em que um **piloto fechado e gratuito** com um ou dois escritórios é
possível, desde que os bloqueios da seção 3 (Gate 0 e Gate 1) sejam resolvidos antes. A distância para o Astrea não está na interface
(o design system aplicado já está num nível comparável), e sim em **dados confiáveis do tribunal, notificação entregue, financeiro,
app completo e a operação de um SaaS** (cobrança, suporte, termos, confiabilidade).

## 2. Benchmark: Astrea

Fatos do Astrea (Aurum), conforme as fontes ao final:

- Público: advogado autônomo e escritório de 1 a 30 advogados. O produto para grandes bancas e departamentos jurídicos é outro (Themis).
- **Monitoramento automático de processos**, com limite por plano (o plano Light cita até 40 processos).
- **Publicações e intimações** recebidas por nome (módulo de Publicações); na mesma tela: ler, cadastrar o processo, agendar o prazo e
  atribuir ao responsável.
- Agenda, kanban e atividades; publicações viram tarefas e compromissos com poucos cliques.
- **Financeiro**: honorários, despesas, lançamentos, timesheet, faturas detalhadas, relatórios.
- **Área do cliente** (citada por guia de terceiros) e e-mail ao cliente quando há movimentação (relato de usuário).
- **App iOS e Android** em todos os planos, com notificação de publicações.
- **IA**: geração de peças, classificação de andamentos, tratamento de publicações, "tradução de juridiquês"; revisão humana recomendada.
  Módulo de indicadores recente.
- Plano gratuito limitado e período de teste; plano pago citado a partir de R$ 379/mês no anual (valor instável entre páginas).
- Segundo a própria Aurum: **sem API pública, sem CRM de vendas nativo e não é um ERP com controladoria profunda.**

Não confirmei no Astrea: integração com Google Agenda e assinatura eletrônica. Para Advbox só confirmei que os planos permitem integração
com IA e que o plano para grandes escritórios parte de R$ 5.000/mês.

### Paridade com o Astrea

| Capacidade | Astrea | Iustitia hoje | Distância |
|---|---|---|---|
| Publicações/intimações | por nome, monitoramento contínuo | captura DJEN, triagem, sugestão de IA, revisão humana (`Legal`) | **Média**: a fonte de dados é frágil (ver Gate 0) e o escopo de tribunais não está medido |
| Monitoramento de processos e movimentações | sim, com limite por plano | **não existe** entidade de movimentação; DataJud só preenche cadastro | **Alta** |
| Publicação → prazo → responsável | mesma tela | revisão da IA abre o evento pré-preenchido; criação no backend não existe | Média |
| Cálculo de prazo | n/c (não confirmei) | só dias úteis no navegador, sem feriados/recesso | **Alta** (risco de responsabilidade) |
| Agenda, kanban, atividades | sim | agenda, tarefas, Kanban e etiquetas existem | **Baixa** (falta prioridade, comentários, tempo) |
| Financeiro e honorários | sim, com timesheet e faturas | tela salva **só no navegador**, sem servidor | **Alta** |
| Área do cliente | sim | não existe | **Alta** |
| App móvel | iOS e Android | Flutter parcial (agenda, processos, clientes, perfil); sem publicações | Média |
| IA de peças | sim | módulo completo, **desligado e não validado** (`Drafting:Enabled=false`) | Média |
| Notificação por e-mail/push | sim | **e-mail é stub; push vazio** | **Alta** |
| Indicadores | sim | tela de demonstração | Alta |
| API pública, CRM de vendas | **não tem** | não tem | **Nenhuma**: não precisa para paridade |
| Funil comercial, conflito de interesses, assinatura eletrônica | não confirmados / fora do foco | ausentes | Baixa prioridade para a primeira venda |

Leitura: o Astrea também não faz tudo. Para igualar o essencial não é preciso funil, API pública nem ERP. É preciso **fazer muito bem
publicação → prazo → aviso → responsável**, e ter financeiro básico, app e área do cliente simples.

## 3. O que falta para produção (em portões)

**Gate 0 — pode existir produto? (decisões, não código)**
1. **Fonte de dados de tribunais e uso comercial.** DataJud proíbe uso comercial; DJEN não tem termo localizado. Sem resposta escrita do CNJ ou
   contrato com provedor, o coração do produto (publicações e monitoramento) não pode ser vendido.
2. **Enquadramento** (portfólio × comercial) e entidade jurídica para contratar, cobrar e assinar contratos de tratamento de dados.
3. **Sigilo × IA**: Anthropic (contrato de não-treinamento ainda não confirmado) e GPU alugada (fatos do caso em host de terceiro).

**Gate 1 — confiável o bastante para um piloto fechado**
4. **CI** de build e testes (hoje só segurança e backup) e **teste real ponta a ponta** do módulo de peças e da fila.
5. **Aviso entregue**: e-mail transacional real (convites e lembretes de prazo) e push; hoje são stubs.
6. **Prazo correto**: calculadora no backend com feriados forenses, recesso e suspensões, validada por advogado.
7. **Autorização por caso/cliente** e MFA para sócio; teste automatizado de acesso entre colegas e entre escritórios.
8. **LGPD mínima**: política de privacidade e termos publicados, exportação e exclusão de dados do titular, DPO nomeado, plano de incidente.
9. **Backup restaurado ao menos uma vez**, com procedimento escrito.
10. **Topologia de produção decidida** (Azure × execução própria com Cloudflare Tunnel) e `ForwardedHeaders` restrito.

**Gate 2 — paridade mínima para convencer quem usa Astrea**
11. **Movimentações processuais** (entidade, captura, histórico) e monitoramento por processo.
12. **Publicação → prazo no backend**, com responsável, idempotência e trilha de revisão.
13. **Financeiro no servidor**: honorários, contas a receber, despesas, timesheet e relatório simples. Hoje a tela guarda no navegador e some em
    outro aparelho.
14. **Notificações ao cliente** por e-mail quando há movimentação (o Astrea faz) e **área do cliente** mínima (andamento + documentos).
15. **App móvel** com publicações e alertas (o Astrea tem app em todos os planos).
16. **Gestão de documentos** (versões, permissões por caso, scan de malware) e exportação de peças em DOCX/PDF.
17. **Importação** de processos e clientes por planilha, e de exportações de outros sistemas: o cliente que sai do Astrea precisa trazer a base.

**Gate 3 — operar como SaaS (item não coberto na auditoria anterior)**
18. **Cobrança do próprio produto**: planos, período de teste, assinatura, nota fiscal, inadimplência, cancelamento. **Não encontrei nada disso
    no backend.** Sem isso não há como vender.
19. **Suporte e ajuda**: canal de atendimento, central de ajuda, e-mails de boas-vindas, onboarding guiado (existe um wizard de cadastro).
20. **Confiabilidade operada**: página de status, alertas de plantão, SLO definidos, teste de carga, métricas de produto, e-mail com SPF/DKIM/DMARC.
21. **Segurança verificada**: teste de intrusão, revisão de dependências com correção, gestão de segredos e rotação, acesso a dados de produção auditado.
22. **Acessibilidade e responsividade validadas** em dispositivos reais (o design system tem as regras; falta verificação).
23. **Publicidade e posicionamento** dentro das regras da OAB (Provimento 205/2021): a landing já evita promessa de resultado e números.

## 4. Onde o Iustitia pode se diferenciar (sem inventar)

- **IA com rastreabilidade**: citações classificadas, bloqueio de exportação com citação não verificada e revisão humana registrada. O Astrea
  também recomenda revisão humana, mas não confirmei um bloqueio equivalente. Só vale como diferencial **depois** de a IA ser validada.
- **RAG sobre acervo normativo próprio** com fonte e versão por trecho.
- **Privacidade**: execução local/privada da IA, se o jurídico aceitar o desenho com GPU sob demanda (isso também é risco, ver Gate 0).
- **Atendimento por WhatsApp com handoff bot↔humano** (módulo existente): não confirmei equivalente no Astrea.

## 5. Caminho sugerido

1. **Semanas 1–2:** decisões do Gate 0 (CNJ por escrito, provedor de dados, enquadramento) + CI + validar o módulo de peças localmente.
2. **Semanas 3–6:** e-mail real, calculadora de prazo, autorização por caso, MFA, LGPD mínima, restauração de backup.
3. **Piloto fechado e gratuito** (1–2 escritórios, com contrato de piloto e termos) enquanto se constrói Gate 2.
4. **Antes de cobrar:** Gate 2 essencial (movimentações, financeiro no servidor, app, área do cliente) e Gate 3 (cobrança, suporte, status).

Não há como estimar prazos totais sem saber o tamanho da equipe e a decisão sobre a fonte de dados; os intervalos acima são ordem de grandeza
para um desenvolvedor focado e podem estar subestimados.

## Fontes

- [Astrea ou Projuris — diferenças (Aurum)](https://www.aurum.com.br/blog/astrea-ou-software-concorrente/)
- [Planos e preços do Astrea (Aurum)](https://www.aurum.com.br/astrea/planos-e-precos/)
- [Gestão de processos judiciais com o Astrea (Aurum)](https://www.aurum.com.br/astrea/gestao-de-processos/)
- [Central de ajuda do Astrea](https://astrea.aurum.com.br/pt-BR/)
- [Aplicativo móvel Astrea](https://astrea.aurum.com.br/pt-BR/articles/7192870-conheca-o-aplicativo-movel-astrea)
- [Guia completo do Astrea (B2B Stack)](https://blog.b2bstack.com.br/astrea/)
- [Comparativos B2B Stack: Advbox × Astrea](https://www.b2bstack.com.br/compare/advbox-vs-astrea), [Legal One × Astrea](https://www.b2bstack.com.br/compare/legal-one-vs-astrea), [Projuris ADV × Astrea](https://www.b2bstack.com.br/compare/projurisadv-vs-astrea)
- [Planos da Advbox](https://advbox.com.br/planos)
- [Conjur: pesquisa sobre uso de softwares jurídicos](https://www.conjur.com.br/?p=445736)
