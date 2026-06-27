using FluentValidation.Results;

namespace STOKIO.Api.Endpoints;

public static class ValidationResultExtensions
{
    public static IResult ToHttpResult(this ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}

