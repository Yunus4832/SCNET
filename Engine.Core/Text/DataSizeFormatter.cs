using Engine.Core.Mathematics;

namespace Engine.Core.Text;

public static class DataSizeFormatter
{
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes}B";
        }

        if (bytes < 1048576)
        {
            var kibibytes = bytes / 1024f;
            return string.Format(PrepareFormatString(kibibytes, "kB"), kibibytes);
        }

        if (bytes < 1073741824)
        {
            var mebibytes = bytes / 1024f / 1024f;
            return string.Format(PrepareFormatString(mebibytes, "MB"), mebibytes);
        }

        var gibibytes = bytes / 1024f / 1024f / 1024f;
        return string.Format(PrepareFormatString(gibibytes, "GB"), gibibytes);
    }

    private static string PrepareFormatString(float value, string unit)
    {
        var integerDigits = (int)(MathUtils.Log10(value) + 1f);
        return "{0:F" + MathUtils.Max(3 - integerDigits, 0) + "}" + unit;
    }
}
