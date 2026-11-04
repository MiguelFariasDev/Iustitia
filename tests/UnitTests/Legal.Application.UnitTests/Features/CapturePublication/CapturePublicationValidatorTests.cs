using Advocacia.Modules.Legal.Application.Features.CapturePublication;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.CapturePublication;

public class CapturePublicationValidatorTests
{
    private readonly CapturePublicationValidator _validator = new();

    private static CapturePublicationCommand BuildCommand(
        Guid? tenantId = null,
        string externalId = "cnj-1",
        string cnjNumber = TestPublications.ValidCnjNumber,
        string content = "Intimação.") =>
        new(
            tenantId ?? Guid.NewGuid(),
            externalId,
            cnjNumber,
            PublicationSource.DJEN,
            content,
            TestPublications.Now.AddDays(-1));

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        _validator.Validate(BuildCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithoutTenantId_Fails()
    {
        _validator.Validate(BuildCommand(tenantId: Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithoutExternalId_Fails()
    {
        _validator.Validate(BuildCommand(externalId: "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithExternalIdAboveColumnLength_Fails()
    {
        _validator.Validate(BuildCommand(externalId: new string('x', 101))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithoutContent_Fails()
    {
        _validator.Validate(BuildCommand(content: "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ErrorMessages_NeverEchoTheContent()
    {
        // Mensagens de validação vão para o corpo da resposta e para os logs: o inteiro teor
        // não pode aparecer nelas (ver claude.md, seção 14).
        var command = BuildCommand(externalId: "", content: "CPF 123.456.789-00 da parte");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        string.Join(" ", result.Errors.Select(error => error.ErrorMessage))
            .Should().NotContain("123.456.789-00");
    }
}
