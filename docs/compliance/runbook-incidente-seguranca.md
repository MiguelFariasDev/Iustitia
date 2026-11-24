# Runbook — incidente de segurança com dados pessoais

> Documento operacional. **Os prazos regulatórios abaixo devem ser confirmados com o jurídico/DPO antes de entrar em
> produção** — a regulamentação da ANPD muda.

## 1. O que é um incidente

Acesso não autorizado, perda, alteração, divulgação ou destruição acidental ou ilícita de dados pessoais. Exemplos:
credencial de sócio vazada, token do Supabase/chave de serviço exposta, backup acessado indevidamente, falha de
isolamento entre escritórios (RLS), e-mail enviado ao destinatário errado, repositório público com segredo.

## 2. Papéis

| Papel | Responsabilidade |
|---|---|
| **Comandante do incidente** | Conduz a resposta e decide contenção; registra a linha do tempo. |
| **Encarregado (DPO)** | Avalia o risco ao titular, decide e conduz a comunicação à ANPD e aos titulares. |
| **Técnico de plantão** | Contém, preserva evidências e corrige. |
| **Jurídico** | Valida prazos, texto das comunicações e dever de sigilo profissional (OAB). |

## 3. Linha do tempo alvo

| Quando | Ação |
|---|---|
| **0–1 h** | Triagem: é incidente? Abrir registro. Acionar comandante e DPO. |
| **1–4 h** | **Conter**: revogar credenciais/chaves, desligar a integração afetada, bloquear usuários suspeitos, isolar o serviço. **Preservar evidências** (logs, snapshots) antes de limpar. |
| **4–24 h** | Delimitar: quais escritórios, quais titulares, quais categorias de dado, janela de exposição. |
| **Em até 3 dias úteis do conhecimento** | Se houver **risco ou dano relevante** aos titulares: comunicar a **ANPD** e os **titulares** (art. 48 da LGPD; regulamento de comunicação de incidente da ANPD — confirmar o prazo vigente). |
| **Sempre, sem demora** | Avisar os **escritórios afetados** (controladores): eles têm as próprias obrigações perante os titulares. |
| **Após a contenção** | Corrigir a causa raiz, revisar o runbook, registrar lições aprendidas. |

## 4. Contenção — comandos e pontos de controle

- **Chaves e segredos**: rotacionar no provedor e atualizar as variáveis de ambiente/Key Vault. Segredos citados no
  código: Supabase (`ServiceRoleKey`, JWT), `Email__ApiKey`, `Anthropic`, `VastAi__ApiKey`, `Encryption` (chaves de campo),
  chaves de bot de atendimento (`bot_api_keys` — revogar pela tela de integrações).
- **Sessões**: encerrar sessões de um usuário no painel do Supabase (Authentication → Users → Sign out).
- **Acesso por papel**: mudar o papel ou desativar o usuário em Configurações → Usuários.
- **Tenant isolado**: se o vazamento for entre escritórios, **desligar o endpoint** afetado (feature flag) até a correção.
- **Banco**: as políticas RLS estão em `infra/supabase/migrations`; qualquer suspeita de bypass vira incidente crítico.

## 5. O que informar (ANPD e titulares)

1. Natureza dos dados afetados e quantidade aproximada de titulares.
2. Medidas técnicas e de segurança adotadas (o dado pessoal sensível está cifrado em repouso — documento, e-mail e telefone
   de cliente usam AES-GCM por campo, ver ADR-068; isso reduz o risco e deve constar).
3. Riscos relacionados ao incidente.
4. Motivos da demora, se a comunicação não for imediata.
5. Medidas adotadas para reverter ou mitigar.
6. Contato do encarregado.

## 6. Registro

Manter, por pelo menos 5 anos, o registro do incidente: descrição, data de conhecimento, titulares e dados afetados,
decisão de comunicar (e justificativa se não comunicar), medidas e responsáveis. Modelo mínimo:

```
ID: INC-AAAA-NNN        Conhecido em: ____/__/__ __:__     Comandante: ____     DPO: ____
Resumo:
Escritórios afetados:                      Titulares (aprox.):          Categorias de dado:
Causa raiz:
Contenção (quando/como):
Comunicação ANPD: [ ] sim [ ] não — justificativa/data/protocolo:
Comunicação a titulares/escritórios: data/forma:
Ações corretivas e prazos:
```

## 7. Pendências para a produção

- Definir e publicar o **encarregado (DPO)** e o canal de contato.
- Escala de plantão e canal de alerta (a plataforma já expõe `/health` e métricas; falta o alertamento).
- Exercício de simulação (tabletop) antes do lançamento.
