using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

/// <summary>Base type for failures that map to a well defined HTTP status code.</summary>
internal abstract class AppException : Exception
{
    protected AppException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

/// <summary>The input is invalid (HTTP 400).</summary>
internal sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", 400)
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>The credentials are missing or invalid (HTTP 401).</summary>
internal sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message, 401)
    {
    }
}

/// <summary>The caller is authenticated but not allowed to perform the operation (HTTP 403).</summary>
internal sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message, 403)
    {
    }
}

/// <summary>The resource does not exist or is outside the caller's read scope (HTTP 404).</summary>
internal sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message, 404)
    {
    }
}

/// <summary>The operation is incompatible with the current state of the resource (HTTP 409).</summary>
internal sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, 409)
    {
    }
}
