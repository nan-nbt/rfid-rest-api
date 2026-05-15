using System.Text.RegularExpressions;

public class StringUtils
{
    /**
     * 判斷是否是十六進制
     *
     * @param str
     * @return
     */
    public static bool IsHexNumber(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }
        str = str.Trim();
        if (str.Length == 0 || (str.Length % 2 != 0))
        {
            return false;
        }
        if (Regex.IsMatch(str, "^[0-9A-Fa-f]+$"))
        {
            return true;
        }
        return false;
    }
}