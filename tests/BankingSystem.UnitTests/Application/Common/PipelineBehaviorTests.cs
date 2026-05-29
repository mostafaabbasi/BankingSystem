using BankingSystem.Application.Common.Behaviors;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Application.Transactions.Transfer;
using BankingSystem.Domain.Common;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankingSystem.UnitTests.Application.Common;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Given_NoValidators_When_Handle_Then_DelegatesDirectly()
    {
        var behavior = new ValidationBehavior<TransferCommand, Result<TransferResponse>>(
            validators: [],
            NullLogger<ValidationBehavior<TransferCommand, Result<TransferResponse>>>.Instance);

        var expected = Result<TransferResponse>.Success(
            new TransferResponse(Guid.NewGuid(), "Pending", "corr-1", DateTimeOffset.UtcNow));

        Func<CancellationToken, Task<Result<TransferResponse>>> next = _ => Task.FromResult(expected);

        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1");

        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Given_ValidCommand_When_Handle_Then_DelegatesAndReturnsResult()
    {
        var behavior = new ValidationBehavior<TransferCommand, Result<TransferResponse>>(
            validators: [new TransferCommandValidator()],
            NullLogger<ValidationBehavior<TransferCommand, Result<TransferResponse>>>.Instance);

        var expected = Result<TransferResponse>.Success(
            new TransferResponse(Guid.NewGuid(), "Pending", "corr-1", DateTimeOffset.UtcNow));

        Func<CancellationToken, Task<Result<TransferResponse>>> next = _ => Task.FromResult(expected);

        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1");

        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Given_InvalidCommand_When_Handle_Then_ReturnsValidationFailureWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<TransferCommand, Result<TransferResponse>>(
            validators: [new TransferCommandValidator()],
            NullLogger<ValidationBehavior<TransferCommand, Result<TransferResponse>>>.Instance);

        var nextCalled = false;
        Func<CancellationToken, Task<Result<TransferResponse>>> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result<TransferResponse>.Success(
                new TransferResponse(Guid.NewGuid(), "Pending", "corr", DateTimeOffset.UtcNow)));
        };

        var command = new TransferCommand(Guid.Empty, Guid.Empty, 0m, "EUR", "");

        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        result.Should().BeFailureWithCode("Validation.Failed");
        nextCalled.Should().BeFalse();
    }
}

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task Given_SuccessfulRequest_When_Handle_Then_DelegatesAndReturns()
    {
        var behavior = new LoggingBehavior<TransferCommand, Result<TransferResponse>>(
            NullLogger<LoggingBehavior<TransferCommand, Result<TransferResponse>>>.Instance);

        var expected = Result<TransferResponse>.Success(
            new TransferResponse(Guid.NewGuid(), "Pending", "corr-1", DateTimeOffset.UtcNow));

        Func<CancellationToken, Task<Result<TransferResponse>>> next = _ => Task.FromResult(expected);

        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1");

        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Given_ThrowingHandler_When_Handle_Then_ExceptionPropagates()
    {
        var behavior = new LoggingBehavior<TransferCommand, Result<TransferResponse>>(
            NullLogger<LoggingBehavior<TransferCommand, Result<TransferResponse>>>.Instance);

        Func<CancellationToken, Task<Result<TransferResponse>>> next =
            _ => throw new InvalidOperationException("Boom");

        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1");

        var act = () => behavior.HandleAsync(command, next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Boom");
    }
}
