# Política de Privacidade — RASCUNHO PARA REVISÃO JURÍDICA

> **Não publicar sem revisão de advogado especializado em proteção de dados.** Os campos entre colchetes dependem de
> decisões do negócio (razão social, CNPJ, encarregado, provedores finais). Este texto descreve o que o produto
> **de fato faz hoje**; ajuste-o se a realidade mudar.

**Última atualização:** [data]

## 1. Quem somos

O Iustitia é um sistema de gestão para escritórios de advocacia, operado por **[razão social]**, CNPJ **[CNPJ]**,
com sede em **[endereço]** ("Iustitia").

## 2. Duas relações diferentes

1. **Dados dos usuários do escritório** (advogados e equipe que usam o sistema): o Iustitia é **controlador**.
2. **Dados dos clientes, partes e contatos do escritório** inseridos no sistema: o **escritório é o controlador** e o
   Iustitia é **operador**, tratando os dados **apenas conforme as instruções do escritório**. Quem fala com o titular
   desses dados é o escritório.

## 3. Quais dados tratamos

| Categoria | Exemplos | Finalidade | Base legal (LGPD) |
|---|---|---|---|
| Cadastro do usuário | nome, e-mail, papel, telefone | criar e proteger a conta, autenticar | execução de contrato (art. 7º, V) |
| Dados de uso e segurança | endereço IP, navegador, registros de acesso e auditoria | segurança, prevenção a fraude, suporte | legítimo interesse (art. 7º, IX); obrigação legal (Marco Civil, art. 15) |
| Dados de clientes e processos do escritório | nome, CPF/CNPJ, contato, endereço, número de processo | prestar o serviço ao escritório | instrução do controlador (art. 39) |
| Publicações e movimentações | diário oficial e tribunais | monitorar prazos | instrução do controlador |
| Textos enviados à IA | trechos de peças e documentos | gerar minutas assistidas | instrução do controlador |

Não coletamos dados sensíveis por iniciativa própria; o escritório pode inseri-los em campos livres, sob sua
responsabilidade.

## 4. Como protegemos

- Isolamento entre escritórios no nível do banco de dados (RLS) e da aplicação.
- **Cifragem de campo** (AES-GCM) para documento, e-mail e telefone de clientes; índice cego para busca por documento.
- Autenticação com Supabase Auth; **verificação em duas etapas obrigatória** para titulares e sócios [se ativada].
- Registro de auditoria imutável, sem conteúdo pessoal.
- Em e-mails automáticos enviamos apenas o título do alerta e um link — nunca dados do caso.
- **Inteligência artificial**: o conteúdo enviado para gerar minutas **não é usado para treinar modelos** [confirmar com os
  provedores], e o uso de IA é registrado sem guardar o texto trocado.

## 5. Com quem compartilhamos (suboperadores)

[Preencher com os provedores efetivamente contratados — exemplos do código atual:] Supabase (autenticação e banco),
provedor de nuvem e hospedagem **[nome]**, provedor de e-mail transacional (Resend), provedor de IA **[Anthropic e/ou
infraestrutura própria de GPU]**, provedor de consulta de publicações **[definir]**. Transferências internacionais
ocorrerão com as garantias do art. 33 da LGPD **[detalhar]**.

## 6. Direitos do titular

Titulares podem pedir: confirmação do tratamento, acesso, correção, anonimização/eliminação, portabilidade,
informação sobre compartilhamento e revogação do consentimento (LGPD, art. 18).

- **Se você é cliente de um escritório** que usa o Iustitia: dirija o pedido ao escritório; ele dispõe das ferramentas
  de exportação e anonimização.
- **Se você é usuário do sistema**: escreva para **[e-mail do encarregado]**.

## 7. Retenção

Mantemos os dados enquanto a conta do escritório estiver ativa e pelo prazo necessário a obrigações legais. Após o
encerramento, os dados são **[eliminados/exportados em X dias]** e permanecem em backups por até **[X dias]**.

## 8. Incidentes

Em caso de incidente com risco ou dano relevante, comunicaremos a ANPD, os escritórios afetados e, quando cabível, os
titulares, nos prazos da lei.

## 9. Encarregado (DPO)

**[nome]** — **[e-mail]**

## 10. Alterações

Avisaremos sobre mudanças relevantes no sistema e por e-mail aos administradores de cada escritório.
