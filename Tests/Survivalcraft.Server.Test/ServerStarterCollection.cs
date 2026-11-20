using Engine.Core.FileStorage;

namespace Survivalcraft.Server.Test;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServerStarterCollection : ICollectionFixture<ServerTestStorage>
{
    public const string Name = "ServerStarter";
}

public sealed class ServerTestStorage : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "scnet-server-tests-" + Guid.NewGuid().ToString("N")
    );

    public ServerTestStorage()
    {
        Directory.CreateDirectory(_directory);
        Storage.RegisterFileSystemRoot("app", AppContext.BaseDirectory, readOnly: true);
        Storage.RegisterFileSystemRoot("config", Path.Combine(_directory, "Config"));
        Storage.RegisterFileSystemRoot("data", Path.Combine(_directory, "Data"));
        Storage.RegisterFileSystemRoot("external", _directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
    }
}
