using EntitySystem.Core;

namespace Game.World.Presentation;

/// <summary>Actual presentation-side world identity and explicit requests to its application lifecycle owner.</summary>
public sealed class ClientWorldPresentation : IDisposable
{
    private bool _disposed;

    public WorldRuntimeContext Runtime { get; }

    public Project Project { get; }

    public string? RequestedCharacterName { get; init; }

    public WorldInfo? WorldInfo { get; }

    public WorldRuntimeContext? LocalAuthority { get; }

    public bool IsMultiplayer { get; private set; }

    public ComponentCharacter? LocalCharacter => Project.FindSubsystem<SubsystemCharacters>()?.LocalCharacter;

    public event Action<ClientWorldSaveRequest>? SaveRequested;

    public event Action? LeaveRequested;

    public ClientWorldPresentation(
        WorldRuntimeContext runtime,
        Project project,
        WorldInfo? worldInfo,
        WorldRuntimeContext? localAuthority
    )
    {
        if (runtime.IsAuthority || !ReferenceEquals(runtime.Project, project))
        {
            throw new ArgumentException("Client world presentation requires its actual client project.");
        }

        Runtime = runtime;
        Project = project;
        WorldInfo = worldInfo;
        LocalAuthority = localAuthority;
        Project.Disposed += OnProjectDisposed;
    }

    public void SetMultiplayer(bool multiplayer) => IsMultiplayer = multiplayer;

    public WorldRuntimeContext? GetLocalAuthority(Project project)
    {
        return !_disposed && ReferenceEquals(project, Project) ? LocalAuthority : null;
    }

    public void RequestSave(bool waitForCompletion, bool showErrorDialog)
    {
        if (!_disposed)
        {
            SaveRequested?.Invoke(new ClientWorldSaveRequest(Project, waitForCompletion, showErrorDialog));
        }
    }

    public void RequestLeave()
    {
        if (!_disposed) { LeaveRequested?.Invoke(); }
    }

    private void OnProjectDisposed(Project project) => Dispose();

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        Project.Disposed -= OnProjectDisposed;
        SaveRequested = null;
        LeaveRequested = null;
    }
}
