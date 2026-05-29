using BankingSystem.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace BankingSystem.Api.Common;

public static class HttpResults
{
    public static IResult Problem(Error error) =>
        error.Code.Contains("NotFound")
            ? Results.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = error.Message,
                Status = StatusCodes.Status404NotFound
            })
            : error.Code.Contains("Validation")
                ? Results.BadRequest(new ProblemDetails
                {
                    Title = "Validation Error",
                    Detail = error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : Results.UnprocessableEntity(new ProblemDetails
                {
                    Title = "Business Rule Violation",
                    Detail = error.Message,
                    Status = StatusCodes.Status422UnprocessableEntity
                });
}
