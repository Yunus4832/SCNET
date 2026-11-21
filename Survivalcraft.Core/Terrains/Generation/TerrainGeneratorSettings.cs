using EntitySystem.TemplatesDatabase;

namespace Game.Terrains.Generation;

public sealed class TerrainGeneratorSettings
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public TerrainGeneratorDefinition Definition { get; }

    public TerrainGeneratorSettings(TerrainGeneratorDefinition definition)
    {
        Definition = definition;
        foreach (var parameter in definition.Parameters) { _values.Add(parameter.Key, parameter.DefaultValue); }
    }

    public T Get<T>(string key) => (T)_values[key];

    public void Set(string key, object value)
    {
        var parameter = Definition.Parameters.FirstOrDefault(p => p.Key == key) ??
                        throw new InvalidDataException($"Unknown parameter '{key}' for generator '{Definition.Id}'.");
        parameter.Validate(value);
        _values[key] = value;
    }

    public void Save(ValuesDictionary world)
    {
        var parameters = new ValuesDictionary();
        foreach (var parameter in Definition.Parameters) { parameters.SetValue(parameter.Key, _values[parameter.Key]); }

        world.SetValue("TerrainGeneratorId", Definition.Id);
        world.SetValue("TerrainGeneratorVersion", Definition.Version);
        world.SetValue("TerrainGeneratorParameters", parameters);
    }

    public static TerrainGeneratorSettings Load(ValuesDictionary world)
    {
        if (world.GetValue<object>("TerrainGeneratorId", false) is not string id)
        {
            throw new InvalidDataException("Missing or invalid TerrainGeneratorId.");
        }

        var definition = TerrainGeneratorDefinition.Find(id);
        if (world.GetValue<object>("TerrainGeneratorVersion", false) is not int version ||
            version != definition.Version)
        {
            throw new InvalidDataException(
                $"Unsupported or missing TerrainGeneratorVersion for '{id}'. Expected {definition.Version}."
            );
        }

        if (world.GetValue<object>("TerrainGeneratorParameters", false) is not ValuesDictionary values)
        {
            throw new InvalidDataException($"Missing or invalid TerrainGeneratorParameters for '{id}'.");
        }

        var settings = new TerrainGeneratorSettings(definition);
        foreach (var pair in values) { settings.Set(pair.Key, pair.Value); }

        foreach (var parameter in definition.Parameters)
        {
            if (!values.ContainsKey(parameter.Key))
            {
                throw new InvalidDataException($"Missing parameter '{parameter.Key}' for '{id}'.");
            }
        }

        return settings;
    }
}
