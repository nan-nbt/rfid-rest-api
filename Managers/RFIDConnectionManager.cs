using System.Collections.Concurrent;

public class RFIDConnectionManager
{
    private readonly ConcurrentDictionary<string, RFIDConnection> _connections
        = new ConcurrentDictionary<string, RFIDConnection>();

    public bool AddConnection(RFIDConnection connection)
    {
        return _connections.TryAdd(connection.ConnectionId, connection);
    }

    public bool RemoveConnection(string connectionId)
    {
        return _connections.TryRemove(connectionId, out _);
    }

    public RFIDConnection? GetConnection(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connection);

        return connection;
    }

    public bool IsDeviceInUse(string deviceIp, uint devicePort)
    {
        return _connections.Values.Any(x =>
            x.DeviceIp == deviceIp &&
            x.DevicePort == devicePort);
    }

    public bool IsClientConnected(string clientIp)
    {
        return _connections.Values.Any(x =>
            x.ClientIp == clientIp);
    }
}