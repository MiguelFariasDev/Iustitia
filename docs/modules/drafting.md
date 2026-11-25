# Módulo Drafting — RAG jurídico e geração assistida de peças

Decisões: [ADR-072](../adr/072-geracao-de-pecas-com-rag-local.md) (GPU sob demanda),
[ADR-073](../adr/073-acervo-normativo-em-markdown.md) (acervo em Markdown),
[ADR-074](../adr/074-casos-busca-hibrida-e-revisao-humana.md) (casos, BM25, revisão humana).

Toda saída é **minuta sujeita a revisão humana**. O recurso nasce desligado (`Drafting:Enabled=false`).

Conteúdo: [fluxo](#fluxo) · [estrutura](#estrutura) · [endpoints](#endpoints) · [curl](#exemplos-com-curl) ·
[streaming](#streaming) · [acervo](#acervo-jurídico) · [busca](#busca-e-prompt) · [revisão](#validação-e-revisão-humana) ·
[configuração](#configuração) · [implantação](#implantação) · [checklists](#checklists) · [medições e limites](#medições-e-limites)

## Fluxo

```
 POST /casos ─▶ caso (unidade de isolamento)
 POST /casos/{id}/documentos ─▶ original cifrado no storage ─▶ fila
        Worker  Conversao (CPU)  PDF/DOCX/planilha/imagem+OCR/e-mail/áudio ─▶ Markdown + chunks
                Indexacao (GPU)  classificação ─▶ BGE-M3 + BM25 ─▶ Qdrant documentos_caso
 POST /pecas/gerar ─▶ valida, sanitiza, grava Pendente ─▶ 202
        Worker  PreparandoGpu ─▶ BuscandoDocumentosCaso ─▶ BuscandoJuridico
                ─▶ Gerando (Ollama, NDJSON) ─▶ Validando (citações) ─▶ Salvando ─▶ Concluido
 GET /pecas/{id}/stream (NDJSON) ou /status
 PUT /pecas/{id}/citacoes/{c} ─▶ POST /pecas/{id}/revisao ─▶ GET /pecas/{id}/exportar
```

A API nunca fala com Ollama, Qdrant de geração ou Vast.ai no meio da requisição: grava e enfileira. O Worker é o
único processo que liga e para a GPU. O stream lê o texto parcial do banco, então funciona com API e Worker
separados. Fila vazia por `VastAi:IdleTimeoutMinutes` **para** a instância; nada no código a destrói.

## Estrutura

A especificação pedia um projeto único; o código segue a arquitetura do repositório (Clean Architecture por módulo).

| Especificação | Onde está (`src/Modules/Drafting/…`) |
|---|---|
| Controllers | `Drafting.Api/{Casos,Pecas,Normas}Endpoints.cs` (minimal API, convenção do repositório) |
| DTOs | `Drafting.Application/Models/*` |
| Entities | `Drafting.Domain/{Casos,Pecas,Normas,Fila,Gpu,Seguranca}` |
| Interfaces | `Drafting.Application/Abstractions/*`, `Normas/NormaAbstractions.cs` |
| Services (caso, anexo, acesso) | `Drafting.Application/Casos/*` |
| Conversão, chunking, metadados | `Drafting.Application/Conversao/*`, `Drafting.Infrastructure/DocumentConversion/*` |
| OCR e transcrição | `Drafting.Infrastructure/{Ocr,Transcricao}` |
| Ingestão do acervo | `Drafting.Application/Normas/*`, `Drafting.Infrastructure/Normas/*` |
| Busca híbrida, reranking, contexto | `Drafting.Application/Busca/*` |
| Prompt, orçamento, geração, fila, GPU | `Drafting.Application/Services/*` |
| Validação de citações | `Drafting.Application/Validacao/*` |
| Qdrant, Ollama, Vast.ai, storage, DOCX | `Drafting.Infrastructure/{Qdrant,Ollama,VastAi,Storage,Exportacao}` |
| BackgroundServices | `Drafting.Infrastructure/BackgroundJobs/*` |
| EF Core e migrações | `Drafting.Infrastructure/Persistence/*`, `infra/supabase/migrations/{016,017,019}*.sql` |
| Testes | `tests/UnitTests/Drafting.*`, `tests/IntegrationTests/Drafting.IntegrationTests` |

## Endpoints

`/api/v1`, JWT, perfil advogado ou superior (salvo indicação). O escritório e o usuário vêm sempre do token.
Quem não tem acesso recebe **404**. Erros: [catálogo](../errors/catalog.md#drafting-26002699).

| Método | Rota | Função |
|---|---|---|
| POST | `/casos` | abre um caso (201); quem cria é responsável |
| GET | `/casos`, `/casos/{id}` | casos visíveis; detalhe com membros |
| PUT | `/casos/{id}` | altera; `encerrado` fecha ou reabre |
| PUT / DELETE | `/casos/{id}/membros[/{usuarioId}]` | concede ou revoga acesso (`Leitura`, `Escrita`) |
| POST | `/casos/{id}/documentos` | anexa (202); multipart `arquivo`, `restrito` |
| GET | `/casos/{id}/documentos[/{docId}]` | estado, classificação, confiança do OCR |
| GET | `…/{docId}/original`, `…/{docId}/markdown` | arquivo original; Markdown extraído |
| POST | `…/{docId}/revisar`, `…/{docId}/reprocessar` | confirma a leitura (OCR baixo, conteúdo suspeito); refaz |
| DELETE | `…/{docId}` | remove índice, arquivos e registro |
| POST | `/pecas/gerar` | pede a minuta (202); limite `ai-generation` |
| GET | `/pecas`, `/pecas/{id}`, `/pecas/{id}/status` | lista, minuta com fontes, andamento |
| GET | `/pecas/{id}/stream` | NDJSON; 409 se já terminou |
| POST | `/pecas/{id}/cancelar`, `/pecas/{id}/regenerar` | cancela; nova versão (`confirmar: true`) |
| GET | `/pecas/{id}/citacoes` | citações com o veredito automático |
| PUT | `/pecas/{id}/citacoes/{citacaoId}` | decide: `Confirmada`, `Corrigida` ou `Rejeitada`, com justificativa |
| POST | `/pecas/{id}/revisao` | conclui a revisão (`declaroRevisao: true`) |
| GET | `/pecas/{id}/exportar?formato=docx\|md` | exporta a peça revisada |
| GET | `/pecas/admin/metricas` | painel (sócio+) |
| POST | `/normas/admin/sincronizar[?reprocessar=true]` | pede ingestão do acervo (sócio+, 202) |
| GET | `/normas/admin/jobs`, `/normas/admin/colecoes` | histórico; estado das coleções (sócio+) |
| GET | `/normas/{id}` | norma por trás de uma citação |

Tipos de peça: `peticao_inicial`, `contestacao`, `replica`, `recurso_apelacao`, `embargos_declaracao`,
`agravo_instrumento`, `contrarrazoes`, `manifestacao`, `recurso_ordinario`, `recurso_especial`,
`recurso_extraordinario`, `contrato`, `parecer`. Áreas: `civel`, `trabalhista`, `previdenciario`, `tributario`,
`familia`, `consumidor`, `administrativo`, `empresarial`, `penal`, `constitucional`.

## Exemplos com curl

```bash
API=https://api.exemplo/api/v1
AUTH="Authorization: Bearer $JWT"

# 1. Abrir o caso
CASO=$(curl -sS -X POST "$API/casos" -H "$AUTH" -H 'Content-Type: application/json' -d '{
  "clienteId": "0b6f2f2e-0d55-4f1c-9f0c-0d6f3c1f7a11",
  "titulo": "Negativação indevida", "area": "consumidor", "uf": "CE"
}' | jq -r .id)

# 2. Dar acesso de leitura a colega (opcional)
curl -sS -X PUT "$API/casos/$CASO/membros" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"usuarioId":"5d0c…","permissao":"Leitura"}'

# 3. Anexar documentos (202: leitura e indexação são assíncronas)
DOC=$(curl -sS -X POST "$API/casos/$CASO/documentos" -H "$AUTH" \
  -F arquivo=@contrato.pdf -F restrito=false | jq -r .id)

# 4. Acompanhar: Recebido → Convertendo → Convertido → Indexando → Indexado
curl -sS "$API/casos/$CASO/documentos/$DOC" -H "$AUTH" | jq '{status, confiancaOcr, requerRevisaoHumana}'
curl -sS "$API/casos/$CASO/documentos/$DOC/markdown" -H "$AUTH"

# 5. Pedir a minuta
PECA=$(curl -sS -X POST "$API/pecas/gerar" -H "$AUTH" -H 'Content-Type: application/json' -d "{
  \"casoId\": \"$CASO\", \"tipoPeca\": \"peticao_inicial\", \"area\": \"consumidor\",
  \"fatos\": \"O autor teve o nome inscrito em cadastro de inadimplentes por dívida já paga.\",
  \"pedidos\": \"Declaração de inexistência do débito e indenização por dano moral.\",
  \"documentosIds\": [\"$DOC\"], \"dataFatos\": \"2026-03-10\"
}" | jq -r .id)

# 6. Acompanhar em tempo real (-N desliga o buffer) ou por consulta
curl -N -sS "$API/pecas/$PECA/stream" -H "$AUTH"
curl -sS "$API/pecas/$PECA/status" -H "$AUTH"

# 7. Revisar as citações
curl -sS "$API/pecas/$PECA/citacoes" -H "$AUTH" | jq '.citacoes[] | {id, texto, status, detalhe, bloqueante}'
curl -sS -X PUT "$API/pecas/$PECA/citacoes/$CITACAO" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"decisao":"Confirmada","justificativa":"Conferida no Planalto em 22/11/2026."}'

# 8. Concluir a revisão e exportar (antes disso: 409 DRAFTING_REVIEW_REQUIRED)
curl -sS -X POST "$API/pecas/$PECA/revisao" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"declaroRevisao": true}'          # ou com "textoFinal": "…" para gravar o texto corrigido
curl -sS -OJ "$API/pecas/$PECA/exportar?formato=docx" -H "$AUTH"

# Cancelar, regenerar
curl -sS -X POST "$API/pecas/$PECA/cancelar" -H "$AUTH"
curl -sS -X POST "$API/pecas/$PECA/regenerar" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"confirmar": true, "instrucoesAdicionais": "Enfatizar a responsabilidade objetiva."}'

# Acervo jurídico e painel (sócio+)
curl -sS -X POST "$API/normas/admin/sincronizar" -H "$AUTH"
curl -sS "$API/normas/admin/jobs" -H "$AUTH"
curl -sS "$API/normas/admin/colecoes" -H "$AUTH"
curl -sS "$API/pecas/admin/metricas" -H "$AUTH"
```

Resposta do `POST /pecas/gerar`:

```json
{"id":"8f0e…","status":"Pendente","mensagem":"Sua solicitação está na fila de processamento.",
 "criadoEm":"2026-11-22T14:03:10Z","statusUrl":"/api/v1/pecas/8f0e…/status","streamUrl":"/api/v1/pecas/8f0e…/stream"}
```

Falhas seguem ProblemDetails (RFC 7807), com o código do catálogo e sem modelo, endereço ou exceção.

## Streaming

`Content-Type: application/x-ndjson`, um objeto JSON por linha, descarregado a cada evento. Exemplo (valores ilustrativos):

```json
{"etapa":"PreparandoGpu","mensagem":"Preparando o ambiente de IA.","progresso":15,"tipo":"status"}
{"etapa":"BuscandoDocumentosCaso","mensagem":"Analisando anexos do caso.","progresso":25,"tipo":"status"}
{"etapa":"BuscandoJuridico","mensagem":"Buscando fundamentos jurídicos.","progresso":35,"tipo":"status"}
{"etapa":"Gerando","mensagem":"Gerando minuta.","progresso":40,"tipo":"status"}
{"conteudo":"EXCELENTÍSSIMO SENHOR DOUTOR JUIZ","tipo":"fragmento"}
{"conteudo":" DE DIREITO\n\nDOS FATOS\n","tipo":"fragmento"}
{"etapa":"Validando","mensagem":"Conferindo as citações da minuta.","progresso":93,"tipo":"status"}
{"solicitacaoId":"8f0e…","versao":1,"fontes":[{"id":"…","nomeArquivo":"Lei nº 8.078 de 11/09/1990","trecho":"…","score":0.91,"citada":true,"rotulo":"Lei nº 8.078 de 11/09/1990, art. 14","grupo":"LeisFederais","situacao":"Vigente"}],"avisos":[],"aviso":"…","tempoGeracaoMs":41200,"tipo":"fim"}
```

Erro no meio (o HTTP já é 200): `{"mensagem":"Não foi possível gerar a peça. Tente novamente.","solicitacaoId":"8f0e…","tipo":"erro"}`.

- Leia pelo campo `tipo`, não pela ordem dos campos.
- `status` `Pendente` depois de fragmentos = a geração foi reagendada: limpe o texto já mostrado.
- Desconectar encerra só o acompanhamento. Para interromper a geração, use `/cancelar`.

## Acervo jurídico

Lê uma pasta local com o repositório já baixado (`Ingestao:Modo=Local`, somente leitura) ou faz clone parcial
(`Git`). Entram apenas `constituicao.md`, `federal/` e `jurisprudencia/`. Ficam fora `_fonte/`, `.git/`, `scripts/`,
`estaduais/`, `municipais/`, `reguladores/`, `tratados/`, qualquer segmento iniciado por `_` e o que não for `.md`.

- **Comece pelo subconjunto** (`SomenteAltaUtilizacao=true`): Constituição, 35 códigos e leis de uso frequente,
  leis complementares e súmulas, repetitivos, repercussão geral e OJs. Desligue para ingerir tudo.
- **Incremental por SHA-256:** novo ou alterado reprocessa; igual não toca a GPU; removido some da busca e o
  histórico fica. Mudar chunking ou modelo de embedding exige `?reprocessar=true`.
- **Chunking jurídico:** artigo → § → inciso → alínea, 400–900 tokens, com norma e hierarquia em cada trecho.
- **Rastreabilidade:** fonte oficial, URN, arquivo, versão do texto e situação acompanham cada trecho até a citação.
- **Três camadas:** Markdown integral no storage · registro, versões e chunks no PostgreSQL · vetores e o texto do
  *trecho* no Qdrant. O Markdown completo nunca vai ao Qdrant.

| Coleção | Conteúdo | Escopo |
|---|---|---|
| `constituicao_federal`, `leis_federais`, `jurisprudencia` | acervo público | compartilhado |
| `documentos_caso` | anexos | tenant + caso, filtro obrigatório |
| `pecas_modelo` | reservada, sem alimentação | — |

## Busca e prompt

1. **Consulta estruturada** (tese, temas, dispositivos suspeitos) pelo LLM com JSON Schema; se falhar, por regras.
2. **Por coleção:** vetor denso (BGE-M3, 30) + BM25 (30) + referência exata no PostgreSQL → fusão RRF.
3. **Reranking por regras:** termos, fatos, pedidos, hierarquia, autoridade da fonte, vigência na data dos fatos,
   área, UF, tribunal e recência. Norma revogada perde pontos e é sinalizada, não descartada.
4. **Seleção:** até 3 trechos por norma, vagas por grupo (caso 4, Constituição 2, leis 4, jurisprudência 3).
   A reserva só vale com relevância ≥ `RelevanciaMinimaReserva`: trecho fraco não entra só para preencher vaga.
5. **Orçamento:** `NumCtx − NumPredict − prompt fixo − entrada`. Sem espaço mínimo, a solicitação é recusada.

O system prompt é fixo (v3, travado por SHA-256). A pessoa usuária não o altera: `InputSanitizer` e
`PromptInjectionGuard` filtram a entrada, e os anexos têm rótulos de seção neutralizados. Anexo com instruções ao
assistente é marcado como suspeito e fica fora da busca até revisão humana. É mitigação, não garantia.

## Validação e revisão humana

Formatos: `[NORMA, art. X]`, `[TRIBUNAL, súmula/tema/processo]` e `[ARQUIVO, p. X]`.

| Status | Significado | Bloqueia |
|---|---|---|
| `Validada` | confere com trecho recuperado | não |
| `PendenteDeVerificacao` | sem respaldo suficiente no contexto, ou apoiada em OCR/transcrição não conferidos | sim |
| `PossivelAlucinacao` | fonte, número, ementa ou aspas que não constam do contexto nem do acervo | sim |
| `ConflitoDeVersao` | há versões divergentes do dispositivo | não, avisa |
| `NormaRevogada` | fora de vigor | não, avisa |

A minuta só exporta depois de: decisão justificada em cada citação bloqueante, nenhuma citação rejeitada no texto
final e declaração de revisão. Mudar uma decisão reabre a revisão. Tudo fica na trilha de auditoria.

## Configuração

Segredos **só** por variável de ambiente ou cofre:

| Variável | Uso |
|---|---|
| `Drafting__Enabled` | liga o módulo (API e Worker) |
| `VastAi__ApiKey`, `VastAi__InstanceId` | controle da instância; `VastAi__Enabled=false` para Ollama sempre ligado |
| `Ollama__BaseUrl`, `Ollama__ApiKey` | endereço TLS do túnel; o mesmo token do nginx |
| `Qdrant__BaseUrl`, `Qdrant__ApiKey` | gRPC (6334) com TLS |
| `Ingestao__Storage__AccessKey`, `…__SecretKey` | R2/B2/S3 |
| `Encryption__*` | chaves de campo e de arquivo (ADR-068) |
| `Drafting__Transcricao__BaseUrl`, `…__ApiKey` | Whisper próprio; vazio = áudio recusado |

Seções não secretas em `src/Hosts/{Api,Worker}/appsettings.json`: `Drafting` (`Fila`, `Custo`, `Anexos`,
`Conversao`, `Ocr`, `Transcricao`), `Rag` (`Chunking`, `Busca`), `Ingestao` (`Storage`, `Filtros`), `VastAi`,
`Ollama`, `Qdrant`. API e Worker precisam dos mesmos valores de `Ingestao:Storage`, `Encryption` e `Qdrant`.

> **`NumCtx`.** O padrão 8192 comporta em média cinco trechos e uma minuta de ~2.500 tokens. Com 24 GB de VRAM use
> `Ollama__NumCtx=16384` e `Ollama__NumPredict=4096`; o contexto recuperado cresce sozinho.

> `http://` fora de loopback é recusado para Ollama, Qdrant e transcrição, salvo `AllowInsecureHttp` explícito.

## Implantação

### Desenvolvimento

```bash
docker compose -f infra/docker/docker-compose.yml up -d
docker compose -f infra/docker/docker-compose.ai.yml up -d qdrant
export ADVOCACIA_DB_CONNECTION="Host=localhost;Port=5434;Database=advocacia_db;Username=postgres;Password=postgres"
dotnet ef database update --project src/Modules/Drafting/Drafting.Infrastructure --context DraftingDbContext
for f in 016_drafting 017_normas_juridicas 020_drafting_casos; do
  psql "postgresql://postgres:postgres@localhost:5434/advocacia_db" -v ON_ERROR_STOP=1 -f infra/supabase/migrations/$f.sql
done
export Ingestao__DiretorioLocal=/caminho/para/leis Ingestao__WorkerHabilitado=true
dotnet run --project src/Hosts/Worker    # e, em outro terminal, src/Hosts/Api
```

No Supabase, `scripts/migrations/apply-supabase.sh` já inclui este módulo. Reversão do 020:
`infra/supabase/rollback/020_drafting_casos.down.sql`.

### Serviços sempre ligados (sem GPU)

API, Worker, PostgreSQL e Qdrant ficam **fora** da instância de GPU.

- **Qdrant:** v1.15.5, TLS, API key, disco cifrado, snapshot agendado e `nofile=65536`. Com o limite padrão, a
  carga em massa falha com "Too many open files".
- **Worker:** `src/Hosts/Worker/Dockerfile` já traz git, Tesseract (por), Poppler e LibreOffice. Volume em
  `/var/lib/advocacia`.
- **Storage:** R2 ou B2 (`Ingestao:Storage:Provedor=S3`), bucket privado, chave restrita ao bucket. `Local` só com
  volume compartilhado entre API e Worker.

### Vast.ai (GPU)

Arquivos: [`infra/vast/onstart.sh`](../../infra/vast/onstart.sh),
[`infra/ollama/Modelfile.jurema-7b`](../../infra/ollama/Modelfile.jurema-7b).

1. **Modelo.** O [Jurema-7B](https://huggingface.co/Jurema-br/Jurema-7B) (NeuralMind, base Qwen2.5-7B-Instruct,
   Apache 2.0) **não está na biblioteca do Ollama**. Aceite os termos no Hugging Face e baixe os safetensors. Leia as
   *Acceptable Use Guidelines*: o card descreve o modelo como de pesquisa e desenvolvimento.
2. **Instância.** Imagem `ollama/ollama`, GPU com ≥ 12 GB (q4_K_M, 8192) ou 24 GB (16384), disco de ≥ 60 GB,
   **nenhuma porta publicada**. Prefira host *secure cloud* e tipo *on-demand*: interrompível pode cair no meio da
   geração (a fila reprocessa, mas o tempo de GPU se perde).
3. **Túnel.** No painel do Cloudflare, crie um túnel nomeado com hostname `ia.SEU-DOMINIO` → `http://127.0.0.1:8080`
   (nunca 11434). Alternativa por arquivo: [`infra/cloudflared/config.example.yml`](../../infra/cloudflared/config.example.yml).
4. **Variáveis da instância:** `OLLAMA_PROXY_TOKEN` (`openssl rand -hex 32`) e `CF_TUNNEL_TOKEN`.
5. **Primeira carga.** Gere o GGUF ([abaixo](#gerar-o-gguf-uma-vez)) e copie-o para `/root/modelos/jurema-7b-q4_K_M.gguf`
   e o Modelfile para `/root/`, com uma chave SSH temporária. Use `infra/vast/onstart.sh` como *on-start script*. Ele é
   idempotente: cria `jurema-7b`, baixa `bge-m3`, sobe nginx e túnel.
6. **Conferir**, de fora:
   ```bash
   curl -s -o /dev/null -w '%{http_code}\n' https://ia.SEU-DOMINIO/api/tags                       # 401
   curl -s https://ia.SEU-DOMINIO/api/tags -H "Authorization: Bearer $OLLAMA_PROXY_TOKEN" | jq '.models[].name'
   curl -s -o /dev/null -w '%{http_code}\n' -X POST https://ia.SEU-DOMINIO/api/pull \
     -H "Authorization: Bearer $OLLAMA_PROXY_TOKEN"                                              # 404
   ```
7. **Worker:** `Ollama__BaseUrl=https://ia.SEU-DOMINIO`, `Ollama__ApiKey=$OLLAMA_PROXY_TOKEN`, `VastAi__ApiKey`,
   `VastAi__InstanceId`, `VastAi__ResolverEndpointPelaVast=false`.
8. **Testar o ciclo:** peça uma minuta com a instância parada; ela deve subir, gerar e voltar a *stopped* após a
   ociosidade. Confira `GET /pecas/admin/metricas` e a tabela `gpu_instance_logs`.

Instância parada **continua cobrando disco** ([documentação da Vast](https://docs.vast.ai/guides/instances/storage)), e
saldo zerado a interrompe. Destruí-la apaga os modelos: é ação manual e o código não a executa. Ao religar, a GPU
pode estar ocupada por outra pessoa; o circuito abre e a fila espera.

### Gerar o GGUF (uma vez)

O Ollama atual não quantiza para q4_K_M ao importar ([documentação](https://docs.ollama.com/import)); a conversão é do
llama.cpp, fora da aplicação. Precisa de ~35 GB de disco e serve qualquer máquina com Python, inclusive um notebook do
Google Colab (sem GPU). Ainda **não executado** neste projeto.

```bash
pip install -q huggingface_hub && hf auth login      # conta que aceitou os termos do modelo
hf download Jurema-br/Jurema-7B --local-dir jurema
git clone --depth 1 https://github.com/ggml-org/llama.cpp
pip install -q -r llama.cpp/requirements/requirements-convert_hf_to_gguf.txt
python llama.cpp/convert_hf_to_gguf.py jurema --outtype f16 --outfile jurema-f16.gguf
cmake -S llama.cpp -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build --target llama-quantize -j
./build/bin/llama-quantize jurema-f16.gguf jurema-7b-q4_K_M.gguf Q4_K_M
sha256sum jurema-7b-q4_K_M.gguf                      # anote e confira depois de copiar
```

## Checklists

### Segurança
- [ ] Nenhuma porta publicada na Vast; `/api/tags` sem token devolve 401 e `/api/pull` devolve 404
- [ ] TLS e autenticação em Ollama e Qdrant; `AllowInsecureHttp` desligado
- [ ] Segredos só em cofre ou variáveis; chave da Vast com escopo mínimo; rotação definida
- [ ] Migrações 016, 017 e 019 aplicadas; aplicação conecta como `app_user` (sem BYPASSRLS)
- [ ] Coleção `documentos_caso` inacessível fora da rede do Worker e da API
- [ ] Worker em contêiner sem root, com limite de CPU e memória (processa arquivo não confiável)
- [ ] Antivírus no upload: **não implementado**; decidir (ex.: ClamAV) antes de abrir a usuários externos
- [ ] Eventos `AcessoNegadoAoCaso` e de injeção de prompt acompanhados
- [ ] Teste de restauração de backup (PostgreSQL, storage, Qdrant e chaves de cifra)

### Privacidade
- [ ] DPIA atualizado; base legal registrada; avaliação de sigilo (OAB) para processar em GPU de terceiro
- [ ] Contrato e política de retenção da Vast.ai e do storage revistos; região do bucket definida
- [ ] Transcrição só em servidor próprio
- [ ] Disco do Qdrant cifrado (guarda trechos em texto)
- [ ] Logs conferidos: sem fatos, trechos, nomes ou prompts
- [ ] Prazo de retenção e rotina de exclusão por caso definidos; cliente informado do uso de IA

### Custo
- [ ] `Drafting:Custo:GpuUsdPorHora` igual ao preço real contratado
- [ ] Parada por ociosidade verificada na prática; alerta de saldo na Vast
- [ ] Custo mensal do disco parado conhecido
- [ ] Primeira ingestão medida com GPU real no subconjunto antes de ampliar o escopo
- [ ] Um único Worker controla a instância

### Qualidade jurídica
- [ ] Avaliação cega de 20 ou mais minutas reais por área, por profissional habilitado
- [ ] Taxa de `PossivelAlucinacao` e de citação corrigida por tipo de peça acompanhada
- [ ] Amostra de citações `Validada` conferida contra o texto oficial (a validação confere o acervo, não o Diário Oficial)
- [ ] Vigência: "Não informada" tratada como pendência humana
- [ ] OCR: amostra de documentos manuscritos, carimbados e de baixa resolução conferida
- [ ] Atualização do acervo com responsável e periodicidade
- [ ] Peças longas testadas com o `NumCtx` de produção (minuta não pode sair truncada)

## Medições e limites

Medido com o repositório real (commit `c9f21b4`), Qdrant v1.15.5 e PostgreSQL 16 locais, **embeddings simulados**
(o tempo de GPU não entra):

| Medida | Valor |
|---|---|
| Subconjunto de alta utilização | 5.936 arquivos → 23.050 trechos, 0 falhas, 72 s |
| Qdrant após a carga | 245 MB de RAM, 251 MB de disco |
| PostgreSQL após a carga | 97 MB |
| Busca híbrida | ~20 ms |
| Escopo completo | 203.265 arquivos elegíveis (não ingerido) |

**Verificado com a ferramenta real:** Tesseract em português, Poppler e LibreOffice da imagem do Worker; regras do nginx e
idempotência do `onstart.sh` (com Ollama e cloudflared simulados).

**Não verificado neste ambiente** (sem GPU e sem contas externas): geração real com Jurema-7B, embeddings BGE-M3,
Vast.ai e túnel, servidor Whisper, R2/B2. Testes usam servidores simulados que seguem os contratos documentados.

**Fora do escopo:** reranker com modelo, `.msg`, SignalR, alimentação de `pecas_modelo`, motor de vigência por data,
antivírus, exportação PDF.

## Testes

```bash
dotnet test tests/UnitTests/Drafting.Application.UnitTests
dotnet test tests/UnitTests/Drafting.Infrastructure.UnitTests
dotnet test tests/UnitTests/Drafting.Api.UnitTests
dotnet test tests/IntegrationTests/Drafting.IntegrationTests   # sobe PostgreSQL 16 e Qdrant via Docker
```
