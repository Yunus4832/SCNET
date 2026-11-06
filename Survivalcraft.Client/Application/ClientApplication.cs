using Game.ClientContent;
using Game.World.Presentation;

namespace Game.Application;

/// <summary>Owns client application content, frame telemetry and pending platform URI delivery.</summary>
public static class ClientApplication
{
    public static Commands.Http.HttpPlayerCommandAdapter? PlayerCommands { get; set; }

    private static ClientContentRuntime? _content;

    private static ClientStoragePaths? _paths;

    private static WorldRepository? _worlds;

    public static ClientStoragePaths Paths =>
        _paths ?? throw new InvalidOperationException("Client storage paths have not been configured.");

    public static WorldRepository Worlds =>
        _worlds ?? throw new InvalidOperationException("World repository has not been configured.");

    public static void ConfigurePaths(ClientStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public static void InstallWorldRepository(WorldRepository worlds)
    {
        ArgumentNullException.ThrowIfNull(worlds);
        _worlds = worlds;
    }

    private static readonly List<ClientUriRequest> _uris = [];

    public static ClientContentRuntime Content =>
        _content ?? throw new InvalidOperationException("Client content has not been installed.");

    private static ClientWorldPresentation? _world;

    public static ClientWorldPresentation? World => _world;

    public static void BindWorld(ClientWorldPresentation world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var previous = _world;
        _world = world;
        previous?.Dispose();
    }

    public static void ReleaseWorld()
    {
        var previous = _world;
        _world = null;
        previous?.Dispose();
    }

    public static float LastFrameTime { get; set; }

    public static float LastCpuFrameTime { get; set; }

    public static event Action<ClientUriRequest>? HandleUri;

    public static void InstallContent(ClientContentRuntime content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var previous = _content;
        _content = content;
        previous?.Dispose();
    }

    public static void ReleaseContent()
    {
        var previous = _content;
        _content = null;
        previous?.Dispose();
    }

    public static void EnqueueUri(Uri uri) => _uris.Add(new ClientUriRequest(uri));

    public static void DispatchUris()
    {
        foreach (var request in _uris.ToArray())
        {
            HandleUri?.Invoke(request);
            if (request.IsHandled) { _uris.Remove(request); }
        }
    }
}
