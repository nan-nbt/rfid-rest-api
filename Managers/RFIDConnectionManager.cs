using System.Collections.Concurrent;
using RFIDAPI.Models;

namespace RFIDAPI.Managers;

public class RfidConnectionManager
{
    private readonly ConcurrentDictionary<string, RfidConnection> _connections
        = new ConcurrentDictionary<string, RfidConnection>();

    public bool AddConnection(RfidConnection connection)
    {
        return _connections.TryAdd(connection.ConnectionId, connection);
    }

    public bool RemoveConnection(string connectionId)
    {
        return _connections.TryRemove(connectionId, out _);
    }

    public RfidConnection? GetConnection(string connectionId)
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
