# E-mail transacional

## O que é enviado por quem

| Mensagem | Quem envia | Observação |
|---|---|---|
| Convite de usuário (link de acesso) | **Supabase Auth** (`/auth/v1/invite`) | Configure o **SMTP próprio** no projeto Supabase (Authentication → SMTP) com o mesmo domínio do produto. A aplicação não envia um segundo e-mail. |
| Alertas (publicações e tarefas/prazos) | Aplicação (`AlertEmailConsumer` → `EmailService` → `ResendEmailSender`) | Só o **título** do alerta e um link para `/alertas`. Descrição, partes e nº de processo ficam na plataforma (sigilo profissional). |

## Configuração

Seção `Email` (appsettings) — **a chave nunca vai no appsettings**:

| Chave | Origem | Descrição |
|---|---|---|
| `Email__ApiKey` | variável de ambiente / Key Vault | Chave da API do Resend. |
| `Email:FromAddress` | config | Remetente verificado no provedor (SPF, DKIM e DMARC do domínio). |
| `Email:AppBaseUrl` | config | URL pública do frontend, usada nos links. |
| `Email:AlertTypes` | config | Tipos de alerta enviados por e-mail (`Publication`, `Task`, `Birthday`, `Other`). Padrão: `Publication`, `Task`. |

Sem `ApiKey`/`FromAddress` o envio é ignorado com log (sem destinatário nem corpo): dev local e CI funcionam sem provedor.

## Comportamento em falha

O envio roda em consumer próprio, separado do push em tempo real. Falha do provedor (429, 5xx, 4xx) lança exceção → o MassTransit retenta com backoff e, esgotado, vai para a fila de erro. O sino do app não recebe o alerta duplicado, pois a idempotência é por consumer.

## Pendências conhecidas

- Não há preferência por usuário para desligar e-mails de alerta (hoje é configuração global por tipo).
- Domínio, remetente e conta no provedor dependem de decisão do negócio.
