using System.Text;

namespace RFIDAPI.Helpers;

public static class DataConvert
{
    public static byte[] HexStringToByteArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length % 2 != 0)
        {
            return [];
        }

        var buffer = new byte[normalized.Length / 2];
        for (var index = 0; index < normalized.Length; index += 2)
        {
            if (!byte.TryParse(normalized.AsSpan(index, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out buffer[index / 2]))
            {
                return [];
            }
        }

        return buffer;
    }

    public static string ByteArrayToHexString(byte[]? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(data.Length * 3);
        foreach (var value in data)
        {
            builder.Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
        }

        return builder.ToString();
    }

    public static string ByteArrayToHexString(byte[]? data, int length)
    {
        if (data is null || length < 0 || length > data.Length)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length * 3);
        for (var index = 0; index < length; index++)
        {
            builder.Append(data[index].ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
        }

        return builder.ToString();
    }
}
