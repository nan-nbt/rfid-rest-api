using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace RFIDAPI.Helpers;

public static class HttpStatusHelper
{
    public static string GetStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status200OK => "OK",
        StatusCodes.Status201Created => "CREATED",
        StatusCodes.Status202Accepted => "ACCEPTED",
        StatusCodes.Status204NoContent => "NO CONTENT",
        StatusCodes.Status400BadRequest => "BAD REQUEST",
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT FOUND",
        StatusCodes.Status405MethodNotAllowed => "METHOD NOT ALLOWED",
        StatusCodes.Status408RequestTimeout => "REQUEST TIMEOUT",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status500InternalServerError => "INTERNAL SERVER ERROR",
        StatusCodes.Status501NotImplemented => "NOT IMPLEMENTED",
        StatusCodes.Status502BadGateway => "BAD GATEWAY",
        StatusCodes.Status503ServiceUnavailable => "SERVICE UNAVAILABLE",
        StatusCodes.Status504GatewayTimeout => "GATEWAY TIMEOUT",
        _ => ReasonPhrases.GetReasonPhrase(statusCode)?.ToUpperInvariant() ?? "UNKNOWN"
    };
}

