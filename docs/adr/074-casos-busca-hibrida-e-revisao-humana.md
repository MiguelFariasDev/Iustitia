# ADR-074 — Casos, anexos isolados, BM25 no Qdrant e revisão humana como trava

Status: aceito. Substitui partes da [ADR-072](072-geracao-de-pecas-com-rag-local.md) e da
[ADR-073](073-acervo-normativo-em-markdown.md) (ver "O que muda").

## Contexto
A ADR-072 indexava documentos por **escritório**, e a ADR-073 usava o full-text do PostgreSQL como segundo
sinal de busca e não tinha exportação. Três problemas:

1. Anexo de um cliente podia ser recuperado na peça de outro: o filtro era só o tenant.
2. Busca por "art. 186 do Código Civil" dependia de vetor e de `ts_rank`, que não é BM25.
3. A trava de citações era um campo que o frontend deveria respeitar, sem nada no servidor.

## Decisão
1. **O caso é a unidade de isolamento.** `casoId` é obrigatório em `POST /pecas/gerar`. Acesso por participação
   (responsável, membros com Leitura/Escrita, gestão do escritório); anexo pode ser `restrito`. Quem não vê
   recebe 404. São quatro barreiras independentes: RLS por tenant, autorização do caso, filtro obrigatório no
   Qdrant (`FiltroCaso` não se constrói sem tenant, caso e usuário) e conferência de cada ponto recuperado
   contra o registro no PostgreSQL. Um teste de arquitetura impede usar a coleção de casos fora do store próprio.
2. **Anexos viram Markdown antes da indexação**, com conversores locais: PDF, DOCX, DOC, planilhas, imagens
   (OCR Tesseract), e-mail `.eml` e áudio (servidor Whisper próprio, opcional). Original, Markdown e trechos
   ficam cifrados (AES-GCM). Conversão roda sem GPU; só classificação e embeddings a usam.
3. **BM25 real no Qdrant:** vetor esparso `bm25` com `Modifier.Idf`, ao lado do vetor denso BGE-M3. Fusão RRF
   (ou DBSF) na aplicação, mais um terceiro sinal de referência exata (norma + dispositivo) no PostgreSQL. O
   full-text fica só como reserva quando o Qdrant falha.
4. **Escopo do acervo:** `constituicao.md`, `federal/` e `jurisprudencia/`, começando pelo subconjunto de alta
   utilização (`SomenteAltaUtilizacao`). Três coleções públicas; `documentos_caso` privada; `pecas_modelo`
   reservada.
5. **O orçamento de contexto deriva da janela do modelo** (`NumCtx` − `NumPredict` − prompt fixo). O Ollama corta
   o começo do prompt em silêncio quando estoura, e o começo é o system prompt. Entrada grande demais é
   recusada com erro claro.
6. **Revisão humana é estado do servidor.** Exportar exige revisão concluída; concluir exige decisão
   justificada em cada citação bloqueante, e citação rejeitada não pode permanecer no texto final. Mudar uma
   decisão reabre a revisão. Antes da revisão não há arquivo: só a minuta na tela, marcada como minuta.
7. **System prompt fixo**, travado por SHA-256 em teste. A pessoa usuária só preenche campos do user prompt, e
   texto vindo de fora tem os rótulos de seção neutralizados.

## O que muda em relação às ADRs anteriores
| Antes | Agora |
|---|---|
| `/documentos/*` (acervo do escritório) | `/casos/{id}/documentos`; as rotas antigas saíram |
| `indexed_documents` | Tabela mantida e sem uso; removê-la é decisão à parte (destrutiva) |
| Sete coleções públicas | Três |
| Full-text como "palavra-chave" | BM25 no Qdrant |
| `NumCtx` ≥ 16384 obrigatório | 8192 com orçamento calculado; 16384 recomendado com GPU de 24 GB |
| qwen3:8b | jurema-7b |
| Repositório clonado por Git | Pasta local (`Ingestao:Modo=Local`); Git continua disponível |

## Alternativas rejeitadas
- **Uma coleção por caso:** milhares de coleções degradam o Qdrant; o filtro com índice de payload, somado à
  conferência no banco, dá a garantia sem o custo.
- **Cross-encoder para reranking:** o Ollama não expõe esse endpoint e a stack exclui Python obrigatório. O
  reranker é por regras, com interface para troca.
- **Fusão nativa do Qdrant (`prefetch`):** não deixa combinar o sinal de referência exata nem as reservas por
  hierarquia (Constituição, leis, jurisprudência).
- **SignalR:** o streaming NDJSON sobre o texto parcial já atende e funciona com API e Worker separados.

## Defeitos encontrados e corrigidos no que já existia
- `VastAiService` descartava a resposta antes de lê-la: nenhuma chamada à Vast.ai funcionava.
- `FilaCoordinator` criava escopo síncrono para um recurso só assíncrono: todo item da fila falhava.
- `NormaRepository` abria a conexão sem passar pelo interceptor de tenant: sob RLS, as gravações seguintes
  afetavam zero linhas e a geração travava.
- O upload fechava o stream antes de ler o tamanho; o cancelamento no meio do stream do Ollama era ignorado.
- O binder do .NET **acrescenta** a listas com valor padrão: reduzir `PastasIncluidas` não tinha efeito.
- O projeto de testes do Drafting não compilava; 30 testes falhavam sem ninguém ver. O build Release quebrava.

## Consequências e riscos
- Com 8192 de contexto cabem em média cinco trechos. Peças longas pedem 16384 e mais VRAM.
- API e Worker precisam do mesmo object storage. O provedor `Local` só serve com volume compartilhado.
- Vigência é a declarada pela fonte; não há motor de vigência por data.
- Ferramentas externas (Tesseract, Poppler, LibreOffice) processam arquivo não confiável: rodam com tempo
  limite, sem shell e em diretório temporário próprio, dentro de contêiner sem root.
- A avaliação de qualidade jurídica com peças reais continua pendente e é pré-requisito de produção.
