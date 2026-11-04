using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Publications;

public class PublishedAtTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InThePast_Succeeds()
    {
        var result = PublishedAt.Create(Now.AddDays(-2), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(Now.AddDays(-2));
    }

    [Fact]
    public void Create_NormalizesToUtc()
    {
        var saoPauloOffset = new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.FromHours(-3));

        var result = PublishedAt.Create(saoPauloOffset, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Offset.Should().Be(TimeSpan.Zero);
        result.Value.Value.Should().Be(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Create_InTheFuture_Fails()
    {
        var result = PublishedAt.Create(Now.AddDays(1), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_DATE));
    }

    [Fact]
    public void Create_WithinClockSkewTolerance_Succeeds()
    {
        // Tribunal com o relógio alguns minutos adiantado não deve ter a publicação rejeitada.
        var result = PublishedAt.Create(Now.AddMinutes(4), Now);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_BeyondClockSkewTolerance_Fails()
    {
        var result = PublishedAt.Create(Now + PublishedAt.ClockSkewTolerance.Add(TimeSpan.FromMinutes(1)), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_DATE));
    }

    [Fact]
    public void ToDateOnly_UsesTheUtcDate()
    {
        var lateNightUtc = new DateTimeOffset(2026, 9, 27, 23, 30, 0, TimeSpan.Zero);

        var publishedAt = PublishedAt.Create(lateNightUtc, Now).Value;

        publishedAt.ToDateOnly().Should().Be(new DateOnly(2026, 9, 27));
    }
}
