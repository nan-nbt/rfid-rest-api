namespace RFIDAPI.Constants;

public static class ConnectionStatus
{
    public const string CONNECTED = "Connected";
    public const string DISCONNECTED = "Disconnected";
    public const string CONNECTION_NOT_FOUND = "Connection was not found";
    public const string UNABLE_IDENTIFY_CLIENT = "Unable to identify client IP address";
    public const string CLIENT_ALREADY_CONNECT = "This client is already connected";
    public const string KEY_EMPTY = "RFID reader address (IP and port) is required";
    public const string IN_USE = "RFID reader device is already in use";
    public const string CAN_NOT_CONNECT = "Can not connect to RFID device host";
    public const string CAN_NOT_DISCONNECT = "Failed to disconnect from RFID device";
}
