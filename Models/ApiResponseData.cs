using System.Text.Json.Serialization;

namespace RFIDAPI.Models;

public class DeviceConnectionData
{
    [JsonPropertyName("device_address")]
    public string DeviceAddress { get; set; } = string.Empty;

    [JsonPropertyName("connection_id")]
    public string? ConnectionId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class TagOperationData
{
    [JsonPropertyName("epc_code")]
    public string EpcCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class MessageData
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

