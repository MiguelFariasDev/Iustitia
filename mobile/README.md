# Advocacia Mobile

App Flutter (iOS e Android) do escritório. Fundação: login, painel, processos, clientes, agenda
(demonstração), perfil, notificações push e em tempo real. Visão geral, telas e lacunas do
backend em [`docs/mobile`](../docs/mobile); decisões em [ADR-067](../docs/adr/067-flutter-para-o-app-mobile.md)
e [ADR-068](../docs/adr/068-autenticacao-mobile-sessao-segura-e-biometria.md).

## Requisitos

- Flutter estável com Dart `^3.13` (`flutter --version`).
- Android: Android SDK (minSdk 24). iOS: macOS + Xcode (iOS 15+).

## Configuração

Nada de URL ou chave no código: tudo entra por `--dart-define`. Copie o exemplo e preencha:

```bash
cp env/example.json env/dev.json      # env/*.json é ignorado pelo git
flutter run --dart-define-from-file=env/dev.json
```

| Chave | Para quê |
|---|---|
| `API_URL` | Base da API REST, ex.: `https://api.exemplo.com.br/api/v1` |
| `SUPABASE_URL`, `SUPABASE_ANON_KEY` | Supabase Auth e Realtime (mesmo projeto do web) |
| `APP_NAME` | Nome exibido (padrão `Advocacia`) |
| `AUTH_MODE=mock` | Login local de demonstração, sem Supabase (só dev/testes) |
| `API_MOCKS=true` | Repositórios em memória, sem API. **Padrão falso.** |
| `FIREBASE_API_KEY`, `FIREBASE_APP_ID`, `FIREBASE_MESSAGING_SENDER_ID`, `FIREBASE_PROJECT_ID` | Push. Sem as quatro, o app roda sem push |

Sem `SUPABASE_*` (e sem `AUTH_MODE=mock`) ou sem `API_URL` (e sem `API_MOCKS`), o app mostra o que
falta em vez de quebrar.

### Demonstração (sem backend)

```bash
flutter run --dart-define=AUTH_MODE=mock --dart-define=API_MOCKS=true
# login: ana.souza@escritorio.com.br / demo1234
```

A **agenda é sempre de demonstração**, com ou sem esses flags: ainda não há backend de agenda.

## Testes

```bash
flutter analyze
flutter test            # unitários (repositórios, interceptors, formatadores…) + widget
```

Os testes não usam aparelho nem rede: o `Dio` recebe um adaptador falso e os repositórios têm
versões em memória (`lib/core/mock`). O relógio é injetável (`clockProvider`).

## Push (FCM/APNs) — passos fora do código

O código está pronto, mas **depende de configuração que não vai no repositório** e que não foi
validada em aparelho:

1. Crie o app Android e o app iOS no projeto Firebase, usando o `applicationId`/bundle ID
   `br.com.escritorio.advocacia.advocacia_mobile`, e preencha as quatro chaves `FIREBASE_*`.
2. **iOS:** habilite *Push Notifications* e *Background Modes → Remote notifications* no Xcode,
   suba a chave APNs (.p8) no Firebase e use `platform :ios, '15.0'` no `ios/Podfile` (gerado no
   primeiro `pod install`).
3. O backend precisa do endpoint `POST /api/v1/devices/register` (hoje inexistente — o app trata o
   404 e segue sem push) e de enviar `data.route` (ex.: `/processos/{id}`) nas notificações.

## Pastas

```
lib/
  app/            config, tema, router, shell, host de notificações
  core/           api (Dio), auth, errors, notifications, settings, utils, mock
  features/       auth · dashboard · processos · clientes · agenda · perfil
                  (cada uma: data / domain / presentation)
  shared/         widgets e modelos reutilizáveis
test/             unit/ e widget/ (+ support/ com os fakes)
env/              example.json (os demais são ignorados)
```

## O que ainda NÃO foi validado

Compilado e testado: `flutter analyze`, `flutter test` e um `flutter build web` (usado para
capturas de tela e checagem de contraste). **Não compilado:** os builds Android e iOS nativos (sem
Android SDK nem macOS no ambiente de desenvolvimento). Pendente em aparelho: push, biometria
real e o armazenamento no Keychain/Keystore.
