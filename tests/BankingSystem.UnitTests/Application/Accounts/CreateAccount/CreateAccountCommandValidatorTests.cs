using BankingSystem.Application.Accounts.CreateAccount;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace BankingSystem.UnitTests.Application.Accounts.CreateAccount;

public sealed class CreateAccountCommandValidatorTests
{
    private readonly CreateAccountCommandValidator _validator = new();

    [Fact]
    public void Given_ValidCommand_When_Validate_Then_NoErrors()
    {
        var command = new CreateAccountCommand("owner-123", "EUR", 100m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Given_EmptyOwnerName_When_Validate_Then_HasError(string? ownerName)
    {
        var command = new CreateAccountCommand(ownerName!, "EUR", 100m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.OwnerName);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData("USDT")]
    public void Given_UnsupportedCurrency_When_Validate_Then_HasError(string currency)
    {
        var command = new CreateAccountCommand("owner-1", currency, 100m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    public void Given_SupportedCurrency_When_Validate_Then_NoError(string currency)
    {
        var command = new CreateAccountCommand("owner-1", currency, 100m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Given_NegativeInitialBalance_When_Validate_Then_HasError()
    {
        var command = new CreateAccountCommand("owner-1", "EUR", -0.01m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InitialBalance);
    }

    [Fact]
    public void Given_ZeroInitialBalance_When_Validate_Then_NoError()
    {
        var command = new CreateAccountCommand("owner-1", "EUR", 0m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.InitialBalance);
    }
}
