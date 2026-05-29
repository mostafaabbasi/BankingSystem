using BankingSystem.Domain.Common;
using FluentAssertions;
using Xunit;

namespace BankingSystem.UnitTests.Domain;

public sealed class ResultTests
{

    [Fact]
    public void Given_SuccessResult_When_AccessValue_Then_ReturnsValue()
    {
        var result = Result<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Given_FailureResult_When_AccessError_Then_ReturnsError()
    {
        var error = new Error("Test.Code", "Test message");
        var result = Result<string>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Test.Code");
        result.Error.Message.Should().Be("Test message");
    }

    [Fact]
    public void Given_FailureResult_When_AccessValue_Then_ThrowsInvalidOperationException()
    {
        var result = Result<string>.Failure(new Error("Fail", "oops"));

        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_ValueType_When_ImplicitConversion_Then_WrapsInSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Given_Error_When_ImplicitConversion_Then_WrapsInFailure()
    {
        var error = new Error("Conv.Error", "converted");
        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }


    [Fact]
    public void Error_NotFound_ReturnsExpectedCodeFormat()
    {
        var id = Guid.NewGuid();
        var error = Error.NotFound("Account", id);

        error.Code.Should().Be("Account.NotFound");
        error.Message.Should().Contain(id.ToString());
    }

    [Fact]
    public void Error_Validation_ReturnsExpectedCode()
    {
        var error = Error.Validation("Field.Required", "Field is required");

        error.Code.Should().Be("Field.Required");
    }

    [Fact]
    public void Error_None_IsEmpty()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }
}
