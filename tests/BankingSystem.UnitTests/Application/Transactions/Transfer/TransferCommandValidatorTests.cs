using BankingSystem.Application.Transactions.Transfer;
using FluentValidation.TestHelper;
using Xunit;

namespace BankingSystem.UnitTests.Application.Transactions.Transfer;

public sealed class TransferCommandValidatorTests
{
    private readonly TransferCommandValidator _validator = new();

    [Fact]
    public void Given_ValidCommand_When_Validate_Then_NoErrors()
    {
        var command = new TransferCommand(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "idem-key-1");

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Given_SameFromAndToAccount_When_Validate_Then_HasError()
    {
        var id = Guid.NewGuid();
        var command = new TransferCommand(id, id, 100m, "EUR", "key-1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Source and destination accounts must differ.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Given_NonPositiveAmount_When_Validate_Then_HasError(decimal amount)
    {
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), amount, "EUR", "key-1");

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Given_EmptyFromAccountId_When_Validate_Then_HasError()
    {
        var command = new TransferCommand(Guid.Empty, Guid.NewGuid(), 100m, "EUR", "key-1");

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FromAccountId);
    }

    [Fact]
    public void Given_EmptyIdempotencyKey_When_Validate_Then_HasError()
    {
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "");

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    [Fact]
    public void Given_IdempotencyKeyTooLong_When_Validate_Then_HasError()
    {
        var longKey = new string('x', 257);
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", longKey);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
