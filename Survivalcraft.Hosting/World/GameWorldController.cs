using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Hosting;
using Game.Startup;

namespace Game.World;

/// <summary>持有实际加载的权威端或客户端世界运行时，并管理宿主的加载和关闭操作。</summary>
public sealed class GameWorldController(
    WorldProjectActivity activity,
    Func<Action, Task>? dispatchClient = null
) : IAsyncDisposable
{
    private volatile GameRuntime? _runtime;

    public GameRuntime? Runtime { get => _runtime; private set => _runtime = value; }

    public WorldInfo? WorldInfo { get; private set; }

    public event Action<Project>? AuthorityLoaded;

    public SessionInfo? WorldSession { get; private set; }

    public LocalGameReturnTarget? LocalPlayerReturnTarget { get; set; }

    private readonly Lock _gate = new();

    private CancellationTokenSource? _loading;

    private TaskCompletionSource? _loadCompletion;

    public async Task LoadAuthorityAsync(WorldLoadRequest request, CancellationToken cancellation = default)
    {
        if (Runtime?.Authority is { IsClosed: false })
        {
            throw new InvalidOperationException("Close the existing authority world before loading another world.");
        }

        if (request.AuthorityNode.Role != NodeRole.Authority || request.AuthorityNode.IsTransportRunning)
        {
            throw new InvalidOperationException("Authority loading requires an idle authority endpoint.");
        }

        var loading = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_loading != null)
            {
                loading.Dispose();
                throw new InvalidOperationException("World loading is already active.");
            }

            _loading = loading;
            _loadCompletion = loaded;
        }

        GameRuntime? game = null;
        try
        {
            if (Runtime is { } previous) { await previous.CloseAsync(false).ConfigureAwait(false); }

            loading.Token.ThrowIfCancellationRequested();
            if (request.World.ProjectFormatVersion != WorldVersions.ProjectFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported project format '{request.World.ProjectFormatVersion}'."
                );
            }

            game = new GameRuntime(request.AllowsLocalPause, dispatchClient);
            lock (_gate)
            {
                loading.Token.ThrowIfCancellationRequested();
                WorldInfo = request.World;
                WorldSession = request.StartupSession;
                Runtime = game;
            }

            await game.StartAuthorityAsync(ownerCancellation =>
                {
                    using var linked =
                        CancellationTokenSource.CreateLinkedTokenSource(ownerCancellation, loading.Token);
                    return LoadAuthorityProject(request, linked.Token);
                }
            ).ConfigureAwait(false);
        }
        catch
        {
            if (game != null)
            {
                try { await game.CloseAsync(false).ConfigureAwait(false); }
                catch (Exception cleanup) { Log.Error($"Failed authority load cleanup: {cleanup}"); }

                lock (_gate)
                {
                    if (ReferenceEquals(Runtime, game)) { Clear(); }
                }
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_loading, loading))
                {
                    _loading = null;
                    _loadCompletion = null;
                }

                loading.Dispose();
            }

            loaded.TrySetResult();
        }
    }

    private WorldRuntimeContext LoadAuthorityProject(WorldLoadRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var runtime = request.AuthorityNode.PrepareWorldRuntime();
        activity.Track(
            runtime,
            request.World.DirectoryName,
            operation =>
            {
                var game = Runtime;
                if (!ReferenceEquals(game?.Authority, runtime) || game?.AuthorityExecution is not { } execution)
                {
                    return Task.FromException(
                        new InvalidOperationException("The authority world is not available for editing.")
                    );
                }

                return execution.InvokeAsync(_ =>
                    {
                        operation();
                        return true;
                    }
                );
            }
        );
        try
        {
            var projectPath = Storage.CombinePaths(request.World.DirectoryName, "Project.xml");
            if (!Storage.FileExists(projectPath))
            {
                throw new FileNotFoundException("Project file not found.", projectPath);
            }

            XElement node;
            using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Read))
            {
                node = XmlUtils.LoadXmlFromStream(stream, null, true);
            }

            WorldRepository.ValidateSavedGenerator(node);
            var overrides = new ValuesDictionary
            {
                { "GameInfo", new ValuesDictionary { { "WorldDirectoryName", request.World.DirectoryName } } }
            };
            token.ThrowIfCancellationRequested();
            var catalog = request.Mods.CreateWorldCatalog(
                runtime,
                BlockIndexMapping.FromXml(node.Element("BlockRegistry"))
            );
            token.ThrowIfCancellationRequested();
            var project = AuthorityWorldProjectFactory.Create(
                runtime,
                catalog,
                request.Database,
                node,
                overrides,
                request.Policies,
                metadata => WorldProjectPersistence.SaveMetadata(request.World.DirectoryName, metadata)
            );
            if (request.World.GameModeOverride is { } mode)
            {
                project.FindSubsystem<SubsystemWorldHosting>(true)!.ApplyGameModeOverride(mode);
                request.World.WorldSettings.GameMode = mode;
            }

            project.FindSubsystem<SubsystemWorldHosting>(true)!.ApplyRunServerOverride(request.World.Hosting.RunServer);
            var characters = project.FindSubsystem<SubsystemCharacters>(true)!;
            foreach (var character in characters.Characters.ToArray())
            {
                characters.MakeCharacterOffline(character.CharacterId, false);
            }

            request.AuthorityNode.TurnOnPackageHandle(project);
            AuthorityWorldNetworkSetup.Attach(project, request.AuthorityNode, request.Textures);
            runtime.OwnWorldResource(new WorldTimeReplication(project));
            token.ThrowIfCancellationRequested();
            AuthorityLoaded?.Invoke(project);
            return runtime;
        }
        catch (Exception primary)
        {
            var errors = new List<Exception> { primary };
            var project = runtime.BoundProject;
            try { runtime.Close(); }
            catch (Exception error) { errors.Add(error); }

            try { project?.Dispose(); }
            catch (Exception error) { errors.Add(error); }

            try { request.AuthorityNode.StopImmediate(); }
            catch (Exception error) { errors.Add(error); }

            if (errors.Count > 1) { throw new AggregateException("Authority load and cleanup failed.", errors); }

            throw;
        }
    }

    public GameRuntime RequireRuntime() => Runtime ?? throw new InvalidOperationException("No game runtime is loaded.");

    public void AttachClient(WorldRuntimeContext runtime)
    {
        lock (_gate)
        {
            Runtime ??= new GameRuntime(false, dispatchClient);
            if (Runtime.Client != null && !ReferenceEquals(Runtime.Client, runtime))
            {
                throw new InvalidOperationException("Release the existing client before attaching a replacement.");
            }

            Runtime.AttachClient(runtime);
        }
    }

    public async Task CloseAsync(bool save = false)
    {
        GameRuntime? runtime;
        Task? loading;
        lock (_gate)
        {
            _loading?.Cancel();
            runtime = Runtime;
            loading = _loadCompletion?.Task;
        }

        try
        {
            if (runtime != null) { await runtime.CloseAsync(save).ConfigureAwait(false); }

            if (loading != null) { await loading.ConfigureAwait(false); }
        }
        finally
        {
            lock (_gate)
            {
                if (runtime is null or { IsDisposed: true } && ReferenceEquals(Runtime, runtime)) { Clear(); }
            }
        }
    }

    private void Clear()
    {
        Runtime = null;
        WorldInfo = null;
        WorldSession = null;
        LocalPlayerReturnTarget = null;
    }

    public ValueTask DisposeAsync() => new(CloseAsync());
}
