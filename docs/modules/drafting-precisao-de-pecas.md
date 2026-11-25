# Módulo Drafting — como chegar a peças precisas (investigação e plano)

> **Natureza deste documento:** investigação e proposta, **não** decisão. Escrito em 25/11/2026 a partir do código do
> módulo (commit atual), do conteúdo real do repositório `ctresb/leis` (commit `c9f21b4`, 02/11/2026) e de buscas na web.
> **Atualização (25/11/2026):** após liberação de rede, foram lidos diretamente os metadados do Hugging Face, a
> documentação da Vast.ai e a API do portal de dados abertos do STJ; os pontos correspondentes abaixo estão marcados
> **[verificado]**. Seguem **[não verificado]** o BNP/Pangea (não respondeu) e o STF (não consultado).

## 1. Resumo

1. **A precisão não vem do modelo; vem do que o cerca.** Trocar `qwen3:8b` pelo Jurema-7B melhora o vocabulário e o
   conhecimento jurídico de base, mas um modelo de 7 B parâmetros continua errando ao citar, ao seguir roteiros longos e
   ao raciocinar sobre vários dispositivos. Peça "como no mundo real" exige: **modelo de peça estruturado + fatos
   estruturados + recuperação boa + citação verificável por construção + avaliação contínua com advogados**.
2. **O acervo `ctresb/leis` cobre Constituição e leis muito bem, mas a "jurisprudência" dele NÃO tem acórdãos.** São
   precedentes qualificados: súmulas, temas de repercussão geral e repetitivos, OJs do TST, controle concentrado. Uma
   petição real cita também acórdãos ("STJ, REsp 1.234.567/SP, Rel. Min. X, DJe …", com ementa). Isso não existe hoje no
   sistema e é a maior lacuna de conteúdo.
3. **Licença do Jurema-7B: Apache 2.0 [verificado].** Os metadados do Hugging Face trazem `license: apache-2.0`, e o
   formulário de acesso diz que a licença "permite uso, modificação, redistribuição e **uso comercial**". O acesso é
   restrito (`gated: auto`): exige aceite de diretrizes de uso, com registro de contato. Não é impedimento comercial.
4. **O Vast.ai não garante que uma instância parada volte.** A documentação da Vast diz que, ao parar, a GPU pode ser
   realocada a outro cliente; reiniciar fica em "scheduling" sem prazo até a GPU liberar. O orquestrador atual trata isso
   como falha após 10 minutos, sem plano B. É o maior risco operacional do desenho atual.
5. **Sem um conjunto de avaliação com advogados, nada do que está abaixo é mensurável.** É o primeiro passo.

## 2. Onde o módulo está hoje

| Capacidade | Estado | Observação |
|---|---|---|
| Geração assíncrona, fila, GPU sob demanda (Vast) | ✅ | Orquestrador liga e desliga a instância |
| Prompt de sistema com regras de não invenção | ✅ | `SystemPrompt` v2; 15 regras |
| Busca híbrida em normas | ✅ | Vetor (BGE-M3) + texto completo do PostgreSQL + RRF |
| Reranking | ⚠️ por **regras** | Hierarquia, vigência, área, recência. Sem modelo cross-encoder (declarado fora do escopo) |
| Validação pós-geração das citações | ✅ determinística | Confere rótulos contra o contexto; bloqueia exportação se houver pendência |
| Coleção `modelos_de_peca` | ❌ criada, **sem ingestão e sem uso** | Não há modelo de peça nenhum |
| Geração | ❌ **uma chamada** para a peça inteira | Todo o contexto de uma vez, sem etapas |
| Entrada | ⚠️ texto livre (`fatos`, `pedidos`) | Sem qualificação, cronologia, valor da causa, documentos |
| Consulta de busca | ⚠️ primeiros 2.000 caracteres dos fatos | Uma consulta só; os fatos não são a pergunta jurídica |
| Jurisprudência | ⚠️ só precedentes qualificados | Sem acórdãos; sem atualização própria (snapshot de 15/10/2026) |
| Avaliação de qualidade | ❌ | Sem conjunto de referência, sem métrica, sem regressão |

## 3. Onde a precisão se perde (diagnóstico)

1. **O modelo escreve a citação.** Ele digita `[Lei nº 10.406, art. 186]` do zero. O verificador depois tenta pegar o erro.
   O desenho correto inverte: o modelo só pode apontar **identificadores de evidência** (`[E3]`), e o sistema converte em
   citação formal a partir do dado verificado. Citação inventada deixa de ser possível, em vez de apenas detectável.
2. **A consulta de busca é a descrição dos fatos.** Fatos ("o réu inscreveu meu cliente no SPC sem notificação") não
   casam bem com o texto da lei ou da tese ("art. 43, §2º, CDC; Súmula 359 do STJ"). Falta um passo que transforme o caso em
   **questões jurídicas** e busque cada uma.
3. **Uma chamada para tudo.** Um modelo de 7 B com 8.000 tokens de contexto e um roteiro de oito seções perde o fio. Peça
   por **seção**, cada uma com a sua evidência e a sua instrução, rende mais e é verificável por parte.
4. **Não há modelo de peça.** O que torna uma petição "de verdade" é a forma: os requisitos do art. 319 do CPC, o
   endereçamento correto, a qualificação, o valor da causa, a opção por audiência de conciliação, os pressupostos do
   recurso. Isso é conhecimento **estruturado**, não algo que se espera que o modelo lembre.
5. **O reranking não olha o texto.** Regras de hierarquia ajudam, mas só um cross-encoder diz qual trecho responde à
   pergunta.
6. **Nenhuma medição.** Sem gabarito, não se sabe se uma mudança melhorou ou piorou.
7. **Fatos e partes em texto livre**, então não há como conferir mecanicamente que a minuta usou o nome, o valor e as datas
   certos.

## 4. O Jurema

### O que se sabe (de fontes secundárias; **[não verificado]** no cartão original)

- Família de modelos jurídicos da NeuralMind com o Escavador; o Jurema-7B é um **ajuste fino do Qwen2.5-7B-Instruct**,
  publicado em jul/2025.
- Treino com dados **sintéticos** derivados de documentos jurídicos brasileiros, em boa parte perguntas e respostas.
- Avaliado em BLUEX, ENEM e OAB; no OAB, 0,668 contra 0,533 do modelo base (segundo um guia de 2026, fonte secundária).
- **[verificado]** Base `Qwen/Qwen2.5-7B-Instruct` (arquitetura `qwen2`), 4 arquivos `safetensors` (~15 GB), criado em
  25/07/2025, última alteração 28/07/2025 (**sem atualização desde então**), 136 downloads. **Existem GGUF da comunidade**
  (`Jurema-7B-Q4_K_M-GGUF`, `Q8_0`), portanto roda no Ollama atual sem mudar o serviço; e há um LoRA e variantes ONNX de
  terceiros (LGPD). Qualidade dessas quantizações de terceiros: não avaliada.
- Há uma **API Jurema** hospedada com modelo mais poderoso (detalhes técnicos não encontrados).
- Aviso dos autores: pode alucinar informação jurídica.

### O que isso significa para o projeto

- **Licença/uso:** resolver primeiro (seção 1, item 3). Se for só pesquisa, o caminho comercial é a API da NeuralMind
  (contrato) ou outro modelo.
- **Os benchmarks medem outra coisa.** OAB é prova de múltipla escolha. Escrever uma petição fiel ao contexto recuperado
  depende de **seguir instruções longas, usar o contexto sem inventar e redigir bem** — nada disso é medido por esses testes.
  Um ajuste fino em perguntas e respostas sintéticas pode até piorar a redação longa (**hipótese**, a testar).
- **Serve-se como o Qwen2.5-7B** (mesma arquitetura): vLLM a partir dos pesos BF16, ou Ollama a partir do GGUF. Como o modelo base tem
  contexto nativo de 32 mil tokens (**[não verificado]**), os 16.384 atuais cabem.
- **Memória (estimativa, a confirmar):** ~15 GB de pesos em BF16 + cerca de 56 KiB de cache por token de contexto
  (28 camadas × 4 cabeças KV × 128 × 2 bytes × 2) → ~1 GB por sequência de 16 k. Cabe com folga numa GPU de 24 GB.
- **Decisão por medição, não por nome:** rodar o mesmo conjunto de avaliação (seção 10) com Jurema-7B, `qwen3:8b` e um modelo
  maior (14–32 B quantizado). Modelos maiores costumam seguir roteiros longos melhor; o custo é VRAM. Só os números
  respondem.

## 5. Conteúdo

### 5.1 Constituição e leis (`ctresb/leis`)

Boa base: Markdown com metadados (`situacao`, `versao_texto`, `fonte`). O módulo já ingere, faz chunking por artigo e
guarda vigência. Pendências:

- **Snapshot, não fluxo.** O repositório é atualizado por quem o mantém; o `baixado_em` dos registros é 15/10/2026. Lei
  nova, alteração e revogação só entram quando o repositório as incorpora. Definir **cadência e monitoramento** (diff diário
  do repositório) e exibir a data da base na minuta ("legislação conforme base de DD/MM/AAAA").
- **Texto compilado vs. vigência por data.** Peça refere-se ao fato; a norma aplicável pode ser a da época. Hoje a vigência
  é um campo; um motor de vigência por data está fora do escopo e é caro. Tratar como **alerta ao advogado**, não como
  cálculo automático.

### 5.2 Jurisprudência — o que existe e o que falta

Conteúdo real de `jurisprudencia/` (11.709 arquivos): STF controle concentrado 5.198 · STF repercussão geral 1.494 · STF
súmulas 736 · súmulas vinculantes 62 · STJ repetitivos 1.474 · STJ súmulas 649 · TST OJs 694 · TST súmulas 463 · TNU 483 ·
TRF4 136 · TCU etc. As fontes declaradas nos arquivos são o **Banco Nacional de Precedentes do CNJ**
(`pangeabnp.pdpj.jus.br/api/v1/precedentes`) e os **dados abertos do STJ**.

Problemas de qualidade já vistos nos arquivos (o pipeline precisa tratá-los, não repassá-los ao modelo):

- Súmula vinculante nº 1: campo **"Tese: não informado"**. Um chunk assim não pode ser apresentado como tese.
- Controle concentrado (ADC, ADI…): o arquivo é um **JSON bruto do CNJ**, com `data` vazia e histórico em lista.
- Há precedentes com situação ("Trânsito em Julgado", "APROVADO") que precisa virar **status de vigência** (cancelada,
  superada, revisada) para o modelo e o verificador usarem.

**O que falta para peças reais: acórdãos.** Fontes a avaliar, com o que se sabe:

| Fonte | O que se sabe | Confiança |
|---|---|---|
| **STJ — Dados Abertos** | **[verificado]** Portal CKAN (`/api/3/action/package_search`). Licença dos conjuntos: **Creative Commons Atribuição** (uso comercial permitido com atribuição). Dois conjuntos relevantes: **(1) "Íntegras de Decisões Terminativas e Acórdãos do DJe"** — texto completo (TXT em ZIP) + JSON de metadados (`processo`, `ministro`, `tipoDocumento`, `teor`, `assuntos` em códigos CNJ, datas), **2.662 arquivos de 04/01/2021 a out/2026, ~10,8 GB compactados, atualização diária** (o último arquivo é de 24/11/2026); **(2) "Espelhos de acórdãos"** por Turma e Seção — só os acórdãos que a Secretaria de Jurisprudência trata documentalmente (os de teses novas), atualização mensal até 31/10/2026. Sem API de busca: é download em lote. **Não há campo de ementa separado nem de status do precedente**: a ementa está dentro do texto e precisa ser extraída. | **verificado** (metadados e dicionário lidos; o termo de uso em PDF não foi relido) |
| **CNJ BNP/Pangea** | Substituiu o BNPR em 2023; plataforma de pesquisa de **precedentes qualificados**; tribunais são obrigados a alimentá-la (Res. CNJ 444/2022). O `ctresb/leis` já consome um endpoint `api/v1/precedentes`. Não achei documentação pública atual da API. | **[não verificado]** |
| **STF** | Não pesquisado a fundo (portal bloqueado). | desconhecida |
| **TJs e TRFs** | Cada tribunal tem seu sistema; coleta costuma ser por varredura do portal, com termos de uso e fragilidade próprios. | desconhecida |
| **DataJud** | **Proibido para uso comercial** (`docs/compliance/datajud-termo.md`) e não traz texto de decisão. | verificado no repo |
| **Provedor comercial de jurisprudência** | Alternativa que resolve cobertura e atualização por contrato. Nenhum provedor foi avaliado aqui. | — |

Sobre direitos autorais: decisões judiciais e atos oficiais não são objeto de proteção autoral (Lei 9.610/98, art. 8º, IV).
Isso não dispensa os **termos de uso** de cada fonte nem a LGPD (acórdãos contêm nomes de partes).

**Como indexar acórdão (proposta):**

- Unidade = **ementa + tese** (um chunk, com metadados); trechos do voto só como segundo nível, recuperados depois que a
  ementa casou. Metadados obrigatórios: tribunal, órgão julgador, classe e número, relator, data de julgamento e de
  publicação, tema/súmula relacionados, **situação** (vigente, superado, com repercussão geral…).
- **Escopo seletivo:** o universo é de milhões de decisões. Começar por precedentes qualificados (já existem) + acórdãos
  recentes do STJ e do STF nas áreas do escritório; ementas para a cauda longa.
- **Atualização incremental diária** por data de publicação, com registro da data da última sincronização.
- **Verificação por número:** toda citação de acórdão na minuta é conferida contra o índice (número, classe, UF). Se não
  existir no índice, vira "NÃO VERIFICADA" e bloqueia a exportação. É o que impede o erro mais grave: **jurisprudência
  inexistente**.

### 5.3 Modelos de peça

Hoje não existem. O que fazer:

- **Fonte primária: as peças aprovadas do próprio escritório.** São as que refletem o estilo e a tese da casa e não têm
  problema de autoria. Anonimizar partes, dividir em **seções**, anotar tipo, área, rito e tribunal.
- **Fontes públicas:** formulários e modelos de tribunais e da OAB têm termos próprios; **livros e acervos de modelos
  comerciais têm direito autoral — não ingerir sem licença.**
- **Modelo como dado, não como texto solto.** Para cada tipo de peça: lista de seções, **checklist de requisitos legais**
  (ex.: art. 319 do CPC para a petição inicial — juízo, qualificação das partes, fatos e fundamentos, pedido, valor da
  causa, provas, opção por audiência de conciliação; pressupostos de cada recurso), roteiro de cada seção, frases-padrão do
  escritório e **o que não pode faltar**. O checklist vira **validação determinística** (a minuta tem endereçamento? valor da
  causa? pedido de citação?).
- Servir o modelo de peça ao LLM como **esqueleto + exemplos de seção** (poucos, escolhidos por tipo/área), e não como
  documento inteiro no contexto.

## 6. Arquitetura-alvo do pipeline

```
Formulário estruturado do caso ─▶ (1) Ficha do caso (JSON validado por esquema)
   partes, cronologia, valor, documentos, pedidos        │
                                                         ▼
                              (2) Questões jurídicas + consultas (LLM, saída estruturada)
                                                         │   N questões, cada uma com termos e dispositivos prováveis
                                                         ▼
   (3) Recuperação por questão: denso + esparso (BGE-M3) ─▶ RRF ─▶ cross-encoder (top 50 → 8) ─▶ fichas de evidência
       filtros: área, tribunal/UF, vigência, hierarquia           cada evidência com ID estável [E1]…[En]
                                                         │
   (4) Modelo de peça (esqueleto + checklist) ───────────┤
                                                         ▼
   (5) Redação POR SEÇÃO: só as evidências da seção; o modelo cita apenas [E#] e transcreve trecho entre aspas
                                                         ▼
   (6) Verificação: [E#] existe? aspas ⊂ texto da evidência? acórdão existe no índice? nomes/valores/datas = ficha?
       checklist do modelo de peça completo? norma revogada? ─▶ reparo (re-gera só a seção com erro) ou sinaliza
                                                         ▼
   (7) Resolução das citações: [E3] ─▶ "STJ, REsp …, Rel. …, DJe …" + link da fonte; exportação DOCX; bloqueio se pendente
```

Pontos que tornam o resultado **estruturalmente** mais confiável:

- **Saída estruturada** nas etapas 1–2 (esquema JSON imposto na decodificação, não só pedido no prompt).
- **Citação por identificador**: o modelo não digita número de lei nem de processo.
- **Transcrição entre aspas conferida por substring** contra o texto da evidência (o validador atual já olha aspas ≥ 60 caracteres;
  passa a ser exigência por afirmação jurídica relevante).
- **Pseudonimização reversível das partes** antes de enviar ao modelo: nomes, CPF, CNPJ e endereços viram `[PARTE_A]`,
  `[CPF_1]` e voltam na etapa 7. O modelo não precisa dos dados reais para redigir, e isso reduz o que vai para um host de terceiro.
- **Seção refeita, não peça refeita**: erro numa seção reprocessa só ela.

## 7. Recuperação — melhorias, por retorno esperado

1. **Cross-encoder (reranker de modelo).** O `bge-reranker-v2-m3` aparece como referência em estudos de busca jurídica em
   português. Roda na mesma GPU (porte de ~0,5 B). Substitui/complementa o reranking por regras, que continua como sinal.
2. **BGE-M3 esparso + denso no mesmo modelo.** Hoje o "lexical" é o texto completo do PostgreSQL; o BGE-M3 produz pesos
   esparsos nativamente, o que melhora termos jurídicos e números.
3. **Várias consultas por questão** (reescrita + palavras-chave + dispositivos prováveis), com fusão RRF. Um estudo de 2026
   (benchmark JUÁ) relata que sistemas com RRF tiveram a melhor efetividade agregada.
4. **Filtros de metadados antes da busca** (área, tribunal/UF, situação).
5. **Embeddings ajustados ao domínio** — só depois de medir. Há trabalho brasileiro mostrando ganho ao ajustar o BGE-M3 em
   decisões e legislação de um tribunal de contas; exige dados rotulados e é a última etapa.

## 8. Servir o modelo no Vast.ai

### 8.1 Ollama → vLLM

O módulo fala com o Ollama hoje. Para o Jurema (pesos BF16 do Hugging Face, sem GGUF informado) e para as etapas de saída
estruturada, o **vLLM** é o caminho natural: expõe API compatível com OpenAI, impõe **esquema JSON na decodificação**
(disponível desde a 0.8.5, segundo um artigo da Red Hat; rápido no motor V1), e faz cache de prefixo para o prompt de sistema
e o modelo de peça repetidos. Custo: trocar o `OllamaService` por um cliente OpenAI-compatível e subir **três serviços** na
GPU (geração, embeddings, reranker). Como a porta `ILlmService` já isola o provedor, a mudança é localizada.

### 8.2 O risco de parar a instância — **[docs da Vast lidas só por trechos de busca]**

- **[verificado, docs.vast.ai/guides/reference/troubleshooting]** "When you stop an instance, the gpu(s) it was using may
  get reassigned… If another high priority job is currently using any of the same gpu(s), your instance will be stuck in
  'scheduling' phase until the conflicting jobs are done… **we recommend not stopping an instance unless you are ok with
  the risk of waiting a while to restart it.**" Também: saldo zerado para as instâncias automaticamente.
  Ou seja, o desenho "parar por ociosidade em 10 min" contraria a recomendação do próprio fornecedor.
- O `GpuOrchestrator` aguarda `StartTimeoutMinutes` (10) e marca **falha**, e a peça falha com `DRAFTING_GPU_UNAVAILABLE`.
  Não há segundo caminho.

Mitigações, da mais simples à mais robusta:

1. **Janela de funcionamento em vez de parar por ociosidade**: ligar em horário comercial e desligar à noite. Menos paradas,
   menos chance de perder a GPU; custo maior.
2. **Pesos fora da instância** (bucket próprio) e **imagem/template pronto**, para que **criar uma instância nova** seja um
   fallback automático (minutos, não horas). O código hoje nunca cria nem destrói instâncias — isso é decisão de produto.
3. **Plano B de geração** com contrato de tratamento de dados (ex.: API do Jurema ou outro provedor), usado só quando a GPU
   não volta, e **somente com dados pseudonimizados**.
4. Avaliar **Vast Serverless** (autoscaling, scale-to-zero) **[não verificado]**.
5. Mostrar ao usuário **"GPU indisponível, tente em alguns minutos"** com a peça re-enfileirada automaticamente, em vez de falhar.

### 8.3 Sigilo e LGPD

Os hosts da Vast são de terceiros (o módulo já registra isso). Para minuta com dados de cliente: preferir hosts de
datacenter **[a verificar na documentação da Vast: tipos de host, certificações, isolamento]**, **pseudonimizar** antes de
enviar, não persistir dado de cliente no disco da instância, TLS fim a fim e contrato/DPA. A OAB e a LGPD tratam o escritório
como responsável: a decisão de enviar dado a terceiro é dele.

## 9. Fine-tuning

Só depois de a avaliação mostrar uma lacuna que a recuperação e os modelos de peça não fecham. Se vier: **LoRA** sobre o
modelo escolhido, treinado com as **peças aprovadas do escritório** (pares caso → seção redigida, com evidência no prompt),
para estilo e estrutura — **não** para "ensinar leis" (isso é papel do RAG e envelhece). Exige centenas a milhares de
exemplos revisados; é trabalho de curadoria jurídica, não de engenharia.

## 10. Avaliação (pré-requisito de todo o resto)

- **Conjunto de referência:** 40–60 casos reais anonimizados, distribuídos por tipo de peça e área, **cada um com a peça
  aprovada por advogado e a lista de dispositivos e precedentes que ela deveria citar**.
- **Métricas automáticas** (rodam a cada mudança, no CI):
  - **Precisão de citação:** % das citações que existem no índice e cujo trecho transcrito está na fonte. Meta: ~100%; o
    resto bloqueia a exportação.
  - **Cobertura de fundamentos:** % dos dispositivos e precedentes do gabarito que apareceram.
  - **Completude estrutural:** % do checklist do modelo de peça atendido.
  - **Fidelidade aos fatos:** nomes, valores e datas da minuta batem com a ficha do caso.
- **Métricas humanas** (amostra, por advogado com rubrica fixa): correção jurídica, adequação ao rito, tempo para deixar a peça
  pronta para protocolo. **O tempo de revisão é a métrica de negócio.**
- **Juiz LLM** só como triagem, nunca como aprovação. Um estudo de 2026 usa um juiz grande validado por concordância com um
  oráculo e κ de Cohen — o mesmo cuidado vale aqui.
- **Regressão:** o teste de regressão de prompt que já existe para a Anthropic ganha versão para a geração por seção.

## 11. Roteiro

| Fase | Entrega | Saída (critério) |
|---|---|---|
| 0 | Conjunto de referência + harness + linha de base com o modelo atual | Números reproduzíveis por tipo de peça |
| 1 | Ficha do caso, modelos de peça com checklist, geração por seção, citação por identificador, verificação por aspas, pseudonimização | Precisão de citação medida e acima da base; checklist ≥ 95% |
| 2 | vLLM + comparação Jurema-7B × `qwen3:8b` × modelo maior; fallback de GPU | Decisão de modelo por número; GPU indisponível não derruba a peça |
| 3 | Reranker cross-encoder, BGE-M3 esparso, várias consultas, filtros de metadados | Cobertura de fundamentos sobe sem queda de precisão |
| 4 | Ingestão de acórdãos (fonte verificada), sincronização diária, verificação por número | Zero citação de acórdão inexistente no conjunto de referência |
| 5 | (se necessário) LoRA com peças do escritório | Redução medida no tempo de revisão |

## 12. O que não foi verificado e decisões suas

**Ainda não verificado:**

- Contexto máximo e dados de treino do Jurema-7B (o cartão `README.md` é restrito; só os metadados públicos foram lidos).
- API do BNP/Pangea (`pangea.cnj.jus.br` não respondeu): documentação, termos e limites.
- Vast: tipos de host e certificações, Vast Serverless e templates de vLLM (só a página de problemas e a de gerenciamento
  foram lidas).
- Portal do STF e dos tribunais de interesse; termo de uso do STJ em PDF.

**Decisões do produto:**

1. A fonte de acórdãos: dados abertos do STJ/STF, coleta por tribunal ou provedor comercial.
2. Quem fornece e revisa os modelos de peça (o escritório) e quantas áreas/tipos entram primeiro.
3. Política de GPU: janela fixa, parar por ociosidade com fallback, ou criar instância sob demanda; e se há plano B em API de terceiro.
4. Aceitar ou não enviar dado (mesmo pseudonimizado) a um host de terceiro.

## Fontes consultadas

- Jurema-7B: [Hugging Face](https://huggingface.co/Jurema-br/Jurema-7B), [Blog do Escavador](https://www.escavador.com/blog/jurema-7b-o-primeiro-llm-juridico-brasileiro-de-codigo-aberto-treinado-com-dados-nacionais/), [guia de 2026](https://x-apps.com.br/llm-local-direito-brasileiro-mac-apple-silicon/) (apenas trechos de busca).
- RAG jurídico em português: [Grounded in Law (PROPOR 2026)](https://preview.aclanthology.org/new-sigs/2026.propor-2.9/), [BM25 × BGE-M3 em SLMs (ENIAC)](https://sol.sbc.org.br/index.php/eniac/article/view/46934), [BGE-M3 ajustado em decisões e legislação (ENIAC)](https://sol.sbc.org.br/index.php/eniac/article/view/38746).
- STJ: [portal de dados abertos (CNJ)](https://www.cnj.jus.br/portal-de-dados-abertos-do-stj-oferece-informacoes-em-formato-legivel-por-maquina/), [notícia do STJ](https://www.stj.jus.br/sites/portalp/Paginas/Comunicacao/Noticias/20052022-Novo-Portal-de-Dados-Abertos-amplia-transparencia-de-acoes-realizadas-no-STJ.aspx), [termo de uso](https://bdjur.stj.jus.br/jspui/bitstream/2011/179190/Termo%20de%20Uso%20-%20Dados%20Abertos.pdf), [plano de dados abertos](https://bdjur.stj.jus.br/jspui/bitstream/2011/163765/plano_dados_abertos_STJ.pdf).
- CNJ: [Banco Nacional de Precedentes](https://www.cnj.jus.br/tecnologia-da-informacao-e-comunicacao/justica-4-0/banco-nacional-de-precedentes-bnp/), [manual do Pangea/BNP](https://www.cnj.jus.br/wp-content/uploads/2023/11/manual-de-usuario-pangea-bnp.pdf).
- Vast.ai: [solução de problemas ("scheduling")](https://docs.vast.ai/guides/reference/troubleshooting), [gerenciar instâncias](https://docs.vast.ai/guides/instances/manage-instances), [stop instance](https://docs.vast.ai/cli/reference/stop-instance).
- vLLM: [saídas estruturadas (Red Hat)](https://developers.redhat.com/articles/2025/06/03/structured-outputs-vllm-guiding-ai-responses).
