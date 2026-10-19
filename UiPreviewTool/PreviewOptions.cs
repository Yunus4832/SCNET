using System.Globalization;

namespace Scnet.UiPreviewTool;

public sealed record PreviewOptions(string? XmlPath, string? AssemblyPath, string? FactoryType,
    string OutputDirectory, string[] Cultures, int Width, int Height, float Scale, bool Strict)
{
    public static PreviewOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var strict = false;
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (key == "--strict")
            {
                if (strict)
                {
                    throw new ArgumentException("Duplicate --strict.");
                }
                strict = true;
                continue;
            }
            if (key is not ("--xml" or "--assembly" or "--factory" or "--output" or "--culture" or "--size" or "--scale"))
            {
                throw new ArgumentException($"Unknown option: {key}");
            }
            if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(key, args[index]))
            {
                throw new ArgumentException($"Missing value or duplicate option: {key}");
            }
        }
        var xml = values.GetValueOrDefault("--xml");
        var assembly = values.GetValueOrDefault("--assembly");
        var factory = values.GetValueOrDefault("--factory");
        if (xml != null ? assembly != null || factory != null : assembly == null || factory == null)
        {
            throw new ArgumentException("Choose --xml FILE, or --assembly FILE --factory TYPE.");
        }
        if (!values.TryGetValue("--output", out var output) || string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("--output DIRECTORY is required.");
        }
        var size = values.GetValueOrDefault("--size", "960x640").Split('x');
        if (size.Length != 2 || !int.TryParse(size[0], out var width) || !int.TryParse(size[1], out var height) ||
            width is < 64 or > 8192 || height is < 64 or > 8192)
        {
            throw new ArgumentException("--size must be WIDTHxHEIGHT, each between 64 and 8192.");
        }
        if (!float.TryParse(values.GetValueOrDefault("--scale", "1"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var scale) || !float.IsFinite(scale) || scale is < 0.25f or > 4)
        {
            throw new ArgumentException("--scale must be between 0.25 and 4.");
        }
        var cultures = values.GetValueOrDefault("--culture", "zh-CN").Split(',');
        if (cultures.Any(culture => string.IsNullOrWhiteSpace(culture) || culture.Contains('/') || culture.Contains('\\')))
        {
            throw new ArgumentException("--culture requires comma-separated language identifiers.");
        }
        return new PreviewOptions(xml == null ? null : Path.GetFullPath(xml),
            assembly == null ? null : Path.GetFullPath(assembly), factory, Path.GetFullPath(output),
            cultures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), width, height, scale, strict);
    }
}
