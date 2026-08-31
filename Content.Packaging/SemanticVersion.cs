using System.Globalization;
using System.Numerics;

namespace Content.Packaging;

public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _prerelease;

    private SemanticVersion(BigInteger major, BigInteger minor, BigInteger patch, string[] prerelease, string value)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _prerelease = prerelease;
        Value = value;
    }

    public BigInteger Major { get; }
    public BigInteger Minor { get; }
    public BigInteger Patch { get; }
    public string Value { get; }

    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"'{value}' is not a supported SemVer 2.0 version.");
        }
        return version;
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = null!;
        if (string.IsNullOrEmpty(value) || value.Contains('+'))
        {
            return false;
        }
        var parts = value.Split('-', 2);
        var core = parts[0].Split('.');
        if (core.Length != 3 || !TryParseCore(core[0], out var major) ||
            !TryParseCore(core[1], out var minor) || !TryParseCore(core[2], out var patch))
        {
            return false;
        }
        var prerelease = parts.Length == 1 ? [] : parts[1].Split('.');
        if (prerelease.Any(identifier => identifier.Length == 0 ||
                identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') ||
                identifier.All(char.IsAsciiDigit) && identifier.Length > 1 && identifier[0] == '0'))
        {
            return false;
        }
        version = new SemanticVersion(major, minor, patch, prerelease, value);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }
        var core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (_prerelease.Length == 0 || other._prerelease.Length == 0)
        {
            return _prerelease.Length.CompareTo(other._prerelease.Length) * -1;
        }
        for (var index = 0; index < Math.Min(_prerelease.Length, other._prerelease.Length); index++)
        {
            var comparison = CompareIdentifier(_prerelease[index], other._prerelease[index]);
            if (comparison != 0) return comparison;
        }
        return _prerelease.Length.CompareTo(other._prerelease.Length);
    }

    public override string ToString() => Value;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    private static bool TryParseCore(string value, out BigInteger result)
    {
        result = 0;
        return value.Length > 0 && (value.Length == 1 || value[0] != '0') &&
               value.All(char.IsAsciiDigit) &&
               BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = left.All(char.IsAsciiDigit);
        var rightNumeric = right.All(char.IsAsciiDigit);
        if (leftNumeric && rightNumeric)
        {
            var lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0 ? lengthComparison : string.CompareOrdinal(left, right);
        }
        if (leftNumeric != rightNumeric) return leftNumeric ? -1 : 1;
        return string.CompareOrdinal(left, right);
    }
}
