using System.Globalization;

namespace Game.Terrains.Generation;

public sealed class TerrainGeneratorParameter
{
    public bool UsesBlockNames { get; private init; }

    public IReadOnlyDictionary<string, string> OptionNameKeys { get; private init; } = new Dictionary<string, string>();

    public string Key { get; }

    public string NameKey { get; }

    public string DescriptionKey { get; }

    public TerrainParameterKind Kind { get; }

    public object DefaultValue { get; }

    public string? UnitFormatKey { get; private init; }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public IReadOnlyList<string> Options { get; }

    public IReadOnlyList<double> SuggestedValues { get; }

    private TerrainGeneratorParameter(
        string key,
        string textKey,
        TerrainParameterKind kind,
        object defaultValue,
        double minimum = 0,
        double maximum = 0,
        double step = 0,
        string[]? options = null,
        double[]? suggestedValues = null
    )
    {
        Key = key;
        NameKey = textKey + "_Name";
        DescriptionKey = textKey + "_Description";
        Kind = kind;
        DefaultValue = defaultValue;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        Options = Array.AsReadOnly(options?.ToArray() ?? []);
        SuggestedValues = Array.AsReadOnly(suggestedValues?.ToArray() ?? []);
        if (kind == TerrainParameterKind.Number && (minimum > maximum || step <= 0))
        {
            throw new ArgumentException($"Invalid numeric declaration for '{Key}'.");
        }

        Validate(defaultValue);
        foreach (var suggestion in SuggestedValues) { Validate(suggestion); }
    }

    public static TerrainGeneratorParameter Boolean(string key, string textKey, bool value)
    {
        return new TerrainGeneratorParameter(key, textKey, TerrainParameterKind.Boolean, value);
    }

    public static TerrainGeneratorParameter String(
        string key,
        string textKey,
        string value,
        params (string Value, string NameKey)[] options
    )
    {
        return new TerrainGeneratorParameter(
            key,
            textKey,
            TerrainParameterKind.String,
            value,
            options: options.Select(option => option.Value).ToArray()
        )
        {
            OptionNameKeys = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                options.ToDictionary(option => option.Value, option => option.NameKey, StringComparer.Ordinal)
            )
        };
    }

    public static TerrainGeneratorParameter Block(string key, string textKey, string value, params string[] options)
    {
        return new TerrainGeneratorParameter(key, textKey, TerrainParameterKind.String, value, options: options)
        {
            UsesBlockNames = true
        };
    }

    public static TerrainGeneratorParameter Number(
        string key,
        string textKey,
        double value,
        double minimum,
        double maximum,
        double step,
        string? unitFormatKey = null,
        params double[] suggestedValues
    )
    {
        return new TerrainGeneratorParameter(
            key,
            textKey,
            TerrainParameterKind.Number,
            value,
            minimum,
            maximum,
            step,
            suggestedValues: suggestedValues
        ) { UnitFormatKey = unitFormatKey };
    }

    public object Parse(string text)
    {
        object value = Kind switch
        {
            TerrainParameterKind.Boolean when bool.TryParse(text, out var boolean) => boolean,
            TerrainParameterKind.Number when double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number
            ) => number,
            TerrainParameterKind.String => text,
            _ => throw new InvalidDataException($"Invalid {Kind} value '{text}' for generator parameter '{Key}'.")
        };
        Validate(value);
        return value;
    }

    public void Validate(object value)
    {
        var valid = Kind switch
        {
            TerrainParameterKind.Boolean => value is bool,
            TerrainParameterKind.String => value is string text && (Options.Count == 0 || Options.Contains(text)),
            TerrainParameterKind.Number => value is double number &&
                                           double.IsFinite(number) &&
                                           number >= Minimum &&
                                           number <= Maximum &&
                                           IsOnStep(number),
            _ => false
        };
        if (!valid) { throw new InvalidDataException($"Invalid value '{value}' for generator parameter '{Key}'."); }
    }

    private bool IsOnStep(double value)
    {
        var steps = (value - Minimum) / Step;
        return Math.Abs(steps - Math.Round(steps)) < 0.000001;
    }
}
