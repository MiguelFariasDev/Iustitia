# Runbook — criptografia de campos (chaves, backfill e rotação)

Ver ADR-068. Gerar chaves: `openssl rand -base64 32` (cada chave de cifra e a `BlindIndexKey`).

## Primeira ativação (dados legados em texto puro)
1. Criar no Key Vault: `Encryption--ActiveKeyId` (ex. `k1`), `Encryption--Keys--k1`, `Encryption--BlindIndexKey`.
2. Aplicar migrations EF (`EncryptSensitiveFields` em Platform e Legal) — só alarga colunas e move o índice único.
3. **Antes de reabrir tráfego**, rodar o backfill com a connection string do *dono* do schema (o
   RLS esconde as linhas de quem não é dono):
   `ConnectionStrings__DefaultConnection=<owner> dotnet Worker.dll --backfill-field-encryption`
   É idempotente. Entre os passos 2 e 3 a checagem de CPF duplicado não enxerga linhas legadas.
4. Conferir: `SELECT count(*) FROM clientes WHERE documento NOT LIKE 'enc:v1:%'` = 0 e idem `users.oab_number`.
5. Definir `Encryption__AllowLegacyPlaintext=false`.

## Rotação da chave de cifra
1. Adicionar `Encryption--Keys--k2`; trocar `ActiveKeyId` para `k2` (a `k1` permanece no anel).
2. Rodar o backfill (re-cifra tudo que não esteja sob `k2`).
3. Só então remover `k1`.

## Rotação da `BlindIndexKey`
Invalida todos os índices. Trocar a chave e rodar o backfill **não basta** (ele preserva
índices de 64 caracteres): exige recalcular `documento_busca` a partir do documento decifrado.
Tratar como migração planejada.

## Perda da chave
Os dados cifrados são irrecuperáveis. Backup das chaves no Key Vault (soft-delete + purge protection).
