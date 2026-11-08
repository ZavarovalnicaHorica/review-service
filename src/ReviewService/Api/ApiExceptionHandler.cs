using System.Text.Json;
using Horica.ReviewService.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Horica.ReviewService.Api;

/// <summary>Turns expected errors (404, 409, 400) into RFC 7807 problem responses.</summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int status;
        string title;
        string detail;

        if (exception is ApiException apiException)
        {
            status = apiException.StatusCode;
            title = apiException.Title;
            detail = apiException.Message;
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            status = 409;
            title = "Conflict";
            detail = "The review was changed by someone else in the meantime. Reload it and try again.";
        }
        else
        {
            // Unexpected error: let the default handler produce a 500 response.
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            (JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken);

        return true;
    }
}
