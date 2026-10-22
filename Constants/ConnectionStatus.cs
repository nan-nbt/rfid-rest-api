namespace RFIDAPI.Constants;

public static class ConnectionStatus
{
    public const string CONNECTED = "Connected";
    public const string DISCONNECTED = "Disconnected";
    public const string CONNECTION_NOT_FOUND = "Connection not found";
    public const string UNABLE_IDENTIFY_CLIENT = "Unable to identify client IP";
    public const string CLIENT_ALREADY_CONNECT = "This client already connected";
    public const string KEY_EMPTY = "RFID key is empty (IP:PORT)";
    public const string IN_USE = "RFID device is alredy in used";
    public const string CAN_NOT_CONNECT = "Can not connect to RFID device host";
    public const string CAN_NOT_DISCONNECT = "Can not disconnect";
}
