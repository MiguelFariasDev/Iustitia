# Hardening de segurança — Etapa 3.9

Resumo do que foi entregue e do que **depende de infraestrutura/outras fases**. Decisões em
ADR-067 a ADR-070.

## Entregue e testado
| Controle | Onde | Teste |
|---|---|---|
| Headers OWASP + CSP da API | `SecurityHeadersMiddleware` | `SecurityHeadersTests` |
| Rate limiting (login 5/5min, forgot 3/h, escrita 60, leitura 300, webhook 1000) | `RateLimitingConfiguration` | `RateLimitingTests` |
| CORS estrito validado na subida | `SecurityConfiguration` | `SecurityHeadersTests` |
| IP real atrás de proxy | `UseForwardedHeaders` | — (depende da infra) |
| Guard contra SQL concatenado / AllowAnyOrigin | — | `SqlInjectionGuardTests` |
| Criptografia AES-256-GCM + índice cego | `BuildingBlocks.Infrastructure/Security` | `AesGcmFieldEncryptorTests`, `FieldEncryptionTests` |
| Mascaramento de PII em logs (recursivo + por nome) | `LogMaskingPolicy`, enricher | `LogMaskingPolicyTests`, `SensitiveDataMaskingEnricherTests` |
| Auditoria imutável + retenção + arquivamento | `014_audit_immutability.sql`, `AuditLogCleanupJob` | `AuditImmutabilityTests` (Postgres real) |
| Cobertura de eventos de auditoria | `[Audited]` + `AuditBehavior` | `AuditBehaviorTests`, `AuditTrailEndpointsTests` |
| CSP com nonce no web | `proxy.ts`, `csp.ts` | `csp.test.ts` + verificado em `next build/start` |
| Validação de assinatura de webhook | `WebhookSignatureValidator` | `WebhookSignatureValidatorTests` |
| Scans de CI (NuGet, npm, Trivy imagens/segredos) | `.github/workflows/security.yml` | — |

## Formatos de máscara em log (desvio consciente)
CPF é mascarado por inteiro (`***.***.***-**`), não `***.456.789-**`: com 6 dígitos visíveis e
2 verificadores, restam ~1.000 candidatos — reversível por força bruta. CNPJ, e-mail e telefone
seguem o formato pedido.

## Pendências (não implementáveis nesta etapa)
- **Infra:** restringir ingress do Container App ao Front Door (`X-Azure-FDID`); container Blob de
  auditoria com WORM ≥ 5 anos + lifecycle Cool/Archive; `AuditArchive__AccountUrl` e identidade
  gerenciada com `Storage Blob Data Contributor`; chaves `Encryption--*` no Key Vault.
- **Web:** cookies HttpOnly exigem BFF (ADR-070). Atualizar dependências com vulnerabilidade (`next`).
- **Webhook WhatsApp (5.2):** o validador existe, mas não há endpoint (Fase 9). O endpoint deve
  chamar `WebhookSignatureValidator` sobre o corpo bruto antes de desserializar, ficar sob
  `/webhooks/` e nunca confiar no número de telefone sem validar no Core.
- **Mobile (5.1):** o app é **Swift/SwiftUI** (ADR-011), não Flutter, e `mobile-ios` ainda não tem
  código. Requisitos equivalentes para a Fase 7: tokens no Keychain
  (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`); *certificate/public-key pinning* via
  `URLSessionDelegate` (fixar a chave pública, com pino de backup, não o certificado folha);
  ocultar conteúdo sensível no app switcher e detectar captura de tela
  (`UIScreen.isCaptured`/`userDidTakeScreenshotNotification` — o iOS não permite *bloquear*
  screenshots como o Android); detecção de jailbreak opcional.
- **Rotação de refresh token / JWT 15 min:** configuração do projeto Supabase
  (Auth → "Refresh token rotation" ligado, JWT expiry 900 s), não do código.
- **Hash de senha:** o Supabase usa bcrypt, não Argon2; o `Konscious.Argon2` do claude.md só
  se aplicaria a segredo gerido pela própria aplicação (ex.: API key do bot).
- **Rate limit distribuído** (Redis) e **alarme** sobre erro de gravação de auditoria.
