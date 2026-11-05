using System.Text.RegularExpressions;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Mascara dados sensíveis (CPF, CNPJ, e-mail, telefone, tokens, senhas) antes de irem para
/// qualquer sink de log — ver ADR-037/068 e docs/compliance/lgpd.md. Função pura, sem
/// dependência do Serilog, para ser testável isoladamente (ver
/// <see cref="SensitiveDataMaskingEnricher"/> para o ponto de integração real).
///
/// Formatos:
///   - CPF:      <c>***.***.***-**</c> — mascarado POR INTEIRO. A Etapa 3.9 pediu
///     <c>***.456.789-**</c>, mas isso deixa só os 3 primeiros dígitos desconhecidos (os dois
///     últimos são dígitos verificadores, calculados a partir dos nove primeiros): 1.000
///     candidatos, reversível por força bruta em milissegundos. Manter o CPF todo oculto é o
///     que a minimização da LGPD exige; para correlacionar eventos use o Id do cliente.
///   - CNPJ:     <c>**.345.678/0001-**</c> (CNPJ de pessoa jurídica é dado público do cadastro
///     da Receita; o miolo e a filial ajudam a depurar sem expor pessoa natural).
///   - E-mail:   <c>m***@dominio.com</c>.
///   - Telefone: <c>(11) 9****-1234</c> (DDD, primeiro dígito e últimos quatro).
///   - Documento/telefone sem pontuação (11 ou 14 dígitos seguidos): tudo <c>*</c>.
///   - Token Bearer/JWT e pares <c>senha=…</c>/<c>"token":"…"</c>: valor substituído por <c>***</c>.
///
/// Senhas nunca deveriam chegar aqui: a regra é não logar o campo. Os padrões por nome são a
/// rede de segurança para quando alguém loga um objeto ou uma query string inteira.
/// </summary>
public static partial class LogMaskingPolicy
{
    public const string Redacted = "***";

    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // Ordem importa: formatos mais específicos antes dos genéricos. CNPJ antes de CPF (o
        // trecho "00.000.000" do CNPJ também bateria com o início do CPF); pares chave=valor
        // e tokens antes de e-mail/dígitos, para não mascarar duas vezes o mesmo trecho.
        var masked = CnpjRegex().Replace(value, "**.${meio1}.${meio2}/${filial}-**");
        masked = CpfRegex().Replace(masked, "***.***.***-**");
        masked = BearerTokenRegex().Replace(masked, "Bearer ***");
        masked = JwtRegex().Replace(masked, Redacted);
        masked = SecretPairRegex().Replace(masked, "${chave}${separador}***");
        masked = PhoneRegex().Replace(masked, MaskPhoneMatch);
        masked = EmailRegex().Replace(masked, MaskEmailMatch);
        masked = UnformattedDocumentRegex().Replace(masked, match => new string('*', match.Length));

        return masked;
    }

    /// <summary>
    /// Nomes de propriedade cujo VALOR é sempre suprimido em logs estruturados, qualquer que
    /// seja o conteúdo (comparação sem caixa, por "contém"). Cobre o caso em que o valor não tem
    /// padrão reconhecível por regex — uma senha, um token opaco, um número de OAB.
    /// </summary>
    public static bool IsSensitivePropertyName(string name)
    {
        foreach (var fragment in SensitiveNameFragments)
        {
            if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] SensitiveNameFragments =
    [
        "password", "passwd", "senha", "secret", "token", "apikey", "api_key", "authorization",
        "cookie", "cpf", "cnpj", "documento", "oab",
    ];

    private static string MaskEmailMatch(Match match)
    {
        var localPart = match.Groups["local"].Value;
        var domain = match.Groups["domain"].Value;

        return $"{localPart[0]}***@{domain}";
    }

    // (11) 98765-1234 -> (11) 9****-1234 ; (11) 3456-1234 -> (11) 3***-1234
    private static string MaskPhoneMatch(Match match)
    {
        var rest = match.Groups["resto"].Value;

        return $"{match.Groups["pais"].Value}{match.Groups["ddd"].Value}{rest[0]}{new string('*', rest.Length - 1)}-{match.Groups["final"].Value}";
    }

    [GeneratedRegex(@"\b\d{2}\.(?<meio1>\d{3})\.(?<meio2>\d{3})/(?<filial>\d{4})-\d{2}\b")]
    private static partial Regex CnpjRegex();

    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")]
    private static partial Regex CpfRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9\-_.~+/]+=*")]
    private static partial Regex BearerTokenRegex();

    // header.payload.signature (Base64url) — um JWT solto, sem o prefixo "Bearer".
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtRegex();

    // password=abc, "senha":"abc", token: abc, apikey=abc — mantém a chave, suprime o valor.
    [GeneratedRegex(
        "(?i)(?<chave>\"?(?:password|passwd|senha|secret|client_secret|access_token|refresh_token|id_token|api[_-]?key|apikey)\"?)(?<separador>\\s*[:=]\\s*\"?)[^\\s\"',;&}]+")]
    private static partial Regex SecretPairRegex();

    // "(11) 98765-1234", "+55 (11) 98765-1234", "11 98765-1234" -> mantém DDD, 1º dígito e final.
    [GeneratedRegex(@"(?<prefixo>(?<pais>\+55\s?)?(?<ddd>(?:\(\d{2}\)\s?)|(?:\b\d{2}\s)))(?<resto>\d{4,5})-(?<final>\d{4})\b")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?<local>[A-Za-z0-9._%+-]+)@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})\b")]
    private static partial Regex EmailRegex();

    // CPF/CNPJ/celular sem pontuação: exatamente 11 ou 14 dígitos isolados.
    [GeneratedRegex(@"(?<![\d.\-/])(?:\d{14}|\d{11})(?![\d.\-/])")]
    private static partial Regex UnformattedDocumentRegex();
}
