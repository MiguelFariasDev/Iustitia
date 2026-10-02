# ADR-062: Validação do Número CNJ com Dígitos Verificadores

**Status:** Aceito — implementado na Etapa 1.2, estendido à Fase 2

## Contexto

O número único de processo (Resolução CNJ nº 65/2008) tem o formato
`NNNNNNN-DD.AAAA.J.TR.OOOO` — 20 dígitos, sendo `DD` dois dígitos verificadores calculados
por mod 97 (ISO 7064).

Neste sistema ele não é um rótulo: é **a chave pela qual as publicações são capturadas**. O
job consulta o DJEN por esse número (ver `docs/modules/legal/cnj-capture.md`), e é por ele
que cada publicação é vinculada ao processo.

Um número com dígito verificador errado tem consequência direta e silenciosa: o processo é
cadastrado, a advogada acredita que está acompanhado, e **nenhuma publicação chega** — porque
o DJEN não conhece aquele número. Não há erro, não há alerta. O caso simplesmente não avisa,
até o prazo vencer.

## Decisão

Validar **formato e dígitos verificadores** em todas as fronteiras:

| Camada | Onde |
|---|---|
| Domínio | `Legal.Domain.Shared.CNJNumber` — usado por `Processo` e por `Publication` |
| Aplicação | `CreateProcessoValidator` |
| Interface | `lib/validation/documento.ts` (`isValidCnj`) + Zod, validando enquanto se digita |

Regras aplicadas:

- **20 dígitos** exatos, com ou sem máscara na entrada.
- **Segmento do Judiciário (posição 13) entre 1 e 9** — `0` não existe na Resolução.
- **Mod 97 base 10 (ISO 7064):** removidos os dígitos verificadores e concatenado `"00"` ao
  final, o resto da divisão por 97 deve ser `98 − DD`.

### Forma canônica: mascarada

O número é armazenado **mascarado** (`1234567-48.2026.8.26.0001`) em `processos.cnj_number` e
`publications.cnj_number`. É o que o usuário busca, o que aparece em exportação e o que
permite comparar processo e publicação sem normalizar dos dois lados.

Os **dígitos puros** existem em `processos.cnj_digits`, coluna derivada, por duas razões: é o
formato que o DJEN exige no parâmetro `numeroProcesso`, e é o que torna a busca por número
parcial indexável — o EF Core não traduz busca textual sobre value object convertido
(ver `QueryTranslationTests`).

### `BigInt` no cliente

O número reordenado tem 20 dígitos e ultrapassa `Number.MAX_SAFE_INTEGER`. Em TypeScript o
cálculo usa `BigInt`; em C#, `BigInteger`. Um `Number` comum daria resultado errado para
alguns números — e errado de forma intermitente, que é pior que errado sempre.

## Consequências

**Positivas:**

- Um número digitado errado é recusado **no formulário**, antes de o processo existir e antes
  de a advogada passar a confiar num acompanhamento que não acontece.
- Validado contra dados reais: **0 rejeições em 1.243 publicações** capturadas do DJEN na
  prova de conceito (Etapa 1.3) — a regra não rejeita dado legítimo.
- `CNJNumber` vive em `Legal.Domain.Shared` porque é o mesmo conceito para os dois agregados
  do módulo: a Publicação cita o número, o Processo é identificado por ele.

**Negativas / trade-offs:**

- **O número não é editável depois do cadastro** (`Processo.Update` não o aceita). É a
  identidade do processo e a chave das publicações já vinculadas; trocá-lo orfanaria essas
  publicações sem deixar rastro. Cadastrou errado, cadastra de novo — atrito deliberado.
- O algoritmo existe em C# e em TypeScript. Os dois têm teste com os mesmos casos, incluindo
  o dígito verificador trocado e o segmento `0`.
- Valida a **estrutura**, não a existência: um número bem formado pode não corresponder a
  processo nenhum. Isso aparece como ausência de publicações — motivo pelo qual a Fase 2
  precisa de uma métrica de cobertura (ver `docs/poc/cnj-poc-lessons.md`).
