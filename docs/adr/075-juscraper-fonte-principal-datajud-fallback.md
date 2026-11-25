# ADR-075 — Juscraper como fonte principal dos dados do tribunal; DataJud como fallback

**Status:** Aceito — implementado com testes unitários; **não** testado contra o serviço juscraper real nem
contra os portais (ver "Estado da verificação").

**Revisa:** ADR-064 (DataJud, uso portfólio).

## Contexto

O ADR-064 colocou a consulta de processo atrás de uma porta do módulo Legal
(`IConsultaProcessoTribunal`) porque o DataJud "tem prazo de validade": o Termo de Uso proíbe uso
comercial (cláusulas 3.3 e 3.8). Além disso, na prática o DataJud:

- responde devagar e no limite (429 depois de 16 a 28 s, com a chave pública dividida com todo o país);
- traz os nomes da Tabela Processual Unificada, não o que o cartório escreveu;
- não serve para acompanhar andamentos noturnos de centenas de processos.

O **juscraper** é um serviço HTTP (.NET 10) que consulta diretamente os portais públicos dos tribunais
(e-SAJ e a nova consulta pública do PJe), com cache de 30 min, 1 s entre requisições ao mesmo portal e no
máximo 2 simultâneas. Contrato em `docs/integrations/juscraper.md`.

## Decisão

### 1. Consulta do cadastro: juscraper principal, DataJud fallback

`ConsultaProcessoTribunalComFallback` é a implementação registrada da porta; as duas fontes são *keyed
services* (`FonteConsulta.Juscraper`, `FonteConsulta.DataJud`). O handler, o endpoint e a tela não mudam —
a resposta só ganha o campo opcional `fonte`.

| Resultado do juscraper | Ação |
|---|---|
| encontrado | juscraper |
| não encontrado (200, `encontrado: false`) | "não encontrado", sem DataJud — o portal é a fonte primária e mais atual. `Juscraper:FallbackQuandoNaoEncontrado` liga o DataJud também aqui |
| `TRIBUNAL_NAO_SUPORTADO`, `CAPTCHA_EXIGIDO`, `ACESSO_BLOQUEADO` | DataJud (o caso principal: TRFs, TRTs, TJMG…) |
| `TRIBUNAL_INDISPONIVEL`, `RESPOSTA_INESPERADA`, timeout, rede, 5xx, circuito aberto | DataJud |
| `NAO_AUTORIZADO` | DataJud, com log de nível **Error** (é configuração errada) |
| `NUMERO_CNJ_INVALIDO` | erro, sem DataJud |
| `Juscraper:BaseUrl` vazio | só DataJud (o comportamento anterior) |

Se o DataJud também falhar, vale o erro dele (a última fonte tentada); os dois códigos vão para o log.

A decisão usa o **código original** do juscraper (`JuscraperFalha.Motivo`), não o `ErrorCode` do Iustitia:
`CNJ_TRIBUNAL_NOT_SUPPORTED` junta três causas diferentes. O `Error` do BuildingBlocks não tem metadados,
então o adaptador expõe um método interno (`ConsultarDetalhadoAsync`) que devolve o resultado da porta e a
falha original.

Resiliência: 60 s por tentativa e **uma** repetição (o juscraper já repete contra o portal; repetir mais
multiplicaria as requisições ao tribunal). Com o circuito aberto, o composto vai direto ao DataJud.

Métrica `consulta_tribunal.fonte` com tags `fonte`, `tribunal` e `motivo_fallback`. O número do processo
nunca é tag.

### 2. Acompanhamento noturno de movimentações

`AcompanhamentoMovimentacoesJob` (Hangfire, `30 2 * * *`, **desligado por padrão**) percorre os processos
ativos e suspensos de cada escritório e envia `SincronizarMovimentacoesCommand`, um processo por vez, num
escopo de DI próprio, com 1,5 s de pausa:

- janela = data do último andamento capturado − 3 dias; na primeira vez, o histórico inteiro;
- grava só os ids novos (`processo_movimentacoes`, único em `(tenant_id, processo_id, external_id)`);
- **primeira sincronização = base**: grava sem alerta nem aviso;
- com novidade: alerta in-app `Movimentacao` ao responsável (só número e quantidade) e **rascunho**
  `NotificacaoCliente` (`PendenteRevisao`) se o cliente tiver e-mail, estiver ativo e não anonimizado.
  No máximo um pendente por processo; as noites seguintes somam a ele;
- falha de um processo conta e segue; tribunal sem cobertura é contado à parte; sem retry do Hangfire.

### 3. Revisão humana obrigatória do aviso ao cliente

O job **nunca** envia ao cliente. O advogado revisa a fila (`GET /api/v1/notificacoes-cliente`) e envia
(`POST /{id}/enviar`, com o texto editado ou o rascunho) ou descarta (`POST /{id}/descartar`). O envio vai
pelo `IEmailSender` para o e-mail **atual** do cadastro, com o HTML escapado; falha do provedor deixa o aviso
como `FalhaEnvio`, que pode ser reenviado. O banco reforça: um aviso `Enviada` sem revisor ou sem data de
envio viola um CHECK (`021_movimentacoes_notificacoes.sql`).

### 4. Fallback no acompanhamento é restrito (diferente da consulta)

Misturar fontes no mesmo processo criaria duplicatas: o DataJud usa os nomes da Tabela Unificada e
timestamps; o portal, os nomes do cartório, às vezes só a data. O mesmo andamento teria ids diferentes. Por
isso (`AcompanhamentoProcessualComFallback`):

| Situação | Ação |
|---|---|
| erro transitório do juscraper (portal fora, timeout, rede) | sem fallback: pula a noite, a próxima retenta |
| sem cobertura permanente (`TRIBUNAL_NAO_SUPORTADO`, `CAPTCHA_EXIGIDO`, `ACESSO_BLOQUEADO`) | DataJud, com `ExternalId = "datajud:" + SHA-256(código + data/hora ISO + nome)`, 64 caracteres no total |
| processo que já tem andamentos de uma fonte | continua nela. Trocar só explicitamente (`POST …/sincronizar?fonte=`), gravando nova base sem notificar |

A fonte do processo é derivada dos andamentos gravados (a do capturado por último).

## Cuidado jurídico

Como fallback, o uso do DataJud **diminui, mas não desaparece**. O ADR-064 continua valendo: se o produto for
comercializado, `Juscraper:FallbackDataJud` precisa ir para `false` (ou o DataJud ser trocado por um provedor
com contrato). Com o fallback desligado, tribunal sem cobertura no juscraper fica sem consulta automática e
sem acompanhamento.

Os portais dos tribunais são públicos, mas raspá-los também tem limites: o juscraper não resolve captcha nem
contorna bloqueio, espaça as requisições e mantém cache. O job roda de madrugada, um processo por vez.

## Consequências

**Positivas:** dados do cartório (com o complemento do andamento), mais atuais que o DataJud; acompanhamento
noturno barato com `desde`; o Iustitia continua funcionando sem o juscraper (BaseUrl vazio ou serviço fora).

**Negativas / trade-offs:**

- Mais uma dependência externa para operar (hospedar no Brasil: vários tribunais recusam acesso de fora).
- Não é usada como readiness: o health do juscraper não entra no `/health/ready` do Iustitia.
- A lista de cobertura do juscraper (`GET /v1/tribunais`) ainda não é consumida pelo front; o botão segue
  habilitado pela cobertura do DataJud, que já inclui todos os tribunais do juscraper.
- No dev sem `Email:ApiKey`, o `ResendEmailSender` ignora o envio sem erro: o aviso fica `Enviada` sem ter
  saído. Em produção o provedor precisa estar configurado.

## Estado da verificação

- Testes unitários: consulta com fallback (matriz completa), adaptador (mapeamento de cada código, JSON
  ilegível, complemento), sincronização (base, novidade, acúmulo, cliente sem e-mail, troca de fonte, acesso
  confidencial), envio revisado (HTML escapado, falha e reenvio, descarte), acompanhamento com fallback
  (transitório, sem cobertura, permanência na fonte) e job.
- **Não verificado:** chamadas reais ao juscraper e aos portais; a migration e o SQL 021 num PostgreSQL real
  (os testes de integração precisam de banco); envio real de e-mail.
