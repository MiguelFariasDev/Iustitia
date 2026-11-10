# Operação — acesso pelo Cloudflare Tunnel

Como publicar o Core, o frontend e o webhook do Bot atrás de um Cloudflare Tunnel (`cloudflared`),
sem abrir portas no servidor. Vale para hospedagem própria e homologação. Em produção no Azure
Container Apps o ingress é o do Azure ([production-config](compliance/production-config.md));
os princípios abaixo (publicar o mínimo, confiar só no proxy certo) continuam valendo, mas os
endereços de proxy confiável mudam.

## 1. O que precisa ser público

| Peça | Quem acessa | Público? |
|---|---|---|
| Bot — `GET/POST /webhooks/whatsapp` | A Meta | **Sim.** É o motivo do tunnel: a Meta só entrega webhook em HTTPS público. |
| Bot — `POST /api/v1/bot/send` | Só o Core | **Não.** Entrega mensagem ao cliente final; fica interno. |
| Core — `/api/v1/bot/*` | Só o Bot | **Não**, se Core e Bot estão na mesma rede. |
| Core — API (`/api/v1/*`) e frontend | Advogados | Sim, se o acesso for de fora do escritório. |
| Core — `/metrics`, `/jobs`, `/health`, `/health/ready` | Operação | **Não.** Bloqueados no ingress (defesa em profundidade; já exigem autenticação). |
| Supabase (Auth/Realtime) | Navegador | Sim. Se for o Supabase em nuvem, já é público. |

O Core fala com o Bot por `Atendimento:Bot:BaseUrl` e o Bot com o Core por `Core:BaseUrl`, ambos por
endereço **interno** (`http://localhost:…` ou nome do serviço no Docker). Esse tráfego não passa
pelo Cloudflare.

## 2. Pré-requisitos

- Um domínio com DNS no Cloudflare (abaixo: `escritorio.com.br`).
- `cloudflared` instalado no servidor, que precisa alcançar Core, Bot e frontend.
- Portas dos serviços. Os exemplos usam **8080** (Core, como em `ASPNETCORE_URLS` do
  [production-config](compliance/production-config.md)), **8081** (Bot) e **3000** (frontend): ajuste.
  Os serviços devem escutar apenas em `127.0.0.1`/rede interna, nunca em `0.0.0.0` aberto à internet.

## 3. Criar o tunnel

```bash
cloudflared tunnel login
cloudflared tunnel create iustitia            # gera ~/.cloudflared/<UUID>.json
cloudflared tunnel route dns iustitia bot.escritorio.com.br
cloudflared tunnel route dns iustitia api.escritorio.com.br
cloudflared tunnel route dns iustitia app.escritorio.com.br
```

O `<UUID>.json` é uma **credencial**: permissão `600`, fora do repositório, guardada em cofre
(`CLAUDE.md` do Bot, regra 5). Quem a tem publica serviços no seu domínio.

## 4. `config.yml`

`/etc/cloudflared/config.yml`:

```yaml
tunnel: <UUID>
credentials-file: /etc/cloudflared/<UUID>.json

# As regras valem em ordem: vale a PRIMEIRA que casar. A última, sem hostname, é obrigatória.
ingress:
  # --- Bot: SOMENTE o webhook da Meta ---------------------------------------
  - hostname: bot.escritorio.com.br
    path: ^/webhooks/whatsapp$
    service: http://localhost:8081

  # --- Core: tira da internet o que é só de operação -------------------------
  - hostname: api.escritorio.com.br
    path: ^/(metrics|jobs)(/.*)?$|^/health(/ready)?$
    service: http_status:404

  # --- Core: API ----------------------------------------------------------------
  - hostname: api.escritorio.com.br
    service: http://localhost:8080

  # --- Frontend ------------------------------------------------------------------
  - hostname: app.escritorio.com.br
    service: http://localhost:3000

  # --- Todo o resto: 404 --------------------------------------------------------
  - service: http_status:404
```

Como isso protege:

- `bot.escritorio.com.br/api/v1/bot/send` (ou qualquer outra rota do Bot) cai no 404 final e **nunca chega
  ao Bot**.
- O `path` é uma expressão regular e não inclui a query string, então `GET /webhooks/whatsapp?hub.challenge=…`
  (verificação da Meta) casa.
- `/health/live` continua acessível (só diz que o processo está de pé), útil para monitor de
  disponibilidade externo. O detalhado (`/health`, `/health/ready`) revela estado de dependências e fica fechado.

Validar e subir:

```bash
cloudflared tunnel ingress validate
cloudflared tunnel ingress rule https://bot.escritorio.com.br/api/v1/bot/send   # deve apontar para http_status:404
sudo cloudflared service install      # roda como serviço, reinicia sozinho
```

> Alternativa: tunnel gerenciado pelo painel (Zero Trust → Networks → Tunnels), que usa um token em vez de
> `config.yml`. As mesmas regras de hostname/path se cadastram lá; guarde o token como segredo.

## 5. Configurar o Core

O Core só enxerga o IP real do cliente se for configurado para confiar no proxy. Sem isso,
`RemoteIpAddress` é sempre o IP do `cloudflared`: o rate limit por IP junta **todos os usuários numa
partição** (um usuário que erre a senha 10 vezes trava o login dos demais), o log mascara sempre o
mesmo IP e o `UseHttpsRedirection` enxerga HTTP.

A seção `ForwardedHeaders` (`appsettings.json`, desligada por padrão):

| Chave | Padrão | Significado |
|---|---|---|
| `Enabled` | `false` | Liga o middleware (é o primeiro do pipeline). |
| `KnownProxies` | `[]` | IPs de proxy confiáveis — o IP que a **API vê na conexão**, não o do cliente. |
| `KnownNetworks` | `[]` | Redes confiáveis em CIDR (ex.: a sub-rede do Docker). |
| `ForwardLimit` | `1` | Proxies confiáveis em cadeia (1 = só o `cloudflared`). |

Com `Enabled=true` e as duas listas vazias, só o **loopback** é confiável. Escolha pelo desenho:

| Onde roda o `cloudflared` | Configuração |
|---|---|
| Na mesma máquina, falando com `localhost` | `ForwardedHeaders__Enabled=true` (basta) |
| Em container na rede do compose | `ForwardedHeaders__Enabled=true` e `ForwardedHeaders__KnownNetworks__0=<sub-rede>` — descubra com `docker network inspect <rede> --format '{{(index .IPAM.Config 0).Subnet}}'` |
| Atrás de outro proxy (nginx etc.) | Some os dois à lista e use `ForwardLimit=2` |

O que o código **recusa na subida** (a API não sobe com a configuração insegura): `0.0.0.0`/`::` em
`KnownProxies`, redes `/0` em `KnownNetworks`, IP ou CIDR ilegível, `ForwardLimit` fora de 1–5.

> **Não use `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.** Esse atalho do ASP.NET Core confia em
> qualquer origem e ignora esta seção: qualquer cliente que alcance a API passa a poder forjar o próprio
> IP e escapar do rate limit.

`ForwardLimit=1` usa a **última** entrada do `X-Forwarded-For`, que é a que a borda do Cloudflare
acrescenta com o IP que ela viu; o que o cliente mandou antes é ignorado.

O **Bot** não precisa de ajuste: ele não usa IP nem esquema do pedido (a autenticação do webhook é a
assinatura `X-Hub-Signature-256`). Se isso mudar, ele precisa do mesmo tratamento.

### Demais ajustes

| Onde | O que |
|---|---|
| Core | `Cors__AllowedOrigins__0=https://app.escritorio.com.br`. Vazio (padrão) bloqueia o frontend. |
| Frontend | `NEXT_PUBLIC_API_URL=https://api.escritorio.com.br`. As `NEXT_PUBLIC_*` são gravadas **no build**: rebuilde a imagem. |
| Frontend | `NEXT_PUBLIC_SUPABASE_URL`/`NEXT_PUBLIC_SUPABASE_ANON_KEY` apontando para o Supabase acessível pelo navegador. |
| Meta | Webhook: `https://bot.escritorio.com.br/webhooks/whatsapp` + o verify token configurado no Bot. |
| Core (opcional) | `AllowedHosts=api.escritorio.com.br;localhost` em vez de `*`. Inclua todo nome pelo qual o Bot ou monitores chamam o Core, senão recebem 400. |

## 6. Proteção na borda (Cloudflare)

O tunnel resolve conectividade; **não autentica ninguém**.

- **Cloudflare Access** (Zero Trust) em `app.` exigindo login (e-mail ou SSO) antes de a requisição
  chegar ao sistema. Para um sistema com sigilo profissional, é a medida de maior retorno.
  Em `api.`, o Access só funciona se o cliente conseguir passar pelo login do Access: um app mobile ou
  integração precisa de *service token*; se não der, deixe `api.` sem Access (a API já exige JWT).
- **Nunca ponha Access no webhook** (`bot.`): a Meta não faz login. Quem o protege é a assinatura
  `X-Hub-Signature-256`, validada pelo Bot sobre o corpo bruto. Não habilite no hostname do Bot nenhuma
  regra que reescreva o corpo da requisição (Transform Rules, WAF com modificação): a assinatura deixaria de bater.
- **WAF e rate limiting do Cloudflare** na borda complementam o rate limit do Core.
- Uma regra de WAF permitindo só os IPs da Meta no webhook é uma camada extra aceitável, mas a lista muda e
  não substitui a assinatura.

## 7. Checklist

Marque cada item **de fora da rede do servidor** (4G, outra máquina), exceto onde indicado.

**Antes de ligar**

- [ ] `<UUID>.json` com permissão `600`, fora do git; token do tunnel (se usar o painel) em cofre.
- [ ] Core, Bot e frontend escutam só em `127.0.0.1`/rede interna; nenhuma porta de serviço aberta no firewall.
- [ ] `ASPNETCORE_FORWARDEDHEADERS_ENABLED` **não** está definida.
- [ ] `ForwardedHeaders__Enabled=true` (+ `KnownNetworks` se o `cloudflared` estiver em container).
- [ ] `Cors__AllowedOrigins` contém `https://app.escritorio.com.br`; frontend rebuildado com a `NEXT_PUBLIC_API_URL` pública.
- [ ] `cloudflared tunnel ingress validate` sem erro.

**Roteamento**

- [ ] `curl -i "https://bot.escritorio.com.br/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=<token>&hub.challenge=123"` → `200` com corpo `123`.
- [ ] `curl -i -X POST https://bot.escritorio.com.br/webhooks/whatsapp -d '{}'` (sem assinatura) → `401`.
- [ ] `curl -i -X POST https://bot.escritorio.com.br/api/v1/bot/send` → `404`.
- [ ] `curl -i https://api.escritorio.com.br/metrics`, `/jobs`, `/health`, `/health/ready` → `404`.
- [ ] `curl -i https://api.escritorio.com.br/health/live` → `200`.
- [ ] `curl -I https://api.escritorio.com.br/health/live` **não** devolve redirecionamento (nada de `307` em laço).
- [ ] Frontend abre, faz login e as chamadas à API não dão erro de CORS no console.
- [ ] Realtime: a inbox atualiza ao chegar mensagem, sem recarregar (WebSocket pelo tunnel).

**IP real e rate limit** (confirma que o `ForwardedHeaders` está de fato em uso)

- [ ] No log de requisição do Core, o IP mascarado de duas pessoas em redes diferentes **difere** e não é o do `cloudflared`/`127.0.0.1`.
- [ ] Errar o login 11 vezes de uma rede devolve `429` para ela e **não** bloqueia o login de outra rede.
- [ ] Do lado de dentro da rede, `curl -H 'X-Forwarded-For: 1.2.3.4' http://<ip-do-servidor>:8080/…` direto à API **não** muda o IP registrado (peer não confiável).

**Fluxo ponta a ponta com a Meta**

- [ ] Meta aceita a URL do webhook (verificação) e o campo `messages` está assinado.
- [ ] Mensagem real no WhatsApp → o Bot responde o menu (aviso de atendimento automatizado) e o atendimento aparece na inbox.
- [ ] Handoff ("2") → status `PendingHuman`; advogado assume, responde e a mensagem chega ao cliente (Core → Bot → Meta, sem passar pelo Cloudflare).

**Operação**

- [ ] `cloudflared` instalado como serviço com reinício automático (`systemctl status cloudflared`).
- [ ] Alerta se o tunnel cair (Cloudflare Notifications ou monitor externo em `/health/live`).
- [ ] Cloudflare Access ligado em `app.` com a política de quem pode entrar; webhook sem Access.
- [ ] Documentado quem tem acesso à conta do Cloudflare e como rotacionar a credencial do tunnel
  (criar tunnel novo, trocar o serviço, apagar o antigo).

## 8. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| `502 Bad Gateway` | O serviço de origem não está de pé ou a porta/esquema do `service:` está errada (`http://` e não `https://`). |
| `404` em tudo | Nenhuma regra casou: hostname digitado diferente do DNS, ou a regra de `path` do webhook não casa. |
| O IP registrado é sempre o do `cloudflared` | `ForwardedHeaders:Enabled` é falso, ou o IP do `cloudflared` não está em `KnownProxies`/`KnownNetworks` (em Docker, use a sub-rede). |
| Laço de redirecionamento | `ForwardedHeaders` desligado: a API enxerga HTTP e redireciona para HTTPS. |
| API não sobe, mensagem cita `ForwardedHeaders` | Configuração rejeitada (ver seção 5): corrija o valor apontado. |
| Frontend com erro de CORS | `Cors:AllowedOrigins` vazio ou diferente do endereço público do frontend. |
| Meta recusa a verificação | `verify token` diferente do configurado no Bot, ou a rota do webhook bloqueada no ingress. |
| Webhook chega mas o Bot responde `401` | O corpo foi alterado no caminho (regra do Cloudflare que reescreve) ou o App Secret do Bot não é o do app da Meta. |

## 9. Limites do Cloudflare a conhecer

- Tempo máximo de resposta de cerca de 100 s por requisição (planos comuns); o webhook responde em
  milissegundos, mas endpoints lentos do Core devem ser assíncronos.
- Conexões WebSocket ociosas podem ser derrubadas; o `supabase-js` reconecta sozinho.
- Teste rápido sem domínio: `cloudflared tunnel --url http://localhost:8081` cria uma URL
  `*.trycloudflare.com` temporária. Serve para validar o webhook em desenvolvimento; a URL muda a cada execução
  e **não** deve ser usada em produção.

## 10. O que foi verificado

- O comportamento do `ForwardedHeaders` (IP/esquema, peer não confiável, cadeia, configuração insegura
  recusada) tem testes automatizados (`ForwardedHeadersTests`) com um proxy simulado.
- **Não foi testado contra um Cloudflare real**: as regras de `path` do ingress e o formato do
  `X-Forwarded-For` que a borda envia são o que o checklist (seções "Roteamento" e "IP real") confirma no
  primeiro deploy.
