namespace RFIDAPI.Helpers;

public static class StringUtils
{
    public static bool IsHexNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length % 2 != 0)
        {
            return false;
        }

        return normalized.All(Uri.IsHexDigit);
    }
}
