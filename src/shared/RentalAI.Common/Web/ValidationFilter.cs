using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace RentalAI.Common.Web;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var input = context.Arguments.OfType<T>().FirstOrDefault();
        if (input is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
        }

        var result = await validator.ValidateAsync(input);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
