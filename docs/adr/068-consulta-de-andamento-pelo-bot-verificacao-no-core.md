# ADR-068 — Consulta de andamento pelo bot: verificação de identidade feita no Core

**Status:** aceito · **Contexto:** módulo Atendimento (ADR-067); o bot de WhatsApp passa a responder
"como está o meu processo?"

## Contexto

Para falar de um processo, o bot precisa saber **com quem** está falando. O WhatsApp prova o
*número* (o `wa_id` é verificado pela Meta), não a *pessoa*. Quem decide se o número é de um
cliente — e se o documento informado confere — tem de ser quem possui o cadastro.

## Decisão

**A verificação é feita no Core.** O Bot só repassa o que o cliente digitou e recebe de volta uma
*etapa*; ele nunca vê o cadastro, nunca compara documento e não guarda sessão. Isso preserva a
regra de isolamento (o Bot não acessa dados do Core) e mantém uma única implementação do que é
sensível. Contrato em `docs/modules/atendimento.md`.

1. **Fluxo:** `iniciar` → `verificar { documento }` → `processos/{indice}`. Etapas:
   `AguardandoDocumento | Verificado | Negado | Bloqueado | Expirada`.
2. **Só com o bot conduzindo.** Em `PendingHuman`, `Human` ou `Closed` os três endpoints
   respondem 409 (`ATENDIMENTO_CONSULTA_REQUER_BOT`). Quem decide o que o cliente vê, quando há um
   advogado na conversa, é o advogado.
3. **Quem é o cliente:** o cliente **ativo** do escritório dono do telefone. Telefone sem cadastro
   — ou compartilhado por mais de um cliente — responde `Negado`, **idêntico a um documento
   errado** (mesmo status, mesmo corpo): quem pergunta não descobre se o número é de um cliente.
   Casamento brasileiro apenas (DDI 55), tolerando o nono dígito (o WhatsApp ainda entrega alguns
   celulares sem ele).
4. **Sessão persistida** (`atendimento_consulta_sessoes`), ligada ao atendimento: vale **10 min a
   partir do início** e **não se estende ao verificar**. Recomeçar (`iniciar`) invalida a anterior.
5. **3 falhas bloqueiam o cliente por 30 min.** O bloqueio é do **cliente**, não da sessão: encerrar
   a conversa e abrir outra não o contorna. E recomeçar a sessão **não zera as falhas** (o contador é
   herdado das sessões do cliente nos últimos 30 min); sem isso, `iniciar` em laço daria palpites
   ilimitados, dois por vez.
6. **Concorrência.** A sessão usa o `xmin` do PostgreSQL como token de concorrência. Sem ele, 20
   verificações paralelas leem "0 falhas" ao mesmo tempo e **todas** são avaliadas (medido: 16 de 20
   respondidas); com ele, só quem ganha a corrida grava, e os demais falham sem ganhar palpite.
7. **Só os processos desse cliente, nesse escritório.** Os ids liberados são **fixados na
   verificação**; o detalhe lê pelo id fixado (nunca pelo pedido) e a leitura ainda exige
   `tenant_id` **e** `cliente_id`. Duas travas independentes; testado explicitamente: outro cliente
   do mesmo escritório, outro escritório com o mesmo telefone e o mesmo CPF, e sessão
   expirada/bloqueada/não verificada (403).
8. **O que o cliente vê é uma lista fechada:** título (assunto + nº CNJ), fase, último andamento e
   data. O tipo `ClientProcessSummary` só tem esses campos — peças, valores, parte contrária e notas
   internas **não existem** no tipo, e a consulta nem lê essas colunas. Há teste de arquitetura que
   trava a lista.
9. **Andamento só do que o advogado revisou.** O "último andamento" vem apenas de publicações
   `Approved`/`Corrected`, e só tipo + data (nunca o inteiro teor). Publicação pendente tem
   classificação automática ainda não confirmada, e a revisão humana é obrigatória (ADR-012): o
   cliente não fica sabendo de uma sentença por um bot antes de o advogado ter olhado.
10. **"Fase" é a situação cadastrada do processo** (em andamento, suspenso, encerrado). O sistema
    não modela fases processuais; não inventamos uma.
11. **Portas, não dependência.** Atendimento não pode depender de Legal (ADR-055). A identidade e a
    leitura de processos entram por `IClientIdentityDirectory` e `IClientProcessReader`, em
    `BuildingBlocks.Application`, implementadas em `Legal.Infrastructure` — o mesmo desenho de
    `IAlertPublisher`. A porta só devolve *sim/não* para o documento; o documento cadastrado nunca
    sai de Legal por ela, e a comparação é em tempo constante.

### O documento nunca é gravado

- A comparação é em memória: normaliza (só dígitos), compara, descarta. Nenhuma coluna, evento,
  linha de auditoria ou log o recebe; as tentativas erradas também não são guardadas.
- `ToString()` do comando e do corpo da requisição o mascaram (`***`), porque o `ToString` gerado
  para um `record` o imprimiria em qualquer log ou exceção que formatasse o objeto.
- **Histórico de conversa.** O Bot registra tudo que o cliente escreve — inclusive o CPF — no
  histórico. Para o documento não ficar em texto claro lá, o Core **oculta o corpo** das mensagens do
  cliente enquanto há verificação em curso e por 60 s após uma tentativa
  (`[mensagem omitida: verificação de identidade]`), independentemente da ordem em que o Bot chama
  o registro e a verificação. É rede de segurança: o ideal é o Bot já registrar um marcador.
- **Auditoria** (`atendimento_auditoria`): `ConsultaIniciada | Negada | Verificada | Bloqueada |
  ProcessoVisualizado`, ator = o cliente (id do cadastro, ou nulo se o telefone não tem cadastro),
  `reference_id` = o processo visto, com data/hora. Sem o documento.

## Limitação: CPF + telefone é um fator de identidade fraco

Leia isto antes de ligar o recurso em produção.

- O **telefone** prova o número, não a pessoa. Quem tem o aparelho desbloqueado, trocou o chip
  (*SIM swap*) ou usa um número reciclado conversa "como" o cliente.
- O **CPF não é segredo**: aparece em vazamentos, boletos, cadastros e documentos que circulam. Não é
  "algo que só o cliente sabe".
- Logo, a verificação **eleva o custo** de um curioso ou de um erro de digitação, mas **não impede** um
  atacante determinado que controle o número. O bloqueio (3 falhas/30 min) limita o palpite, não a
  posse do número com o CPF já conhecido.
- Por isso o que sai é deliberadamente pouco e em linguagem simples (título, fase, último andamento
  revisado) e tudo fica auditado. Ainda assim é informação de um processo, coberta por sigilo
  profissional e LGPD.

**Recomendações (não implementadas):**
1. Liberar por escritório, atrás de *feature flag* (ADR-038), desligada por padrão.
2. Um segundo fator proporcional ao risco — data de nascimento já cadastrada, ou um código por
   e-mail para o endereço **cadastrado** — antes de mostrar qualquer processo.
3. Avisar o cadastro do cliente (e-mail) a cada consulta verificada, para ele perceber uso indevido.
4. O Bot registrar um marcador no lugar do documento, em vez de depender da ocultação do Core.

## Consequências

- Três códigos novos (`ATENDIMENTO_CONSULTA_*`, faixa 2511–2513) e uma tabela com RLS, `UPDATE` por
  coluna e sem `DELETE` (`015_atendimento_consulta.sql`).
- O Bot precisa de ajuste para usar o fluxo e, idealmente, para não registrar o documento
  (`docs/contrato-core.md` do repositório do Bot espelha este contrato).
- O WhatsApp/Meta retém a mensagem digitada pelo cliente; isso está fora do alcance do Core e do Bot.
- Se um dia houver segundo fator, ele entra como nova etapa antes de `Verificado`, sem mudar o resto.
