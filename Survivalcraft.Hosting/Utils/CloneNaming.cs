using System.Globalization;
using System.Text.RegularExpressions;

namespace Game.Utils;

/// <summary>生成世界和数据实例共用的带时间戳副本名称。</summary>
internal static class CloneNaming
{
    public static string Create(string sourceName, DateTime localTime, int maxLength)
    {
        var baseName = Regex.Replace(sourceName, @"@[0-9]{8}\z", string.Empty);
        var suffix = localTime.ToString("'@'ddHHmmss", CultureInfo.InvariantCulture);
        var length = Math.Min(baseName.Length, maxLength - suffix.Length);
        if (length > 0 && char.IsHighSurrogate(baseName[length - 1])) { length--; }

        return baseName[..length] + suffix;
    }
}
