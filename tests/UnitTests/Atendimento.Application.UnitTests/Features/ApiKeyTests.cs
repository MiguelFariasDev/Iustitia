using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Application.UnitTests.Features;

public class ApiKeyTests
{
    [Fact]
    public void Generate_ProducesAHighEntropyPrefixedKey_AndOnlyItsHashIsStorable()
    {
        var (plain, prefix, hash) = BotApiKeyGenerator.Generate();

        plain.Should().StartWith(BotApiKeyGenerator.KeyPrefix);
        plain.Length.Should().BeGreaterThan(40);
        plain.Should().StartWith(prefix);
        prefix.Should().NotBe(plain);
        hash.Should().HaveLength(64).And.NotContain(plain);
        BotApiKeyGenerator.Hash(plain).Should().Be(hash, "a validação recalcula o mesmo hash");
    }

    [Fact]
    public void Generate_NeverRepeats()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => BotApiKeyGenerator.Generate().PlainText).ToHashSet();

        keys.Should().HaveCount(50);
    }

    [Fact]
    public async Task Issue_ReturnsTheKeyOnce_AndStoresOnlyTheHashForTheCallersTenant()
    {
        BotApiKey? stored = null;
        var repository = Substitute.For<IBotApiKeyRepository>();
        repository.When(r => r.Add(Arg.Any<BotApiKey>())).Do(call => stored = call.Arg<BotApiKey>());
        var handler = new IssueBotApiKeyHandler(repository, new FakeUnitOfWork(), Doubles.Lawyer(), Doubles.Clock());

        var result = await handler.Handle(new IssueBotApiKeyCommand("Bot principal"), default);

        result.IsSuccess.Should().BeTrue();
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(Doubles.TenantId);
        stored.KeyHash.Should().Be(BotApiKeyGenerator.Hash(result.Value.ApiKey));
        stored.KeyHash.Should().NotBe(result.Value.ApiKey);
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_DisablesTheKey()
    {
        var key = BotApiKey.Issue(Doubles.TenantId, "x", "adv_bot_abc", "hash", null, Doubles.Now);
        var repository = Substitute.For<IBotApiKeyRepository>();
        repository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);

        var result = await new RevokeBotApiKeyHandler(repository, new FakeUnitOfWork(), Doubles.Clock())
            .Handle(new RevokeBotApiKeyCommand(key.Id), default);

        result.IsSuccess.Should().BeTrue();
        key.IsActive.Should().BeFalse();
        key.RevokedAt.Should().Be(Doubles.Now);
    }

    [Fact]
    public async Task Revoke_FailsForAnUnknownKey()
    {
        var repository = Substitute.For<IBotApiKeyRepository>();

        var result = await new RevokeBotApiKeyHandler(repository, new FakeUnitOfWork(), Doubles.Clock())
            .Handle(new RevokeBotApiKeyCommand(Guid.NewGuid()), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.ATENDIMENTO_BOT_API_KEY_NOT_FOUND));
    }
}
