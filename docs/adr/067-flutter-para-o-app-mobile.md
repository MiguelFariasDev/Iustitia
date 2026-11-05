# ADR-067 — Flutter (Dart) para o app mobile

**Status:** aceito · **Substitui:** [ADR-011](./011-swift6-swiftui.md) (Swift 6 + SwiftUI) e
[ADR-019](./019-swiftdata-persistencia-local.md) (SwiftData) · **Contexto:** fundação do app mobile

## Contexto

O plano original era um app **iOS nativo** (Swift 6 + SwiftUI, `/mobile-ios`). O escritório usa
iOS **e Android**, e manter dois apps nativos — ou deixar metade da equipe sem app — não cabe
no tamanho do time. A pasta `/mobile-ios` foi descartada; nada dela foi implementado.

## Decisão

1. **Flutter**, um só código para iOS e Android, em `/mobile` (pacote `advocacia_mobile`).
2. **Marca desacoplada.** O pacote Dart, o `applicationId` Android e o bundle ID iOS usam
   `br.com.escritorio.advocacia.advocacia_mobile` — sem o nome do produto. O nome exibido
   ("Advocacia" por padrão) vem de `APP_NAME` (`AppConfig.appName`), do `strings.xml` e do
   `CFBundleDisplayName`. *O prompt original pedia `--project-name iustitia_mobile`; isso
   contradiz a regra de manter a marca fora de nomes de projeto, pacote e bundle ID, e o
   resto do repositório (`Advocacia.sln`, namespaces `Advocacia.*`) a segue.*
3. **Arquitetura em camadas por feature** (`app/`, `core/`, `features/<x>/{data,domain,
   presentation}`, `shared/`), espelhando a divisão do backend. Repositórios são **interfaces**
   (`ProcessoRepository`…) com implementação HTTP e outra em memória para demonstração e testes.
4. **Estado: Riverpod** (`flutter_riverpod` 3). Sem `setState` para estado compartilhado — só para
   estado puramente visual de uma tela (mês exibido na agenda). `ProviderScope(retry: null)`:
   o Riverpod 3 refaz sozinho providers que falham, o que esconderia o erro e repetiria
   chamadas; quem decide tentar de novo é o usuário ("Tentar novamente").
5. **Navegação: GoRouter** com `StatefulShellRoute.indexedStack` (cada aba guarda a própria
   pilha) e **um único `redirect` global**. Rota que vem de fora (push, alerta, parâmetro
   `from`) passa por `safeInternalRoute`, que só aceita as rotas do app — equivalente ao
   `safeRedirectPath` do web.
6. **HTTP: Dio**, atrás de `ApiClient`: os repositórios só enxergam JSON e `ApiException`. O
   interceptor injeta o Bearer do Supabase e, em 401, renova **uma vez** e refaz a chamada;
   outro interceptor converte `ProblemDetails` (RFC 7807) em `ApiException`.
7. **Modelos escritos à mão** (`fromJson`), não com `freezed`/`json_serializable`. São DTOs de
   leitura, pequenos, e a geração de código (`build_runner` + arquivos `.g.dart` versionados)
   custaria mais que o ganho. **Revisitar** quando houver modelos de escrita ou uniões grandes.
8. **Configuração por `--dart-define`** (`AppConfig`): nenhuma URL ou chave no código.
   `API_MOCKS` e `AUTH_MODE=mock` valem **falso/supabase por padrão** — diferente do web, em que
   o mock é o padrão —, porque um build de produção esquecido com mock mostraria dados inventados
   como se fossem do escritório.
9. **Tipografia Inter via `google_fonts` 8.x**, baixada em tempo de execução. O `google_fonts` 9
   depende do pacote `material_ui` (Material fora do SDK), cujo `TextTheme` é de outro tipo que o
   do `package:flutter/material.dart`; a linha 8 usa o Material do SDK. **Antes do lançamento,
   empacotar a fonte como asset**: baixar de um terceiro em tempo de execução vaza o IP do
   aparelho para o Google e falha sem rede.

## O que o app NÃO faz de propósito

Como no web, **nada de dado inventado apresentado como real**:

- **Prazos e tarefas** não existem no backend (Fases 3 e 4). O painel mostra "Próximos prazos —
  Em breve", sem número. O cartão "Tarefas pendentes" do pedido virou "Processos ativos".
- **Agenda** é de demonstração (`DemoAgendaRepository`) e a tela avisa, sempre.
- **Movimentações** do tribunal: a aba existe e diz "em breve" (a consulta ao DataJud devolve só
  metadados do cadastro).
- **"Ver no site do tribunal"** copia o número CNJ e abre o portal unificado do PDPJ: cada
  tribunal tem sistema e URL próprios, e não há como montar um link direto sem inventá-lo.
- **Lembretes de prazo**: o serviço de agendamento existe e é testado, mas o interruptor fica
  desabilitado enquanto a agenda for de demonstração — ligá-lo agendaria avisos de eventos falsos.
- **Cadastro de processos, importação e financeiro**: ficam no web, como pedido.

## Consequências

- **Positivas:** um código para as duas lojas; 150 testes (unitários + widget) rodando sem
  aparelho; telas validadas também por captura real (build web).
- **Negativas:** o ecossistema Flutter se move rápido — as versões resolvidas são muito novas
  (Riverpod 3, go_router 18, local_auth 3) e várias APIs aparecem depreciadas (`getToken` do
  FCM, `anonKey` do Supabase). Dependências ficam fixadas por `pubspec.lock`; atualizar com
  critério, uma por vez.
- **Lacunas verificadas só em parte:** o build **Android/iOS nativo não foi compilado** (sem
  Android SDK nem macOS no ambiente). Validado: `flutter analyze`, `flutter test` e um
  `flutter build web`. Pendente em aparelho: push (FCM/APNs), biometria real, Keychain/Keystore.
