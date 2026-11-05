# Landing, cadastro, login e onboarding

Rotas: `/` (landing), `/cadastro`, `/login`, `/esqueci-senha`, `/redefinir-senha`, `/convite/[token]`,
`/onboarding` (exige sessão), `/termos`, `/privacidade`.

## Decisões
- **Tokens v4 por escopo (`.iu`)**, não no app inteiro: o protótipo v4 não está no repositório e o
  app autenticado usa outro design system. `src/styles/iustitia.css` remapeia as variáveis do shadcn
  só nessas superfícies. Valores marcados `(*)` no CSS são derivações a revisar quando o v4 chegar.
- **Cadastro** usa o contrato real `POST /tenants` (o backend cria a conta no Supabase). Exige CNPJ.
- **Enquadramento de portfólio** (`docs/compliance/portfolio.md`, ADR-064): sem planos/preços na
  landing; aviso no rodapé. Cadastro aberto a terceiros contradiz esse documento — decisão do produto.
- **Onboarding do escritório** persiste em `PUT /settings` (`onboarding.office`, só Sócio); o do
  convidado fica no navegador (não há endpoint por usuário).

## Marcadores pendentes
`{{PREENCHER}}`: texto de Termos e Privacidade; e-mail/contato no rodapé.
`{{CONFIRMAR}}`: tribunais cobertos; permissões do perfil Secretária; previsão do app iOS; modelo de
acesso (convite × aberto); CNPJ obrigatório para autônomo; mover dados do cadastro ao backend.

## Simulado / dependente de integração
Hero (dados fictícios); tour (3 paradas, sem botão da Athena — não existe); WhatsApp (indisponível);
logotipo (só no navegador, sem storage); monitoramento (salvo, mas a captura ainda não lê estas
configurações); 2FA (Supabase TOTP; **não há enforcement de `aal2` no servidor**).
