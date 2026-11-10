# Módulo Drafting — geração assistida de peças (RAG + Ollama na Vast.ai, GPU sob demanda)

Decisão e riscos: [ADR-072](../adr/072-geracao-de-pecas-com-rag-local.md).

> **Estado da verificação:** o código foi escrito sem SDK .NET disponível no ambiente. Nada foi compilado nem executado,
> e a migração EF não foi gerada. Rode `dotnet build` e `dotnet test` antes de confiar nele. O recurso nasce
> **desligado** (`Drafting:Enabled=false`).

## Fluxo (assíncrono)

```
Frontend ─POST /api/v1/pecas/gerar (JWT)─▶ API: valida + sanitiza + grava Pendente + enfileira ─▶ 202 {id}
                                            (a API nunca fala com Ollama, Qdrant ou Vast.ai)
Worker (GeracaoPecaWorker) ─▶ FilaCoordinator
   NaFila ─▶ PreparandoGpu  GpuOrchestrator liga a instância (só se há trabalho) e espera Ollama + modelos
          ─▶ BuscandoContexto  embedding BGE-M3 → Qdrant (tenant + visibilidade + área) → dedupe → ≤ 8.000 tokens
          ─▶ Gerando  system prompt fixo + user prompt → /api/chat NDJSON (texto parcial gravado no banco)
          ─▶ Salvando  CitationVerifier, fontes, tokens, custo estimado, tempo → Concluido + notificação
Fila vazia por IdleTimeoutMinutes (10) ─▶ instância PARADA (stop). Nunca destruída.
```

O stream do `GET /{id}/stream` lê o texto parcial do banco; por isso funciona com API e worker em processos distintos.

## Endpoints (`/api/v1`, JWT; advogado ou superior)

| Método | Rota | Resultado |
|---|---|---|
| POST | `/pecas/gerar` | 202 + `id`, `Location` (rate limit `ai-generation`, 6/min/usuário) |
| GET | `/pecas/{id}/status` | etapa, progresso, tentativas |
| GET | `/pecas/{id}` | minuta, fontes, avisos (sem modelo/infra) |
| GET | `/pecas/{id}/stream` | NDJSON; 409 se não estiver em processamento |
| POST | `/pecas/{id}/cancelar` | 200; 409 se já terminal |
| POST | `/pecas/{id}/regenerar` | nova versão (exige confirmação) |
| GET | `/pecas`, `/pecas/admin/metricas` | listagem; métricas (sócio ou superior) |
| POST | `/documentos/upload` | 202; multipart: `arquivo`, `tipoDocumento`, `area`, `tribunal`, `dataPublicacao`, `tags`, `visibilidade` |
| GET/DELETE | `/documentos`, `/documentos/{id}` | acervo do escritório (privados só do autor) |

Acesso a uma peça: dono ou Owner/Partner; os demais recebem 404. Status: Pendente, NaFila, PreparandoGpu,
BuscandoContexto, Gerando, Salvando, Concluido, Falhou, Cancelado.

### Exemplos

```bash
curl -X POST https://api.exemplo/api/v1/pecas/gerar -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"tipoPeca":"peticao_inicial","area":"civel","fatos":"...","pedidos":["..."]}'
# 202 {"id":"…","status":"Pendente"}

curl -N https://api.exemplo/api/v1/pecas/$ID/stream -H "Authorization: Bearer $JWT"
# {"tipo":"status","etapa":"Gerando"}
# {"tipo":"fragmento","texto":"DOS FATOS\n"}
# {"tipo":"fim","fontes":[…],"avisos":[…],"tempoGeracaoMs":41200}

curl -X POST https://api.exemplo/api/v1/documentos/upload -H "Authorization: Bearer $JWT" \
  -F arquivo=@acordao.pdf -F tipoDocumento=jurisprudencia -F area=civel -F tribunal=TJSP -F tags="dano moral"
```

## Configuração

Segredos **só** por variável de ambiente (nunca no appsettings versionado):

| Variável | Uso |
|---|---|
| `DRAFTING__ENABLED` | liga o módulo |
| `VASTAI__APIKEY`, `VASTAI__INSTANCEID` | controle da instância |
| `VASTAI__ENABLED` | `false` = Ollama sempre ligado (dev) |
| `OLLAMA__BASEURL`, `OLLAMA__APIKEY` | endpoint **com TLS** (ou túnel/proxy autenticado) |
| `QDRANT__BASEURL`, `QDRANT__APIKEY` | gRPC (6334) com TLS |
| `ConnectionStrings__…` | Postgres |

Seções: `Drafting`, `Drafting:Fila`, `Drafting:Custo`, `Drafting:Ingestao`, `Rag`, `VastAi` (Polling 15 s, StartTimeout 10 min,
IdleTimeout 10 min, MaxRetries 3), `Ollama` (qwen3:8b, bge-m3, Temperatura 0.2, TopP 0.9), `Qdrant`.

> **Correção sobre o pedido:** o exemplo sugeria `NumCtx: 8192`, mas o contexto recuperado pode chegar a ~8.000 tokens e ainda
> há system prompt, fatos e a resposta. Com 8192 o Ollama trunca silenciosamente o início do prompt (incluindo as regras).
> O padrão aqui é **16384**.

## Deploy na Vast.ai

1. Crie uma instância com GPU (≥ 12 GB para qwen3:8b quantizado) e imagem com Ollama. Anote o `INSTANCEID`.
2. Dentro dela: `ollama pull qwen3:8b && ollama pull bge-m3`. **Parar** a instância preserva o disco; destruir apaga os modelos.
3. Exponha o Ollama apenas por TLS com autenticação (túnel/proxy reverso com token). Nunca a porta 11434 aberta.
4. Gere uma chave da API da Vast com o menor escopo possível e injete em `VASTAI__APIKEY`.
5. Qdrant fica em host **sempre ligado** (barato, sem GPU), com API key e TLS. Coleção `juridico_chunks` (1024 dimensões, cosseno).
6. Rode o host `Worker` (único componente que liga/desliga a GPU). Dockerfile opcional: publicar `src/Hosts/Worker` com a imagem `mcr.microsoft.com/dotnet/aspnet`.
7. Migração: aplique `infra/supabase/migrations/016_drafting.sql` (ou `dotnet ef migrations add InitialDrafting` no projeto Infrastructure — **não gerada**).

Enquanto a GPU está parada, o armazenamento da instância ainda é cobrado pela Vast; confira a tabela de preços atual.

## Indexação

`POST /documentos/upload` grava metadados e enfileira. O worker extrai texto (PDF com camada de texto, DOCX, TXT), divide em
trechos de 800–1.200 caracteres com overlap, preservando artigo/cláusula, gera embeddings em lote (cache por hash) e grava no Qdrant com
`tenant_id`, visibilidade, tribunal, data e tags. PDF escaneado é recusado (sem OCR). A indexação usa a GPU, então também a liga sob demanda.

## Segurança e economia

Tenant em todo filtro do Qdrant; `generation_queue` e `gpu_instance_logs` guardam só ids/estados (sem RLS), as demais têm RLS;
`InputSanitizer` e `PromptInjectionGuard` (filtro, não garantia); `TransportGuard` recusa HTTP fora de loopback e qualquer chave sob HTTP;
circuit breaker de GPU; embeddings em cache; parada por ociosidade medida pela fila no banco.

## Checklist antes de produção

- [ ] `dotnet build`/`test` verdes; testes de integração com Postgres rodando
- [ ] TLS e autenticação em Ollama e Qdrant; nenhuma porta pública sem auth
- [ ] Segredos só no cofre/variáveis; chave Vast com escopo mínimo
- [ ] Parada ociosa verificada manualmente (instância `stopped` após 10 min)
- [ ] Aceite jurídico: dados de clientes saem para host de terceiro (Vast.ai) — avaliar LGPD e sigilo profissional (OAB) e contrato
- [ ] Revisão humana obrigatória: o aviso aparece em toda minuta; `CitationVerifier` sinaliza citações sem fonte
- [ ] Avaliação de qualidade com peças reais antes de liberar (modelo 8B pode errar; não é jurisprudência verificada)

## Fora do escopo desta entrega

Interface no frontend, exportação DOCX/PDF, OCR, cache semântico, geração por seções, SignalR/e-mail, painel visual de custos.
