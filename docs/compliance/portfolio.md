# O Iustitia é um projeto de portfólio

## O enquadramento

O Iustitia é um sistema de gestão para escritórios de advocacia construído como
**demonstração técnica**. Não é comercializado: não há venda, assinatura, licenciamento nem
cobrança por uso.

Esse enquadramento não é uma formalidade. Ele é o que torna legítimo o uso da API Pública do
DataJud (CNJ), cujo Termo de Uso proíbe uso comercial — ver `datajud-termo.md`.

## O que isso permite e o que não permite

**Permitido, dentro deste enquadramento:**

- Mostrar o sistema como trabalho técnico (entrevistas, repositório público, apresentações).
- Uma advogada usar o sistema no escritório dela, sem pagar nada, como teste de usabilidade
  do protótipo com dados reais.
- Consultar o DataJud para auto-preencher o cadastro de processos.

**Não permitido enquanto o DataJud estiver integrado:**

- Cobrar por qualquer funcionalidade, de qualquer forma.
- Oferecer o sistema como serviço a terceiros, mesmo gratuitamente em troca de outra coisa.
- Vender relatórios, exportações ou análises derivadas dos dados consultados.

## O uso real por uma advogada

O sistema está sendo usado por uma advogada em um escritório real. Isso é deliberado: o valor
de um projeto de portfólio está em ele funcionar sob condições reais, e prazo processual não
tem como ser simulado com honestidade.

Mas isso também é o ponto onde o enquadramento fica mais tenso. Por isso:

- **Não há contrapartida financeira de nenhum tipo.**
- Os dados dos clientes do escritório são tratados conforme a LGPD, com consentimento do
  escritório (ver `security.md`).
- A captura de publicações é **dirigida pelos processos que ela cadastra** — a base não
  contém processos de terceiros (ver `docs/modules/legal/cnj-capture.md`).

## Se um dia for comercializado

A migração é obrigatória, não opcional. Os passos:

1. Remover a integração com o DataJud, ou substituir o adaptador por um provedor comercial
   (a porta `IConsultaProcessoTribunal` existe para isso — ver ADR-064).
2. Revisar todo dado já obtido pelo DataJud que esteja persistido. Hoje isso é pouco: os
   metadados vão para o **formulário**, e o que se grava é o que a advogada confirmou. O
   cache é volátil (1 hora, Redis).
3. Atualizar `README.md`, o rodapé da aplicação e estes documentos.
4. Revisar os demais termos de uso: o DJEN/Comunica é outra API, com outro termo, e precisa
   da mesma análise antes de qualquer comercialização.

## O que já é independente do DataJud

Vale registrar o que **não** depende dessa fonte, porque é a maior parte do sistema:

- Captura de publicações (DJEN/Comunica — outra API, outro termo de uso)
- Cadastro de clientes e processos
- Multi-tenancy, RLS, autenticação, auditoria
- Todo o módulo Platform

O DataJud entra em um único ponto: o auto-preenchimento do cadastro de processos. Sem ele, o
cadastro continua funcionando — só dá mais trabalho.
