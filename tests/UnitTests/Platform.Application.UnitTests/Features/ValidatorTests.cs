using Advocacia.Platform.Application.Features.Auth.Login;
using Advocacia.Platform.Application.Features.Auth.Logout;
using Advocacia.Platform.Application.Features.Auth.RefreshToken;
using Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;
using Advocacia.Platform.Application.Features.Settings.UpdateSettings;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Application.Features.Tenants.UpdateTenant;
using Advocacia.Platform.Application.Features.Users.DeactivateUser;
using Advocacia.Platform.Application.Features.Users.UpdateUser;
using Advocacia.Platform.Application.Features.Users.UpdateUserRole;
using FluentAssertions;

namespace Advocacia.Platform.Application.UnitTests.Features;

/// <summary>
/// Cobertura leve dos validators de FluentValidation dos comandos de Auth/Tenants/Users —
/// os handlers já são testados isoladamente (ver *HandlerTests.cs); aqui só a regra de
/// validação declarativa em si.
/// </summary>
public class ValidatorTests
{
    [Theory]
    [InlineData("", "senha")]
    [InlineData("nao-e-email", "senha")]
    [InlineData("fulano@teste.com", "")]
    public void LoginValidator_WithInvalidInput_Fails(string email, string password)
    {
        var result = new LoginValidator().Validate(new LoginCommand(email, password));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_WithValidInput_Succeeds()
    {
        var result = new LoginValidator().Validate(new LoginCommand("fulano@teste.com", "Senha123!"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RefreshTokenValidator_WithEmptyToken_Fails()
    {
        var result = new RefreshTokenValidator().Validate(new RefreshTokenCommand(""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LogoutValidator_WithEmptyAccessToken_Fails()
    {
        var result = new LogoutValidator().Validate(new LogoutCommand(""));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "11444777000161", "Fulano", "fulano@teste.com", "Senha123!")]
    [InlineData("Escritorio", "", "Fulano", "fulano@teste.com", "Senha123!")]
    [InlineData("Escritorio", "11444777000161", "Fulano", "nao-e-email", "Senha123!")]
    [InlineData("Escritorio", "11444777000161", "Fulano", "fulano@teste.com", "curta")]
    public void CreateTenantValidator_WithInvalidInput_Fails(
        string name, string cnpj, string adminName, string adminEmail, string adminPassword)
    {
        var result = new CreateTenantValidator().Validate(new CreateTenantCommand(name, cnpj, adminName, adminEmail, adminPassword));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateTenantValidator_WithEmptyName_Fails()
    {
        var result = new UpdateTenantValidator().Validate(new UpdateTenantCommand(Guid.NewGuid(), "", null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateUserValidator_WithEmptyUserId_Fails()
    {
        var result = new UpdateUserValidator().Validate(new UpdateUserCommand(Guid.Empty, "Nome", null, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("PapelInexistente")]
    public void UpdateUserRoleValidator_WithInvalidRole_Fails(string role)
    {
        var result = new UpdateUserRoleValidator().Validate(new UpdateUserRoleCommand(Guid.NewGuid(), role));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeactivateUserValidator_WithEmptyUserId_Fails()
    {
        var result = new DeactivateUserValidator().Validate(new DeactivateUserCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "{}")]
    [InlineData("chave.valida", "")]
    [InlineData("chave.valida", "{invalido")]
    public void UpdateSettingsValidator_WithInvalidInput_Fails(string key, string valueJson)
    {
        var result = new UpdateSettingsValidator().Validate(new UpdateSettingsCommand(key, valueJson));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateSettingsValidator_WithValidJson_Succeeds()
    {
        var result = new UpdateSettingsValidator().Validate(new UpdateSettingsCommand("dashboard.layout", "{\"a\":1}"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateFeatureFlagValidator_WithEmptyKey_Fails()
    {
        var result = new UpdateFeatureFlagValidator().Validate(new UpdateFeatureFlagCommand("", true));

        result.IsValid.Should().BeFalse();
    }
}
