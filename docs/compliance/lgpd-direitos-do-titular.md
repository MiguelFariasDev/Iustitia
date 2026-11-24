# LGPD — atendimento aos direitos do titular

## Papéis

- **Escritório (cliente do Iustitia)** = **controlador** dos dados de seus clientes, partes e contatos.
- **Iustitia** = **operador**: trata os dados em nome do escritório, segundo as instruções dele (art. 39).
- O titular (cliente do escritório) dirige o pedido **ao escritório**. O escritório usa as ferramentas abaixo; o
  Iustitia atende diretamente apenas os dados dos **usuários do escritório** (advogados e equipe), de que é controlador.

## O que o produto já faz

| Direito (art. 18) | Como atender | Onde |
|---|---|---|
| II — acesso e V — portabilidade | **Baixar cópia dos dados**: JSON com todos os dados do titular e os processos vinculados | Cliente → Privacidade (LGPD); `GET /api/v1/clientes/{id}/exportacao` (sócio ou titular da conta) |
| IV e VI — eliminação / anonimização | **Anonimizar dados**: elimina nome, documento, contato, endereço, nascimento e observações, de forma irreversível, mantendo o histórico do processo sem identificar o titular | Cliente → Privacidade (LGPD); `POST /api/v1/clientes/{id}/anonimizar` |
| III — correção | Editar o cadastro do cliente | Cliente → Editar |
| I — confirmação / VII — informação sobre compartilhamento | Resposta do escritório com base na política de privacidade e na lista de suboperadores | `docs/legal/politica-de-privacidade.md` |

### Regras da anonimização

- **Recusada com processo em andamento** (ativo ou suspenso) — `CLIENT_HAS_ACTIVE_PROCESSES`. Enquanto o caso corre, o
  escritório tem obrigação legal e dever profissional de manter os dados (art. 16, I; art. 7º, VI). Encerre ou arquive
  os processos e repita.
- **Irreversível** (`CLIENT_ANONYMIZED`): o cadastro não pode mais ser editado ou reativado.
- O documento é substituído por um número **inválido** e único por cliente, o que mantém a unicidade no escritório sem
  colidir com o CPF/CNPJ real de outro cliente.
- Toda exportação e anonimização gera registro de auditoria (`Anonymized`/`Cliente`), sem dado pessoal no log.

## O que a anonimização NÃO cobre (limitações conhecidas)

Registrar e comunicar ao escritório antes de prometer eliminação total:

1. **Backups**: cópias já feitas retêm o dado até expirarem pela política de retenção (ver `docs/operacao`).
2. **Conversas de atendimento** (WhatsApp) e **contatos do CRM** vinculados à mesma pessoa: ainda não são
   anonimizados em cascata.
3. **Documentos anexados** e **peças geradas** que citem o titular no corpo do texto.
4. **Publicações capturadas** do diário oficial: são dado público, vinculado ao processo.
5. **Logs de auditoria**: não contêm dado pessoal por desenho (ver `docs/compliance/security.md`).

## Prazos

O art. 19 da LGPD fixa **resposta imediata** à confirmação de existência e **até 15 dias** para a declaração completa.
Registre a data do pedido e a da resposta no processo interno do escritório.

## Pendências

- Cascata de anonimização para atendimentos, contatos e documentos.
- Registro de pedidos de titulares (protocolo, prazo, resposta) dentro do produto.
- Encarregado (DPO) e canal de contato do Iustitia — decisão do negócio.
