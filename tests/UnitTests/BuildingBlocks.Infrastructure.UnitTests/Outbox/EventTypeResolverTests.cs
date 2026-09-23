using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Outbox;

public sealed class EventTypeResolverTests
{
    private readonly EventTypeResolver _sut = new();

    [Fact]
    public void Resolve_WhenTypeIsKnown_ReturnsType()
    {
        var resolved = _sut.Resolve(typeof(TestDomainEvent).FullName!);

        resolved.Should().Be(typeof(TestDomainEvent));
    }

    [Fact]
    public void Resolve_WhenTypeIsUnknown_ThrowsInvalidOperationException()
    {
        var act = () => _sut.Resolve("Advocacia.Nao.Existe.EventoFantasma");

        act.Should().Throw<InvalidOperationException>();
    }
}
