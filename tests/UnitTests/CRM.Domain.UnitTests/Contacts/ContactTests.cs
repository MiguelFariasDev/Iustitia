using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.CRM.Domain.Contacts;
using FluentAssertions;

namespace Advocacia.Modules.CRM.Domain.UnitTests.Contacts;

public class ContactTests
{
    private static Contact Create(string? document = null, string? email = null, string? phone = null) =>
        Contact.Create(Guid.NewGuid(), ContactType.Testemunha, "  João Pereira ", document, email, phone, null).Value;

    [Fact]
    public void Create_NormalizesNameAndFillsSearchColumn()
    {
        var contact = Create();

        contact.Name.Should().Be("João Pereira");
        contact.NameSearch.Should().Be("joão pereira");
        contact.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_KeepsOnlyDigitsOfDocumentAndPhone_AndLowercasesEmail()
    {
        var contact = Create("111.444.777-35", "Joao@Email.COM", "(85) 99999-1234");

        contact.Document.Should().Be("11144477735");
        contact.Phone.Should().Be("85999991234");
        contact.Email.Should().Be("joao@email.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Fails(string? name)
    {
        var result = Contact.Create(Guid.NewGuid(), ContactType.Fornecedor, name, null, null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CONTACT_NAME_REQUIRED));
    }

    [Fact]
    public void Create_WithEmptyTenant_Fails()
    {
        Contact.Create(Guid.Empty, ContactType.Fornecedor, "Fulano", null, null, null, null)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("123", ErrorCode.CONTACT_DOCUMENT_INVALID, "document")]
    [InlineData("nao-e-email", ErrorCode.CONTACT_EMAIL_INVALID, "email")]
    [InlineData("12345", ErrorCode.CONTACT_PHONE_INVALID, "phone")]
    public void Create_WithMalformedField_Fails(string value, ErrorCode expected, string field)
    {
        var result = Contact.Create(
            Guid.NewGuid(), ContactType.ParteContraria, "Fulano",
            field == "document" ? value : null,
            field == "email" ? value : null,
            field == "phone" ? value : null,
            null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expected.ToString());
    }

    [Fact]
    public void Update_WithInvalidData_KeepsPreviousTypeAndData()
    {
        var contact = Create();

        var result = contact.Update(ContactType.Fornecedor, "", null, null, null, null);

        result.IsFailure.Should().BeTrue();
        contact.Type.Should().Be(ContactType.Testemunha);
        contact.Name.Should().Be("João Pereira");
    }

    [Fact]
    public void DeactivateAndReactivate_AreNotIdempotent()
    {
        var contact = Create();

        contact.Reactivate().Error.Code.Should().Be(nameof(ErrorCode.CONTACT_ALREADY_ACTIVE));
        contact.Deactivate().IsSuccess.Should().BeTrue();
        contact.Deactivate().Error.Code.Should().Be(nameof(ErrorCode.CONTACT_ALREADY_INACTIVE));
        contact.Reactivate().IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("11144477735", "111.444.777-35")]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    public void MaskDocument_FormatsCpfAndCnpj(string digits, string expected) =>
        Contact.MaskDocument(digits).Should().Be(expected);
}
