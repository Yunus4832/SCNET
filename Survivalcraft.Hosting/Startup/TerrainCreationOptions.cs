using Game.Terrains.Generation;

namespace Game.Startup;

public sealed class TerrainCreationOptions
{
    public string? GeneratorId { get; set; }

    public Dictionary<string, string> Parameters { get; } = new(StringComparer.Ordinal);

    public bool IsSpecified => GeneratorId != null || Parameters.Count != 0;

    public void Apply(TerrainCreationOptions overrides)
    {
        if (overrides.GeneratorId != null)
        {
            if (GeneratorId != overrides.GeneratorId) { Parameters.Clear(); }

            GeneratorId = overrides.GeneratorId;
        }

        foreach (var (key, value) in overrides.Parameters) { Parameters[key] = value; }
    }

    public TerrainGeneratorSettings Resolve()
    {
        var definition = GeneratorId == null
            ? TerrainGeneratorDefinition.Standard
            : TerrainGeneratorDefinition.Find(GeneratorId);
        var result = new TerrainGeneratorSettings(definition);
        foreach (var (key, text) in Parameters)
        {
            var parameter = definition.Parameters.FirstOrDefault(item => item.Key == key) ??
                            throw new InvalidDataException(
                                $"Unknown parameter '{key}' for generator '{definition.Id}'."
                            );
            result.Set(key, parameter.Parse(text));
        }

        return result;
    }

    public TerrainCreationOptions Copy()
    {
        var result = new TerrainCreationOptions();
        result.Apply(this);
        return result;
    }
}
