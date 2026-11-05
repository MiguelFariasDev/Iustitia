# ADR-069 — Dispositivos móveis, push (FCM) e revogação

**Status:** aceito · **Relacionado:** ADR-004 (RLS), ADR-065 (alertas), ADR-067 e ADR-068 (app móvel Flutter e sua autenticação)

## Contexto

O app móvel precisa de três coisas do backend: **registrar o aparelho** para receber push, **listar** os
aparelhos do usuário (tela "Segurança") e **revogar** um aparelho perdido. O ADR-068 deixou em aberto
que, perdido o celular, a sessão só acabava revogando o refresh token no Supabase.

## Decisão

1. **`Device` (Platform.Domain)** é a instalação do app de um usuário: tenant, usuário, plataforma
   (`iOS`/`Android`), token de entrega, `IsRevoked`, `LastSeenAt`. Tabela `devices`, com índice **único**
   `(tenant_id, device_token)` — é o que torna o registro idempotente.
2. **Endpoints** (`AnyAuthenticatedUser`): `POST /devices/register`, `GET /devices`, `DELETE /devices/{id}`.
   O registro devolve o `deviceId`; o app o guarda e o envia em **`X-Device-Id`** nas demais chamadas.
3. **O token de entrega é um segredo e nunca sai.** Nenhuma resposta o contém (há teste que falha se um
   campo com "Token" surgir em `DeviceResponse`), `Device.ToString()` não o inclui, as mensagens de
   validação não o ecoam, os logs identificam o aparelho por `DeviceId`, e o mascaramento de logs
   reconhece o formato clássico do FCM como rede de segurança.
4. **Revogar é permanente.** Re-registrar um token revogado **não o reativa**: responde `401 DEVICE_REVOKED`.
   Se reativasse, quem roubou o aparelho (e ainda tem a sessão) desfaria a revogação só reabrindo o app.
   O usuário legítimo volta com um token novo (reinstalar o app gera outro).
5. **Aparelho compartilhado**: o mesmo token registrado por outro usuário *do mesmo escritório* **muda de
   dono** (a linha é reatribuída, não duplicada). Um token revogado fica revogado para todos.
6. **Revogar é do próprio usuário**: o dispositivo de um colega responde `404 DEVICE_NOT_FOUND`, sem
   revelar que o id existe.
7. **Push — `IPushNotificationService`** (BuildingBlocks.Application, como `IAlertPublisher`: módulos de
   negócio não dependem de Platform). Implementação `FcmPushNotificationService` sobre o Firebase Admin
   SDK, **só para dispositivos não revogados**; token que o FCM declara inexistente (app desinstalado) é
   removido. Sem credenciais (`Push:Fcm:CredentialsJson` ou `CredentialsPath`) o serviço é o `Noop` e o
   resto do sistema segue igual. As credenciais usam `CredentialFactory<ServiceAccountCredential>`: o
   `GoogleCredential.FromJson/FromFile` está depreciado por risco de segurança e aceitaria qualquer tipo
   de credencial.
8. **O push sai do `AlertCreatedConsumer`**, depois do Realtime, e **não propaga falha**: se propagasse, o
   MassTransit reenviaria o evento inteiro e o alerta chegaria duplicado ao navegador. A rota do push é
   `/dashboard` — o evento só traz o id da publicação, e é o app que resolve o processo dela.
9. **Guarda de revogação (`RevokedDeviceMiddleware`)**: com `X-Device-Id` de um aparelho revogado, a API
   responde `401 DEVICE_REVOKED` e o app encerra a sessão local. Consulta cacheada por 15 s: **a revogação
   vale em até 15 s**, em troca de não ir ao banco a cada requisição.
10. **Isolamento**: o RLS (`014_devices.sql`) isola por **tenant**; o isolamento por **usuário** é do
    `DeviceRepository` (tenant E usuário em toda consulta), como em alertas (ADR-065). Uma policy por usuário
    impediria a leitura, por token, da linha de um colega — exatamente o que o item 5 precisa.

## O que a revogação NÃO faz (limites assumidos)

- **Não invalida o token do Supabase.** O refresh token continua válido no Supabase Auth. A revogação
  alcança: o push (para), a API (recusa, **se o app enviar `X-Device-Id`**) e o app oficial (que se desloga).
  Quem tiver o refresh token e **não enviar o cabeçalho** passa pela guarda. Revogar sessões no Supabase
  exigiria a API administrativa dele e o id de sessão por aparelho; ficou fora, por não ter sido verificado.
- **Cache de 15 s** na instância: com várias instâncias, cada uma tem o seu.
- **Mesmo token em escritórios diferentes** é permitido (o índice é por tenant) e o RLS não deixa um tenant
  ver o outro; logo, sair de uma conta **deve** chamar `DELETE /devices/{id}` no app, senão o aparelho
  continua recebendo push da conta anterior.

## Pendente de alinhamento com o app móvel

- O app deve **guardar o `deviceId`**, **enviá-lo em `X-Device-Id`** e **tratar `DEVICE_REVOKED`** (logout local).
- O app deve **chamar `DELETE /devices/{id}` ao sair** da conta.
- **FCM `token` x Installation ID (FID).** O Firebase Admin 3.x e o `firebase_messaging` 17 depreciaram o
  token clássico em favor do FID; os dois lados ainda usam o **token clássico** (`Message.Token`, com
  `#pragma` e comentário). Migram **juntos**: trocar só um dos lados quebra o push.
