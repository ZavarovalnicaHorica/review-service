namespace Horica.ReviewService.Common;

/// <summary>
/// Base class for errors that are caused by the caller and should be returned as a ProblemDetails response.
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(404, "Not found", message)
    {
    }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(409, "Conflict", message)
    {
    }
}

public sealed class InvalidRequestException : ApiException
{
    public InvalidRequestException(string message)
        : base(400, "Invalid request", message)
    {
    }
}
