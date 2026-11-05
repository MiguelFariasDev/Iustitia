using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Errors;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Errors;

public partial class ErrorCatalogTests
{
    [Fact]
    public void All_EveryErrorCode_HasADefinition()
    {
        foreach (var code in Enum.GetValues<ErrorCode>())
        {
            ErrorCatalog.TryGet(code, out var definition).Should().BeTrue($"{code} deveria ter uma ErrorDefinition");
            definition.Should().NotBeNull();
        }
    }

    [Fact]
    public void All_HasExactlyOneDefinitionPerErrorCode()
    {
        ErrorCatalog.All.Count.Should().Be(Enum.GetValues<ErrorCode>().Length);
    }

    [Fact]
    public void All_NoDuplicatedCodeStrings()
    {
        var codes = ErrorCatalog.All.Values.Select(d => d.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void Definition_CodeString_FollowsGroupDescricaoConvention(ErrorCode errorCode, ErrorDefinition definition)
    {
        _ = errorCode;
        GroupDescricaoRegex().IsMatch(definition.Code).Should().BeTrue(
            $"'{definition.Code}' deveria seguir o padrão GRUPO_DESCRICAO (maiúsculas e '_')");
    }

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void Definition_CodeString_MatchesEnumName(ErrorCode errorCode, ErrorDefinition definition)
    {
        definition.Code.Should().Be(errorCode.ToString());
    }

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void Definition_Message_IsNotEmpty(ErrorCode errorCode, ErrorDefinition definition)
    {
        _ = errorCode;
        definition.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void Definition_HttpStatus_IsAKnownValue(ErrorCode errorCode, ErrorDefinition definition)
    {
        // 429 é a única exceção: COMMON_RATE_LIMITED nunca passa por
        // ResultExtensions.ToHttpResult (rate limiting é rejeitado pelo middleware nativo do
        // ASP.NET Core antes de qualquer Result existir — ver ADR-039/RateLimitingConfiguration).
        if (errorCode == ErrorCode.COMMON_RATE_LIMITED)
        {
            definition.HttpStatus.Should().Be(429);
            return;
        }

        definition.HttpStatus.Should().BeOneOf(400, 401, 403, 404, 409, 500);
    }

    [Theory]
    [InlineData(ErrorCode.COMMON_UNEXPECTED, 0, 99)]
    [InlineData(ErrorCode.VALIDATION_REQUIRED_FIELD, 100, 199)]
    [InlineData(ErrorCode.AUTH_INVALID_CREDENTIALS, 200, 299)]
    [InlineData(ErrorCode.AUTHORIZATION_FORBIDDEN, 300, 399)]
    [InlineData(ErrorCode.TENANT_NOT_FOUND, 400, 499)]
    [InlineData(ErrorCode.USER_NOT_FOUND, 500, 599)]
    [InlineData(ErrorCode.AUDIT_LOG_NOT_FOUND, 600, 699)]
    [InlineData(ErrorCode.SETTINGS_KEY_NOT_FOUND, 700, 799)]
    [InlineData(ErrorCode.FEATURE_FLAG_NOT_FOUND, 800, 899)]
    [InlineData(ErrorCode.BACKUP_FAILED, 900, 999)]
    [InlineData(ErrorCode.INTEGRATION_UNAVAILABLE, 1000, 1099)]
    [InlineData(ErrorCode.CLIENT_NOT_FOUND, 1100, 1199)]
    [InlineData(ErrorCode.PROCESS_NOT_FOUND, 1200, 1299)]
    [InlineData(ErrorCode.PUBLICATION_NOT_FOUND, 1300, 1399)]
    [InlineData(ErrorCode.ALERT_NOT_FOUND, 1400, 1499)]
    [InlineData(ErrorCode.CNJ_UNAVAILABLE, 1700, 1799)]
    [InlineData(ErrorCode.ATENDIMENTO_NOT_FOUND, 2500, 2599)]
    [InlineData(ErrorCode.AI_INVALID_REQUEST, 1600, 1699)]
    [InlineData(ErrorCode.INTERNAL_UNEXPECTED, 9000, 9999)]
    public void ErrorCode_NumericValue_IsWithinItsGroupRange(ErrorCode code, int rangeStart, int rangeEnd)
    {
        ((int)code).Should().BeInRange(rangeStart, rangeEnd);
    }

    [Fact]
    public void AllErrorCodes_NumericValues_AreWithinASingleDeclaredRange()
    {
        var ranges = new (int Start, int End)[]
        {
            (0, 99), (100, 199), (200, 299), (300, 399), (400, 499), (500, 599),
            (600, 699), (700, 799), (800, 899), (900, 999), (1000, 1099),
            (1100, 1199), (1200, 1299), (1300, 1399), (1400, 1499), (1700, 1799), (2500, 2599), (9000, 9999),
            (1100, 1199), (1200, 1299), (1300, 1399), (1400, 1499), (1600, 1699), (1700, 1799), (9000, 9999),
        };

        foreach (var code in Enum.GetValues<ErrorCode>())
        {
            var value = (int)code;
            ranges.Should().Contain(r => value >= r.Start && value <= r.End, $"{code} ({value}) deveria estar em alguma faixa declarada");
        }
    }

    public static TheoryData<ErrorCode, ErrorDefinition> AllDefinitions()
    {
        var data = new TheoryData<ErrorCode, ErrorDefinition>();
        foreach (var (code, definition) in ErrorCatalog.All)
        {
            data.Add(code, definition);
        }

        return data;
    }

    [GeneratedRegex(@"^[A-Z][A-Z_]*[A-Z]$")]
    private static partial Regex GroupDescricaoRegex();
}
