# ADR-068 — Criptografia de campos em repouso e índice cego

**Status:** aceito · **Contexto:** Etapa 3.9, LGPD

## Decisão
- **AES-256-GCM no aplicativo** (não `pgcrypto`). Com `pgp_sym_encrypt` a chave trafegaria em
  SQL e apareceria em `pg_stat_statements`/logs do Supabase; aqui a chave nunca chega ao banco.
- Formato `enc:v1:{keyId}:{base64(nonce|tag|ct)}`; o contexto da coluna é AAD (um valor copiado
  de outra coluna não decifra). Anel de chaves com `ActiveKeyId` ⇒ rotação sem big-bang.
- Chaves em Key Vault (`Encryption--ActiveKeyId`, `Encryption--Keys--{id}`,
  `Encryption--BlindIndexKey`). Em prod, ausência de chave derruba o startup. Em
  Development/Testing sem configuração usa chaves públicas de dev (sem proteção real).
- **Cifrados:** `clientes.documento` (CPF/CNPJ), `clientes.email`, `clientes.telefone`,
  `users.oab_number`, `users.phone`. **Não cifrados (decisão):** endereço (sem requisito de
  busca, mas baixo risco isolado; proteção por RLS + criptografia do disco do Supabase) e
  `users.email` (identidade de login do Supabase, única por tenant).
- **Índice cego:** a cifra é não-determinística, então igualdade/unicidade não funcionam sobre
  a coluna. `documento_busca` passou a guardar HMAC-SHA256 (chave distinta) e o índice único
  `ux_clientes_tenant_documento` foi movido para ele. Sem isso, o `ValueConverter` pedido
  deixaria o CPF em claro em `documento_busca` e a unicidade deixaria de valer.
- **Trade-off assumido:** a busca por *prefixo* de CPF deixou de existir; só CPF/CNPJ completo.
  Guardar prefixo em claro anularia a criptografia.
- Migração: leitura tolera legado em texto puro (`AllowLegacyPlaintext`); backfill idempotente
  (`worker --backfill-field-encryption`); depois desligar a tolerância.
  Runbook: `docs/runbooks/field-encryption.md`.

## Limitações
Quem comprometer a aplicação em execução tem as chaves em memória. O objetivo é proteger contra
vazamento de banco/backup, não contra comprometimento do runtime.
