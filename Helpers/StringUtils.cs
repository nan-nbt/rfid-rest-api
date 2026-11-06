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

    public static string FormatEpcWithSpaces(string? epc)
    {
        if (string.IsNullOrWhiteSpace(epc))
        {
            return string.Empty;
        }

        var normalized = epc.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < normalized.Length; index += 2)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            if (index + 2 <= normalized.Length)
            {
                builder.Append(normalized.AsSpan(index, 2));
            }
            else
            {
                builder.Append(normalized.AsSpan(index));
            }
        }

        return builder.ToString();
    }
}
