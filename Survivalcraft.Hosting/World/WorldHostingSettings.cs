using EntitySystem.TemplatesDatabase;

using Game.Servers;

namespace Game.World;

public sealed class WorldHostingSettings
{
    public bool RunServer;

    public string? ServerIdentityAddress;

    public P2PRoomSettings P2P = new();

    public void Load(ValuesDictionary values)
    {
        RunServer = values.GetValue(nameof(RunServer), false);
        var identity = values.GetValue(nameof(ServerIdentityAddress), string.Empty);
        ServerIdentityAddress = identity.Length == 0 ? null : IdentityAddress.Normalize(identity);
        P2P = P2PRoomSettings.Load(values.GetValue(nameof(P2P), new ValuesDictionary()));
    }

    public void Save(ValuesDictionary values, string worldName)
    {
        values.SetValue(nameof(RunServer), RunServer);
        values.SetValue(nameof(ServerIdentityAddress), ServerIdentityAddress ?? string.Empty);
        values.SetValue(nameof(P2P), P2P.Save(worldName));
    }
}
