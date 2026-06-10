using System.Security.Cryptography;
using System.Text;

namespace Game.Utils;

public static class HashUtils
{
    public static string ComputeMd5(string input)
    {
        return ComputeMd5(Encoding.Default.GetBytes(input));
    }

    public static string ComputeMd5(byte[] input)
    {
        return Convert.ToHexString(MD5.HashData(input)).ToLowerInvariant();
    }
}
