using BankingSystem.Domain.Common;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace BankingSystem.UnitTests.Common;

public static class ResultAssertionExtensions
{
    public static ResultAssertions<T> Should<T>(this Result<T> result) =>
        new(result, AssertionChain.GetOrCreate());

    public static ResultAssertions Should(this Result result) =>
        new(result, AssertionChain.GetOrCreate());
}

public sealed class ResultAssertions<T>(Result<T> subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<Result<T>, ResultAssertions<T>>(subject, assertionChain)
{
    protected override string Identifier => "Result";

    public AndWhichConstraint<ResultAssertions<T>, T> BeSuccess(string because = "", params object[] becauseArgs)
    {
        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .FailWith("Expected result to be successful{reason}, but it failed with error: {0} — {1}",
                Subject.Error.Code, Subject.Error.Message);

        return new AndWhichConstraint<ResultAssertions<T>, T>(this, Subject.Value);
    }

    public AndConstraint<ResultAssertions<T>> BeFailure(string because = "", params object[] becauseArgs)
    {
        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure)
            .FailWith("Expected result to be a failure{reason}, but it succeeded.");

        return new AndConstraint<ResultAssertions<T>>(this);
    }

    public AndConstraint<ResultAssertions<T>> BeFailureWithCode(string errorCode, string because = "", params object[] becauseArgs)
    {
        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure && Subject.Error.Code == errorCode)
            .FailWith("Expected failure with code {0}{reason}, but got: IsFailure={1}, Code={2}",
                errorCode, Subject.IsFailure, Subject.Error.Code);

        return new AndConstraint<ResultAssertions<T>>(this);
    }
}

public sealed class ResultAssertions(Result subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<Result, ResultAssertions>(subject, assertionChain)
{
    protected override string Identifier => "Result";

    public AndConstraint<ResultAssertions> BeSuccess(string because = "", params object[] becauseArgs)
    {
        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .FailWith("Expected result to be successful{reason}, but it failed with: {0} — {1}",
                Subject.Error.Code, Subject.Error.Message);

        return new AndConstraint<ResultAssertions>(this);
    }

    public AndConstraint<ResultAssertions> BeFailureWithCode(string errorCode, string because = "", params object[] becauseArgs)
    {
        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure && Subject.Error.Code == errorCode)
            .FailWith("Expected failure with code {0}{reason}, but got: IsFailure={1}, Code={2}",
                errorCode, Subject.IsFailure, Subject.Error.Code);

        return new AndConstraint<ResultAssertions>(this);
    }
}
