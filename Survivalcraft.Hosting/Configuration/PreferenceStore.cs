using System.Reflection;
using System.Xml.Linq;

using Engine.Core.Serialization;

using EntitySystem.XmlUtilities;

namespace Game.Configuration;

/// <summary>Persists the preference owners composed by the application using one atomic XML store.</summary>
public sealed class PreferenceStore
{
    private readonly string _path;

    private readonly Dictionary<string, (object Owner, PropertyInfo Property)>
        _properties = new(StringComparer.Ordinal);

    private readonly IDictionary<string, string> _configs;

    private XElement _unowned = new("Settings");

    public PreferenceStore(string path, IEnumerable<object> owners, IDictionary<string, string>? configs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(owners);
        _path = path;
        _configs = configs ?? new Dictionary<string, string>();
        foreach (var owner in owners)
        {
            ArgumentNullException.ThrowIfNull(owner);
            foreach (var property in owner.GetType().GetRuntimeProperties().Where(property =>
                         property.GetMethod is { IsStatic: false, IsPublic: true } &&
                         property.SetMethod is { IsPublic: true }
                     ))
            {
                if (!_properties.TryAdd(property.Name, (owner, property)))
                {
                    throw new InvalidOperationException($"Duplicate preference key '{property.Name}'.");
                }
            }
        }
    }

    public void Load()
    {
        if (!Storage.FileExists(_path))
        {
            Save();
            return;
        }

        using var stream = Storage.OpenFile(_path, OpenFileMode.Read);
        Read(XmlUtils.LoadXmlFromStream(stream, null, true));
        Log.Information("Loaded settings.");
    }

    public void Read(XElement root)
    {
        _unowned = new XElement(root);
        if (root.Element("Configs") is { } config)
        {
            foreach (var attribute in config.Attributes())
            {
                if (!_configs.ContainsKey(attribute.Name.LocalName))
                {
                    _configs[attribute.Name.LocalName] = attribute.Value;
                }
            }
        }

        foreach (var item in root.Elements("Setting"))
        {
            var name = "<unknown>";
            try
            {
                name = XmlUtils.GetAttributeValue<string>(item, "Name");
                if (_properties.TryGetValue(name, out var target))
                {
                    var value = HumanReadableConverter.ConvertFromString(
                        target.Property.PropertyType,
                        XmlUtils.GetAttributeValue<string>(item, "Value")
                    );
                    target.Property.SetValue(target.Owner, value, null);
                }
            }
            catch (Exception exception)
            {
                Log.Warning($"Setting \"{name}\" could not be loaded. Reason: {exception.Message}");
            }
        }
    }

    public XElement Write()
    {
        var root = new XElement(_unowned);
        root.Elements("Setting").Where(element =>
            _properties.ContainsKey((string?)element.Attribute("Name") ?? string.Empty)
        ).Remove();
        foreach (var (name, target) in _properties)
        {
            try
            {
                root.Add(
                    new XElement(
                        "Setting",
                        new XAttribute("Name", name),
                        new XAttribute(
                            "Value",
                            HumanReadableConverter.ConvertToString(
                                target.Property.GetValue(target.Owner, null) ?? string.Empty
                            )
                        )
                    )
                );
            }
            catch (Exception exception)
            {
                Log.Warning($"Setting '{name}' could not be saved. Reason: {exception.Message}");
            }
        }

        root.Elements("Configs").Remove();
        root.Add(new XElement("Configs", _configs.Select(config => new XAttribute(config.Key, config.Value))));
        return root;
    }

    public void Save()
    {
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var directory = Storage.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory)) { directory = _path[..(_path.IndexOf(':') + 1)]; }

        Storage.CreateDirectory(directory);
        try
        {
            using (var stream = Storage.OpenFile(temporaryPath, OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(Write(), stream, null, true);
            }

            Storage.MoveFile(temporaryPath, _path);
        }
        finally
        {
            if (Storage.FileExists(temporaryPath)) { Storage.DeleteFile(temporaryPath); }
        }

        Log.Information("Saved settings");
    }
}
