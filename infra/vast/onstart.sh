#!/usr/bin/env bash
#
# On-start da instância de GPU na Vast.ai (imagem base: ollama/ollama, Ubuntu).
#
#   internet ──TLS──▶ Cloudflare Tunnel ──▶ nginx (127.0.0.1:8080, exige Bearer) ──▶ Ollama (127.0.0.1:11434)
#
# Nenhuma porta é publicada. O Ollama não tem autenticação própria, por isso o nginx exige o token e só
# deixa passar as três rotas que a aplicação usa (/api/chat, /api/embed, /api/tags): pull, delete e create
# de modelo ficam inacessíveis de fora.
#
# Idempotente: roda a cada partida da instância e só baixa/cria o que faltar no disco.
#
# Variáveis (defina na Vast como variáveis de ambiente da instância; nunca no script):
#   OLLAMA_PROXY_TOKEN   token longo e aleatório; o mesmo valor de Ollama__ApiKey no Worker
#   CF_TUNNEL_TOKEN      token do túnel nomeado do Cloudflare
#   MODELO_GGUF          opcional; GGUF do Jurema-7B, o mesmo caminho do FROM do Modelfile
#                        (padrão /root/modelos/jurema-7b-q4_K_M.gguf)
#   MODELFILE            opcional; padrão /root/Modelfile.jurema-7b
set -euo pipefail

: "${OLLAMA_PROXY_TOKEN:?defina OLLAMA_PROXY_TOKEN}"
: "${CF_TUNNEL_TOKEN:?defina CF_TUNNEL_TOKEN}"
MODELO_GGUF="${MODELO_GGUF:-/root/modelos/jurema-7b-q4_K_M.gguf}"
MODELFILE="${MODELFILE:-/root/Modelfile.jurema-7b}"
CLOUDFLARED_VERSAO="${CLOUDFLARED_VERSAO:-2025.8.1}"

# O token vai para a configuração do nginx: só aceita caracteres que não quebram a sintaxe.
[[ "$OLLAMA_PROXY_TOKEN" =~ ^[A-Za-z0-9_-]{32,}$ ]] || {
  echo "erro: OLLAMA_PROXY_TOKEN precisa de 32+ caracteres [A-Za-z0-9_-]" >&2
  exit 1
}

log() { echo "[onstart] $*"; }

if ! command -v nginx >/dev/null || ! command -v curl >/dev/null; then
  log "instalando nginx e curl"
  apt-get update -qq
  apt-get install -y -qq --no-install-recommends nginx-light curl ca-certificates
fi

if ! command -v cloudflared >/dev/null; then
  log "instalando cloudflared ${CLOUDFLARED_VERSAO}"
  curl -fsSL -o /usr/local/bin/cloudflared.novo \
    "https://github.com/cloudflare/cloudflared/releases/download/${CLOUDFLARED_VERSAO}/cloudflared-linux-amd64"
  chmod +x /usr/local/bin/cloudflared.novo
  mv /usr/local/bin/cloudflared.novo /usr/local/bin/cloudflared
fi

log "configurando o proxy"
umask 077
cat > /etc/nginx/nginx.conf <<NGINX
user www-data;
worker_processes 1;
events { worker_connections 64; }
http {
  # Nem o corpo nem o token aparecem no log; sem access log não há registro de fatos de processo.
  access_log off;
  error_log /var/log/nginx/error.log crit;
  server_tokens off;
  map_hash_bucket_size 256;       # "Bearer <token>" passa do tamanho padrão
  client_max_body_size 4m;

  map \$http_authorization \$autorizado {
    default 0;
    "Bearer ${OLLAMA_PROXY_TOKEN}" 1;
  }

  server {
    listen 127.0.0.1:8080;
    location ~ ^/api/(chat|embed|tags)\$ {
      if (\$autorizado = 0) { return 401; }
      proxy_pass http://127.0.0.1:11434;
      proxy_set_header Host localhost:11434;
      proxy_set_header Authorization "";
      proxy_http_version 1.1;
      proxy_buffering off;          # streaming NDJSON
      proxy_read_timeout 900s;
      proxy_send_timeout 900s;
    }
    location / { return 404; }
  }
}
NGINX
chmod 600 /etc/nginx/nginx.conf # contém o token; o master lê como root
nginx -t
if pgrep -x nginx >/dev/null; then
  nginx -s reload
else
  nginx </dev/null >/dev/null 2>&1 # sem redirecionar, o daemon segura a saída do on-start
fi

export OLLAMA_HOST=127.0.0.1:11434
export OLLAMA_KEEP_ALIVE="${OLLAMA_KEEP_ALIVE:-10m}"
export OLLAMA_NUM_PARALLEL=1
export OLLAMA_MAX_LOADED_MODELS=2

if ! pgrep -f "ollama serve" >/dev/null; then
  log "iniciando o Ollama"
  nohup ollama serve </dev/null >/var/log/ollama.log 2>&1 &
fi
for _ in $(seq 1 60); do
  curl -fsS "http://127.0.0.1:11434/api/tags" >/dev/null 2>&1 && break
  sleep 1
done
curl -fsS "http://127.0.0.1:11434/api/tags" >/dev/null

modelos="$(ollama list | awk 'NR > 1 {print $1}')"
if ! grep -q '^bge-m3' <<<"$modelos"; then
  log "baixando bge-m3"
  ollama pull bge-m3
fi
if ! grep -q '^jurema-7b' <<<"$modelos"; then
  [[ -f "$MODELO_GGUF" && -f "$MODELFILE" ]] || {
    echo "erro: jurema-7b ausente; coloque o GGUF em $MODELO_GGUF e o Modelfile em $MODELFILE" >&2
    exit 1
  }
  log "criando jurema-7b"
  ollama create jurema-7b -f "$MODELFILE"
fi

# O túnel sobe por último: só se publica o endereço quando há o que responder.
if ! pgrep -x cloudflared >/dev/null; then
  log "iniciando o túnel"
  TUNNEL_TOKEN="$CF_TUNNEL_TOKEN" nohup cloudflared tunnel --no-autoupdate run </dev/null >/var/log/cloudflared.log 2>&1 &
fi
log "pronto"
