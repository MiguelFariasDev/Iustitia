# ADR-061: Validação de CPF/CNPJ com Dígitos Verificadores

**Status:** Aceito — implementado na Fase 2

## Contexto

O cliente do escritório é identificado por CPF (pessoa física) ou CNPJ (pessoa jurídica).
Esse documento não é só um campo de cadastro: é a **chave de unicidade** do cliente dentro
do escritório e o dado que aparece em procuração, petição e contrato.

Um documento errado só se revela tarde — na hora de protocolar uma peça ou emitir uma
cobrança —, e corrigi-lo depois de o cliente já ter processos vinculados é trabalhoso.

Havia três níveis possíveis de validação:

1. **Só tamanho** (11 ou 14 dígitos).
2. **Tamanho + dígitos verificadores** (mod 11).
3. **Consulta à Receita Federal** (existência real do documento).

## Decisão

Validar **tamanho e dígitos verificadores** (nível 2), em três camadas:

| Camada | Onde | Papel |
|---|---|---|
| Domínio | `Legal.Domain.Clientes.Documento` | Fronteira de verdade — nenhum `Cliente` existe com documento inválido |
| Aplicação | `CreateClienteValidator` | Falha barata antes de tocar o banco |
| Interface | `lib/validation/documento.ts` + Zod | Avisa enquanto se digita |

Três decisões dentro disso:

### Um único value object para CPF e CNPJ

`Documento` valida os dois, com o algoritmo escolhido pelo `TipoCliente`. É **um campo** do
cliente, e qual deles vale é decidido pelo tipo — dois value objects separados exigiriam
uma união no agregado e um `switch` em cada consumidor, sem ganhar nada.

`Cliente.Create` recusa documento cujo tipo divirja do tipo do cliente: sem isso, um CPF
seria aceito para uma empresa e o erro só apareceria ao emitir um documento oficial.

### Sequências de dígitos iguais são recusadas

`000.000.000-00` e `111.111.111-11` **passam** na aritmética do mod 11. São exatamente os
valores que alguém digita para o formulário deixar salvar. São recusados explicitamente.

### O algoritmo é duplicado em relação a `Platform.Domain.Tenancy.CNPJ`

Aquele é o CNPJ **do escritório** (código de erro `TENANT_CNPJ_INVALID`, regra do núcleo da
plataforma); este é o documento **do cliente do escritório**
(`CLIENT_DOCUMENT_INVALID`). Compartilhá-los acoplaria o módulo jurídico ao núcleo de
tenancy, que a ADR-055 separa deliberadamente.

São ~15 linhas de aritmética estável desde 1968. A duplicação é mais barata que a
dependência — e as duas versões têm testes próprios.

## Consequências

**Positivas:**

- Erro de digitação é pego no momento da digitação, não na hora de protocolar.
- A unicidade por `(tenant_id, documento)` fica confiável: sem validação, o mesmo cliente
  entraria duas vezes com dígitos trocados e o índice único não perceberia.
- A validação no cliente é conveniência; a do domínio é a garantia. Um `POST` direto na API
  encontra a mesma regra.

**Negativas / trade-offs:**

- **Não verifica existência real.** Um CPF matematicamente válido pode não pertencer a
  ninguém. Consultar a Receita exigiria integração paga, com latência no cadastro e mais um
  ponto de indisponibilidade — e o escritório tem o documento do cliente em mãos.
- O algoritmo existe em três lugares (dois em C#, um em TypeScript). Os três têm teste com
  os mesmos casos; divergência aparece como teste vermelho, não como bug em produção.

## Alternativas rejeitadas

- **Só validar tamanho:** aceitaria `12345678901`, que é o que se digita para pular o campo.
- **Consultar a Receita no cadastro:** custo e latência desproporcionais ao ganho, para um
  dado que o escritório já possui no contrato assinado.
