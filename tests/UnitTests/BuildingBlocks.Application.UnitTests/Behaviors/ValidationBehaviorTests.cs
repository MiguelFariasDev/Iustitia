using Advocacia.BuildingBlocks.Application.Behaviors;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private sealed record RichCommand(string Email, string Long, string Short, int Num, SampleEnum EnumValue) : ICommand<string>;

    private enum SampleEnum
    {
        A,
    }

    private sealed record SampleVoidCommand(string Name) : ICommand;

    private sealed class SampleVoidCommandValidator : AbstractValidator<SampleVoidCommand>
    {
        public SampleVoidCommandValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    [Fact]
    public async Task Handle_WithNoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<string>>([]);
        var nextCalled = false;

        var result = await behavior.Handle(new SampleCommand("qualquer"), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("ok"));
        }, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<string>>([new SampleCommandValidator()]);
        var nextCalled = false;

        var result = await behavior.Handle(new SampleCommand("nome valido"), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("ok"));
        }, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShortCircuitsWithResultOfTValueFailure()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<string>>([new SampleCommandValidator()]);
        var nextCalled = false;

        var result = await behavior.Handle(new SampleCommand(""), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("ok"));
        }, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_WithInvalidVoidCommand_ShortCircuitsWithResultFailure()
    {
        var behavior = new ValidationBehavior<SampleVoidCommand, Result>([new SampleVoidCommandValidator()]);
        var nextCalled = false;

        var result = await behavior.Handle(new SampleVoidCommand(""), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_NotEmptyFailure_MapsToValidationRequiredField()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Email).NotEmpty(), new RichCommand("", "a", "abcde", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public async Task Handle_EmailAddressFailure_MapsToValidationInvalidFormat()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Email).EmailAddress(), new RichCommand("nao-e-email", "a", "abcde", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_INVALID_FORMAT));
    }

    [Fact]
    public async Task Handle_MaximumLengthFailure_MapsToValidationMaxLengthExceeded()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Long).MaximumLength(2), new RichCommand("a@a.com", "abcdef", "abcde", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED));
    }

    [Fact]
    public async Task Handle_MinimumLengthFailure_MapsToValidationMinLengthNotMet()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Short).MinimumLength(10), new RichCommand("a@a.com", "a", "abc", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_MIN_LENGTH_NOT_MET));
    }

    [Fact]
    public async Task Handle_GreaterThanFailure_MapsToValidationOutOfRange()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Num).GreaterThan(100), new RichCommand("a@a.com", "a", "abcde", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_OUT_OF_RANGE));
    }

    [Fact]
    public async Task Handle_EnumFailure_MapsToValidationInvalidEnum()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.EnumValue).IsInEnum(), new RichCommand("a@a.com", "a", "abcde", 1, (SampleEnum)99));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_INVALID_ENUM));
    }

    [Fact]
    public async Task Handle_CustomPredicateFailure_MapsToValidationInvalidFormat()
    {
        var result = await ValidateRich(v => v.RuleFor(x => x.Email).Must(_ => false), new RichCommand("a@a.com", "a", "abcde", 1, SampleEnum.A));

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_INVALID_FORMAT));
    }

    private static async Task<Result<string>> ValidateRich(Action<InlineValidator<RichCommand>> configure, RichCommand command)
    {
        var validator = new InlineValidator<RichCommand>();
        configure(validator);

        var behavior = new ValidationBehavior<RichCommand, Result<string>>([validator]);

        return await behavior.Handle(command, () => Task.FromResult(Result.Success("ok")), CancellationToken.None);
    }
}
