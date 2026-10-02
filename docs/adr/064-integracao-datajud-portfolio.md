# ADR-064: Integração com a API Pública do DataJud (uso portfólio)

**Status:** Aceito — com prazo de validade explícito (ver "Consequências")

## Contexto

Cadastrar um processo à mão significa digitar tribunal, vara, classe, assunto e data de
distribuição — dados que já existem numa base pública do CNJ. Para um escritório com dezenas
de processos a migrar, é a diferença entre adotar o sistema e desistir dele.

A **API Pública do DataJud** (`api-publica.datajud.cnj.jus.br`) expõe exatamente isso: um
Elasticsearch por tribunal, com chave de API pública divulgada pelo próprio CNJ.

O problema é o Termo de Uso. A cláusula 3.3 proíbe uso comercial; a 3.8 proíbe "explorar
comercialmente qualquer informação derivada". E consumir a API implica aceitá-lo — não há
cadastro nem aceite explícito, a primeira requisição é o aceite.

## Decisão

Usar o DataJud **exclusivamente para fins de portfólio**, e estruturar o código para que
trocar de fonte seja barato.

### O uso é não comercial, e isso está registrado em três lugares

`README.md`, rodapé da aplicação e `docs/compliance/portfolio.md`. Nenhum deles é decorativo:
são o que sustenta o enquadramento. Ver `docs/compliance/datajud-termo.md` para a análise
completa, incluindo o que muda se o projeto for comercializado.

### A porta fica no módulo Legal, não no de integração

```
Legal.Application/Abstractions/IConsultaProcessoTribunal   ← a porta
Legal.Infrastructure/Integrations/DataJudConsultaProcessoTribunal  ← o adaptador
Integrations.CNJ/.../DataJudClient                         ← o HTTP
```

O normal neste projeto seria o handler depender direto de `IDataJudClient`, como o
`CnjCaptureJob` depende de `ICnjClient`. Aqui não: **esta fonte tem prazo de validade**. Com
a porta em Legal, trocar o DataJud por um provedor comercial é escrever outro adaptador — o
handler, o endpoint e a tela não mudam. Também é o que permite a `Legal.Api` não depender de
`Advocacia.Modules.Integrations`, regra que `LegalModuleConventionTests` já cobrava.

O adaptador traduz o vocabulário: códigos da tabela unificada e nível de sigilo ficam nele;
o que segue é `ProcessoTribunal`, no vocabulário do escritório.

### O tribunal sai do número CNJ, não do usuário

`TribunalResolver` (em `Legal.Domain.Shared`) deriva a sigla dos campos J e TR do número
único. Pedir o tribunal ao usuário abriria espaço para ele divergir do número — e um processo
do TJSP consultado no índice do TJRJ volta "não encontrado", sem erro nenhum que denuncie a
causa.

O mesmo algoritmo existe em TypeScript (`getTribunalFromCnj`), para a tela habilitar o botão
e dizer "consultar no TJSP" antes de gastar uma chamada. Os dois têm os mesmos casos de teste.

### Não encontrado é sucesso, não erro

`Found: false` com HTTP 200. A tela precisa distinguir "o tribunal não conhece este processo"
(preencha manualmente) de "a consulta falhou" (tente de novo). Devolver erro nos dois casos
apagaria a diferença — e nem todo processo está no DataJud: depende do que o tribunal enviou.

### O preenchimento exige confirmação

A consulta abre um modal com o que foi encontrado; o formulário só muda se a pessoa clicar em
"Usar estes dados". E campo ausente na resposta **não** entra no patch: se entrasse como
string vazia, apagaria o que já havia sido digitado — o auto-preenchimento viraria
auto-apagamento.

Cliente e responsável nunca vêm da consulta: são decisão do escritório, não do tribunal.

### Contenção: cache e limite

- **Cache de 1h** por `(tribunal, processo)` no Redis, incluindo o resultado "não encontrado"
  — que é justamente o mais reconsultado, porque o usuário acha que errou o número.
- **10 consultas/min por usuário**, política nomeada somando-se ao limitador global.
- Uma consulta por clique. Não há consulta em massa nem em lote.

Nada disso é exigido pelo termo. É o que torna o uso defensável na prática, e o CNJ pede
expressamente que não se façam consultas em massa.

## Consequências

**Positivas:**

- O cadastro de um processo cai de ~8 campos digitados para 2 (cliente e responsável).
- Trocar de fonte custa um adaptador, não uma refatoração.
- O erro mais caro — número válido apontando para o tribunal errado — é impossível: o
  tribunal é derivado do número.

**Negativas / trade-offs:**

- **Esta decisão tem prazo de validade.** No dia em que o Iustitia for comercializado, o
  DataJud sai. Não é "revisar depois": é condição do enquadramento.
- **Risco jurídico reduzido, não eliminado.** A fronteira entre portfólio e produto é de
  interpretação, e o sistema está em uso real por uma advogada. Ver `datajud-termo.md`.
- **Dependência de serviço público instável.** Latência medida em setembro de 2026: **~12 a
  14 segundos** na primeira consulta, contra os ~500ms que a documentação sugere. O timeout
  é de 30s e o circuito abre cedo, mas a espera é visível na tela.
- **A chave pode mudar sem aviso.** Fica em user-secrets/Key Vault, nunca no appsettings
  versionado — mesmo sendo pública, para que trocá-la seja um comando, não um commit.
- **Nem todo tribunal está lá.** O STF não publica no DataJud; a tela desabilita o botão com
  a explicação, em vez de gastar a chamada e devolver erro.

## Uma descoberta que só apareceu consultando de verdade

A API devolve `dataAjuizamento` no formato **compacto** (`"20181029000000"`) e
`movimentos[].dataHora` em **ISO 8601** (`"2018-10-30T14:06:24.000Z"`) — na mesma resposta. A
documentação mostra ISO nos dois.

A primeira versão do cliente lia só ISO e devolvia `CNJ_INVALID_RESPONSE` sobre um HTTP 200
perfeitamente válido. `DataJudDateTimeConverter` aceita os dois formatos, e data ilegível vira
`null` em vez de exceção: perder a data degrada o auto-preenchimento, derrubar a consulta
cancela o recurso.

É o mesmo padrão da Etapa 1.3 com o DJEN, onde o `count` da API mentia — documentação de API
pública descreve a intenção, não o que o serviço faz.

## Alternativas rejeitadas

- **Provedor comercial desde já** (Escavador, Judit, Digesto): resolve o problema jurídico e
  custa assinatura mensal por um sistema que não gera receita. É o caminho *se* houver
  comercialização.
- **Raspagem dos portais dos tribunais**: troca um problema jurídico por um maior — os termos
  de uso em geral proíbem automação — além de frágil e caro de manter.
- **Não integrar**: o cadastro manual funciona. Mas migrar um acervo inteiro à mão é o tipo
  de atrito que faz um sistema ser abandonado na primeira semana.
