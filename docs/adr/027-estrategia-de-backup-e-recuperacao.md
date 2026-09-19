# ADR-027: Estratégia de Backup e Recuperação

**Status:** Aceito

## Contexto

O Iustitia armazena dados jurídicos sensíveis (processos, publicações,
dados de clientes) com exigência regulatória de retenção mínima de 5 anos
(LGPD + normas da OAB — ver `docs/compliance/lgpd.md` e `oab.md`). Perda de
dados nesse contexto não é só um incidente técnico: pode significar perda
de prazo processual real para um cliente do escritório. Depender de uma
única camada de backup (só o backup automático do provedor) é um ponto
único de falha — se o provedor tiver um problema, ou se um erro de
aplicação corromper dados silenciosamente por semanas antes de ser
percebido, um backup diário de curta retenção pode não ser suficiente para
recuperar.

## Decisão

Adotar backup em **múltiplas camadas independentes**, documentadas em
detalhe em `docs/compliance/backups.md`:

1. **Backups automáticos do Supabase** (diários, retenção 7–14 dias
   conforme plano) — primeira linha de defesa, recuperação rápida para
   incidentes recentes.
2. **PITR (Point-in-Time Recovery)** — quando disponível no plano
   contratado, permite restaurar para qualquer segundo dentro da janela de
   retenção, não só para o snapshot diário.
3. **`pg_dump` agendado para Azure Blob Storage**, em três frequências
   independentes (GitHub Actions):
   - Diário → retenção 30 dias
   - Semanal → retenção 1 ano
   - Mensal → retenção 5 anos (alinhado à exigência de retenção legal)
4. **Backup do Supabase Storage** (documentos anexados) para Azure Blob,
   diário, com camada hot (30 dias) e cold (1 ano).
5. Todos os backups em Azure Blob são **criptografados** (AES-256) antes do
   upload, com a chave de criptografia em segredo do GitHub Actions/Azure
   Key Vault — nunca em texto claro.
6. **Teste de restauração trimestral** em ambiente de staging — um backup
   nunca testado não é um backup confiável.

## Consequências

**Positivas:**
- Nenhuma camada sozinha é um ponto único de falha: perder o Supabase não
  significa perder os dados (Azure Blob independente); um erro detectado
  tardiamente ainda tem backups mensais de até 5 anos atrás.
- Retenção de 5 anos nos backups mensais alinha a política de backup com a
  própria exigência legal de retenção de dados.
- Teste de restauração trimestral obrigatório evita a armadilha comum de
  "backup que nunca foi restaurado com sucesso na prática".

**Negativas / trade-offs:**
- Custo de storage adicional no Azure Blob (backups diário/semanal/mensal
  acumulados) — aceito dado o risco que mitiga.
- Complexidade operacional: múltiplos workflows de CI/CD a manter e
  monitorar (alertar em caso de falha é obrigatório, não opcional).
- Backups também contêm dados pessoais (LGPD) — o expurgo de backups
  antigos precisa seguir a mesma política de retenção/exclusão dos dados
  originais, documentado em `docs/compliance/backups.md`.
