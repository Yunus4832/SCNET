using Engine.Network.Rendezvous;

using EntitySystem.TemplatesDatabase;

namespace Game.Servers;

public sealed record P2PRoomSettings
{
    public bool Enabled { get; init; }

    public string ControlUrl { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsPublic { get; init; }

    public P2PRoomSettings SelectService(InstalledRendezvousService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return this with
        {
            ControlUrl = RendezvousAddress.Normalize(new Uri(service.ControlUrl)).AbsoluteUri,
            ServiceName = service.Name
        };
    }

    public ValuesDictionary Save(string worldName)
    {
        var values = new ValuesDictionary();
        values.SetValue(nameof(Enabled), Enabled);
        values.SetValue(nameof(ControlUrl), ControlUrl);
        values.SetValue(nameof(ServiceName), ServiceName);
        values.SetValue(nameof(Name), string.IsNullOrEmpty(Name) ? worldName : Name);
        values.SetValue(nameof(Description), Description);
        values.SetValue(nameof(IsPublic), IsPublic);
        return values;
    }

    public static P2PRoomSettings Load(ValuesDictionary values) => new()
    {
        Enabled = values.GetValue(nameof(Enabled), false),
        ControlUrl = values.GetValue(nameof(ControlUrl), string.Empty),
        ServiceName = values.GetValue(nameof(ServiceName), string.Empty),
        Name = values.GetValue(nameof(Name), string.Empty),
        Description = values.GetValue(nameof(Description), string.Empty),
        IsPublic = values.GetValue(nameof(IsPublic), false)
    };
}
