using System.Text;

public class DataConvert
{
    public static byte[] HexStringToByteArray(string s)
    {


        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        s = s.Replace(" ", "");
        byte[] buffer = new byte[s.Length / 2];
        try
        {
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
        }
        catch (System.Exception ex)
        {
        }
        return buffer;
    }

    public static string ByteArrayToHexString(byte[] data)
    {
        if (data == null)
        {
            return string.Empty;
        }
        try
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0').PadRight(3, ' '));
            return sb.ToString().ToUpper();
        }
        catch (System.Exception ex)
        {
            return string.Empty;
        }

    }

    public static string ByteArrayToHexString(byte[] data, int leng)
    {
        try
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int k = 0; k < leng; k++)
            {
                sb.Append(Convert.ToString(data[k], 16).PadLeft(2, '0').PadRight(3, ' '));
            }
            return sb.ToString().ToUpper();
        }
        catch (System.Exception ex)
        {
            return string.Empty;
        }

    }
}