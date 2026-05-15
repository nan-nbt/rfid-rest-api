public class RFIDConnection
{
    public string ConnectionId { get; set; } = string.Empty;

    public string ClientIp { get; set; } = string.Empty;

    public string DeviceIp { get; set; } = string.Empty;

    public uint DevicePort { get; set; }

    public DateTime ConnectedAt { get; set; }

    public DateTime LastActivity { get; set; }

    public UHFService Service { get; set; } = null!;
}