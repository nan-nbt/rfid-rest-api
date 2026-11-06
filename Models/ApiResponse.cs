using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using RFIDAPI.Helpers;

namespace RFIDAPI.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; set; }
}

public class ApiError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public ApiError()
    {
    }

    public ApiError(string message)
    {
        Message = message;
    }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? timestamp = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = HttpStatusHelper.GetStatusMessage(StatusCodes.Status200OK),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToString("o"),
            Data = data
        };
    }

    public static ApiResponse<T> Success<T>(int statusCode, T data, string? timestamp = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            Message = HttpStatusHelper.GetStatusMessage(statusCode),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToString("o"),
            Data = data
        };
    }

    public static ApiResponse<object> Error(int statusCode, string errorMessage, string? timestamp = null)
    {
        return new ApiResponse<object>
        {
            Success = false,
            StatusCode = statusCode,
            Message = HttpStatusHelper.GetStatusMessage(statusCode),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToString("o"),
            Error = new ApiError(errorMessage)
        };
    }
}

