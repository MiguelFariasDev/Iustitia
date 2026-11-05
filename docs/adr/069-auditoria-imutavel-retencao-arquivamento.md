# ADR-069 — Auditoria imutável, retenção de 5 anos e arquivamento frio

**Status:** aceito · **Contexto:** Etapa 3.9, Provimento CFOAB/LGPD

## Decisão
1. **Imutabilidade em três camadas** (`infra/supabase/migrations/014_audit_immutability.sql`):
   `app_user` só tem `SELECT`/`INSERT`; RLS só com policies de SELECT/INSERT; trigger bloqueia
   `UPDATE`/`TRUNCATE` sempre — até para o dono — e `DELETE` fora das funções de manutenção.
2. **Retenção imposta pelo banco, não por configuração:** o trigger só aceita `DELETE` de
   linha (a) já arquivada (manifesto `audit_log_archives`) ou (b) com mais de 5 anos.
   Antes o job apagava via EF/`app_user` — o que a regra de imutabilidade tornaria impossível,
   e o RLS ainda escondia as linhas dos outros tenants.
3. **Arquivamento frio:** meses completos com mais de 1 ano vão para Blob (NDJSON.gz, SHA-256,
   `If-None-Match: *`, nunca sobrescreve) e só então o banco remove o mês, na mesma transação do
   registro no manifesto, **recusando se a contagem divergir**. Sem `AuditArchive:AccountUrl`
   nada é arquivado nem removido. O container deve ter imutabilidade (WORM ≥ 5 anos) e lifecycle
   Cool/Archive — configuração de infra (pendente).
4. **O que se audita:** `[Audited]` declarativo + `AuditBehavior` (fora da transação: só o que
   foi confirmado). Cobertura: login/logout/login falho (usuário existente), usuários e tenants,
   clientes e processos (criar/editar/desativar/ativar/visualizar), publicações (revisão
   `Approved/Rejected/Corrected` e visualização). `Uploaded/Downloaded` existem no enum para o
   módulo Documents (ainda inexistente).
5. **Sem PII na auditoria:** grava ids, ator, IP completo, User-Agent e metadados não sensíveis
   (ex.: novo status) — nunca CPF/e-mail/notas, que ficariam sem criptografia numa tabela imutável.

## Limitações
Superusuário do banco ainda pode remover o trigger; o controle compensatório é a restrição de
acesso administrativo e o WORM do Blob. Login falho de e-mail inexistente não tem tenant a que
se atribuir e fica só nos logs. Falha ao gravar auditoria é logada como erro e não desfaz a
operação — configurar alarme sobre esse log.
