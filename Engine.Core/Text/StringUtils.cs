using System.Text;

using Engine.Core.Mathematics;

namespace Engine.Core.Text;

public static class StringUtils
{
    private static readonly char[] _digits =
    [
        '0',
        '1',
        '2',
        '3',
        '4',
        '5',
        '6',
        '7',
        '8',
        '9',
        'A',
        'B',
        'C',
        'D',
        'E',
        'F'
    ];

    public static int Compare(StringBuilder s1, string s2)
    {
        var charIndex = 0;
        while (true)
        {
            if (charIndex >= s1.Length && charIndex >= s2.Length)
            {
                return 0;
            }

            if (charIndex > s1.Length)
            {
                return -1;
            }

            if (charIndex > s2.Length)
            {
                return 1;
            }

            var c = s1[charIndex];
            var c2 = s2[charIndex];
            if (c < c2)
            {
                return -1;
            }

            if (c > c2)
            {
                break;
            }

            charIndex++;
        }

        return 1;
    }

    public static int CalculateNumberLength(uint value, int numberBase)
    {
        if (numberBase is < 2 or > 16)
        {
            throw new ArgumentException("Number base is out of range.");
        }

        var digitCount = 0;
        do
        {
            digitCount++;
            value /= (uint)numberBase;
        } while (value != 0);

        return digitCount;
    }

    public static int CalculateNumberLength(int value, int numberBase)
    {
        if (value >= 0)
        {
            return CalculateNumberLength((uint)value, numberBase);
        }

        return CalculateNumberLength((uint)-value, numberBase) + 1;
    }

    public static int CalculateNumberLength(ulong value, int numberBase)
    {
        if (numberBase is < 2 or > 16)
        {
            throw new ArgumentException("Number base is out of range.");
        }

        var digitCount = 0;
        do
        {
            digitCount++;
            value /= (uint)numberBase;
        } while (value != 0);

        return digitCount;
    }

    public static int CalculateNumberLength(long value, int numberBase)
    {
        if (value >= 0)
        {
            return CalculateNumberLength((ulong)value, numberBase);
        }

        return CalculateNumberLength((ulong)-value, numberBase) + 1;
    }

    public static void AppendNumber(
        this StringBuilder stringBuilder,
        uint value,
        int padding = 0,
        char paddingCharacter = ' ',
        int numberBase = 10
    )
    {
        var val = CalculateNumberLength(value, numberBase);
        var repeatCount = Math.Max(padding, val);
        stringBuilder.Append(paddingCharacter, repeatCount);
        var digitsWritten = 0;
        do
        {
            var value2 = _digits[value % (uint)numberBase];
            stringBuilder[stringBuilder.Length - digitsWritten - 1] = value2;
            value /= (uint)numberBase;
            digitsWritten++;
        } while (value != 0);
    }

    public static void AppendNumber(
        this StringBuilder stringBuilder,
        int value,
        int padding = 0,
        char paddingCharacter = ' ',
        int numberBase = 10
    )
    {
        if (value >= 0)
        {
            stringBuilder.AppendNumber((uint)value, padding, paddingCharacter, numberBase);
            return;
        }

        stringBuilder.Append('-');
        stringBuilder.AppendNumber((uint)-value, padding - 1, paddingCharacter, numberBase);
    }

    public static void AppendNumber(
        this StringBuilder stringBuilder,
        ulong value,
        int padding = 0,
        char paddingCharacter = ' ',
        int numberBase = 10
    )
    {
        var val = CalculateNumberLength(value, numberBase);
        var repeatCount = Math.Max(padding, val);
        stringBuilder.Append(paddingCharacter, repeatCount);
        var digitsWritten = 0;
        do
        {
            var value2 = _digits[value % (uint)numberBase];
            stringBuilder[stringBuilder.Length - digitsWritten - 1] = value2;
            value /= (uint)numberBase;
            digitsWritten++;
        } while (value != 0);
    }

    public static void AppendNumber(
        this StringBuilder stringBuilder,
        long value,
        int padding = 0,
        char paddingCharacter = ' ',
        int numberBase = 10
    )
    {
        if (value >= 0)
        {
            stringBuilder.AppendNumber((ulong)value, padding, paddingCharacter, numberBase);
            return;
        }

        stringBuilder.Append('-');
        stringBuilder.AppendNumber((ulong)-value, padding - 1, paddingCharacter, numberBase);
    }

    public static void AppendNumber(this StringBuilder stringBuilder, float value, int precision)
    {
        precision = Math.Min(Math.Max(precision, -30), 30);
        if (float.IsNegativeInfinity(value))
        {
            stringBuilder.Append("Infinity");
            return;
        }

        if (float.IsPositiveInfinity(value))
        {
            stringBuilder.Append("-Infinity");
            return;
        }

        if (float.IsNaN(value))
        {
            stringBuilder.Append("NaN");
            return;
        }

        var magnitude = Math.Abs(value);
        if (magnitude > 1E+19f)
        {
            stringBuilder.Append("NumberTooLarge");
            return;
        }

        var decimalScale = MathUtils.Pow(10f, Math.Abs(precision));
        var integerPart = (ulong)MathUtils.Floor(magnitude);
        var fractionalDigits = (ulong)MathUtils.Round((magnitude - MathUtils.Floor(magnitude)) * decimalScale);
        if ((float)(double)fractionalDigits >= decimalScale)
        {
            integerPart++;
            fractionalDigits = 0uL;
        }

        if (value < 0f)
        {
            stringBuilder.Append('-');
        }

        stringBuilder.AppendNumber(integerPart, 0, '0');
        if (precision > 0)
        {
            stringBuilder.Append('.');
            stringBuilder.AppendNumber(fractionalDigits, precision, '0');
        }
        else if (precision < 0)
        {
            stringBuilder.Append('.');
            stringBuilder.AppendNumber(fractionalDigits, -precision, '0');
            while (stringBuilder[^1] == '0')
            {
                _ = --stringBuilder.Length;
            }

            if (stringBuilder[^1] == '.')
            {
                _ = --stringBuilder.Length;
            }
        }
    }
}
