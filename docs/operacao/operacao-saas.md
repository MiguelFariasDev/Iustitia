# Operação do SaaS — o que existe, o que falta e quem decide

Estado em **24/11/2026**. Este documento separa o que o **código** já entrega do que depende de **decisão ou contratação**
do negócio. Nada abaixo deve ser tratado como pronto só porque está listado.

## 1. O que já está operacional (código e infra)

| Área | Situação | Onde |
|---|---|---|
| Build e testes no CI | ✅ Backend (Release, com PostgreSQL, RabbitMQ e Redis) e frontend em cada PR | `.github/workflows/ci.yml` |
| Backup do banco | ✅ Diário/semanal/mensal, **cifrado por chave pública** (corrigido: antes o passo de criptografia falhava no CI) | `.github/workflows/backup.yml`, `docs/compliance/backups.md` |
| Restauração | ✅ Procedimento + ensaio trimestral | `docs/runbooks/restauracao-de-backup.md` |
| Migrations (aplicar e reverter) | ✅ Runbook + `.down.sql` testados contra o schema real | `docs/runbooks/database-migration.md`, `RollbackScriptsTests` |
| Incidente de segurança / LGPD | ✅ Runbook, direitos do titular (exportar e anonimizar) | `docs/compliance/` |
| Segundo fator para sócios | ✅ Implementado; **desligado por padrão** | `docs/operacao/mfa.md` |
| E-mail transacional | ✅ Alertas por e-mail (precisa de chave e domínio) | `docs/operacao/email-transacional.md` |
| Saúde e métricas | ✅ `/health/live`, `/health/ready`, `/metrics` | `docs/observability/` |
| Alertas de infraestrutura | ⚠️ Módulo Terraform existe; regras precisam estar provisionadas no ambiente | `infra/terraform/modules/alerts`, `docs/observability/alerts.md` |

## 2. O que NÃO existe e por quê

### 2.1 Cobrança e assinatura (billing)

**Não há** plano, assinatura, período de teste, cobrança recorrente nem bloqueio por inadimplência. Construir isso antes
de decidir o provedor seria adivinhar. O que precisa ser decidido:

| Decisão | Opções a avaliar | Por que importa |
|---|---|---|
| Provedor de cobrança | Asaas, Iugu, Pagar.me, Stripe | Brasil: **PIX e boleto** pesam muito mais que cartão para escritórios pequenos; emissão de **nota fiscal** costuma ser a parte difícil |
| Pessoa jurídica emissora | CNPJ, regime tributário, município | Define a NFS-e e o gateway que aceita o cadastro |
| Planos e limites | Solo, Escritório; usuários, processos, uso de IA | Define o que o sistema precisa **medir e bloquear** |
| Política de inadimplência | Aviso → somente leitura → suspensão; prazos | Escritório de advocacia não pode perder acesso a prazos por atraso de boleto: **somente leitura, nunca bloqueio total** |
| Período de teste | 14 ou 30 dias, com ou sem cartão | |

Quando decidido, a peça de código é pequena e já tem encaixe: uma entidade de assinatura no módulo `Platform`, um
**webhook** idempotente do provedor e uma checagem de plano no mesmo ponto onde hoje a `FeatureFlag` decide acesso.

### 2.2 Suporte ao cliente

Não há canal. Mínimo viável antes de vender:

- Endereço de e-mail de suporte e um **SLA de resposta** declarado nos Termos de Uso.
- Link "Ajuda" no menu do app (hoje inexistente) apontando para o canal e para a base de conhecimento.
- Processo interno: quem atende, em quanto tempo, e como um chamado vira correção.

### 2.3 Página de status

Não há. A API expõe `/health`, mas ele é **interno**. Opções: serviço externo de status (barato e independente da própria
infraestrutura — se o status page cair junto com o produto ele não serve) alimentado por um *availability test* no
`/health/ready`. Decidir o provedor e o domínio.

### 2.4 Contratos e conformidade (nada disso é código)

Contrato de dados com o provedor de publicações; termo da Anthropic sobre não treinamento (`docs/compliance/
contrato-nao-treinamento-anthropic.md`); **DPA** com cada suboperador; encarregado (DPO) nomeado; revisão jurídica da
Política de Privacidade e dos Termos (rascunhos em `docs/legal`); **teste de invasão** independente antes do lançamento.

### 2.5 Fonte de dados dos tribunais

A consulta pública do DataJud **proíbe uso comercial** e derivados (`docs/compliance/datajud-termo.md`), então ela serve
a portfólio e demonstração, não a um produto vendido. O acompanhamento de **movimentações** e a captura de publicações
em escala comercial dependem de **autorização formal do CNJ** ou de **contrato com um provedor**. Enquanto isso não for
decidido, não se deve prometer monitoramento de andamentos a clientes pagantes.

## 3. Antes de lançar — lista de verificação

- [ ] Backup cifrado rodando e **restauração ensaiada** com o tempo medido.
- [ ] `Security__Mfa__Enforce=true` depois de os sócios cadastrarem o autenticador.
- [ ] `Email__ApiKey`, `Email:FromAddress` (domínio com SPF/DKIM/DMARC) e SMTP do Supabase configurados.
- [ ] Regras de alerta provisionadas e o **canal de plantão** definido (quem recebe, 24×7 ou horário comercial).
- [ ] Encarregado nomeado e política de privacidade e termos revisados e publicados.
- [ ] Provedor de dados dos tribunais definido e contratado.
- [ ] Cobrança, suporte e página de status decididos (seção 2).
- [ ] Teste de invasão feito e achados críticos corrigidos.
