using System.Runtime.CompilerServices;
using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.XmlUtilities;

namespace Game.World;

public sealed class WorldProjectPersistence : IDisposable
{
    private static readonly ConditionalWeakTable<Project, WorldProjectPersistence> _owners = new();

    private static readonly SemaphoreSlim _saveGate = new(1, 1);

    private readonly Project _project;

    private readonly BlockIndexMapping _mapping;

    private sealed record SaveRequest(Guid Id, TaskCompletionSource<WorldSaveOutcome> Completion);

    private readonly Queue<SaveRequest> _requests = new();

    private SaveRequest? _current;

    private SaveRequest? _final;

    private Task<WorldSaveOutcome>? _pending;

    private Task _lastWrite = Task.CompletedTask;

    public Task Completion => Volatile.Read(ref _lastWrite);

    public static bool TryGet(Project project, out WorldProjectPersistence? owner) =>
        _owners.TryGetValue(project, out owner);

    private volatile bool _disposed;

    public event Action<WorldSaveOutcome>? SaveCompleted;

    public event Action<WorldSaveOutcome>? SaveFailed;

    public WorldProjectPersistence(Project project)
    {
        _project = project;
        if (!WorldRuntimeContext.For(project).IsAuthority)
        {
            throw new InvalidOperationException("Only authority worlds own project persistence.");
        }

        _mapping = WorldContentBinding.For(project).Catalog.Registry.Mapping;
        _owners.Add(project, this);
    }

    public static WorldProjectPersistence For(Project project) => _owners.TryGetValue(project, out var owner)
        ? owner
        : throw new InvalidOperationException("World persistence has not been loaded.");

    public static void WaitForPendingSaves()
    {
        _saveGate.Wait();
        _saveGate.Release();
    }

    /// <summary>将快照捕获排入所属线程，已有写入任务不会阻塞世界更新循环。</summary>
    public Task<WorldSaveOutcome> SaveAsync(Guid operationId = default, bool final = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WorldRuntimeContext.For(_project).VerifyExecutionAccess();
        if (final && _final != null) { return _final.Completion.Task; }

        if (_final != null) { throw new InvalidOperationException("The final world save is already pending."); }

        if (!final && _requests.Count >= 16) { throw new InvalidOperationException("The world save queue is full."); }

        var request = new SaveRequest(
            operationId == Guid.Empty ? Guid.NewGuid() : operationId,
            new TaskCompletionSource<WorldSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)
        );
        _requests.Enqueue(request);
        if (final) { _final = request; }

        Pump();
        return request.Completion.Task;
    }

    public void Pump()
    {
        if (_disposed) { return; }

        WorldRuntimeContext.For(_project).VerifyExecutionAccess();
        if (_pending is { IsCompleted: true } pending)
        {
            var outcome = pending.GetAwaiter().GetResult();
            var completed = _current!;
            _pending = null;
            _current = null;
            if (ReferenceEquals(_final, completed)) { _final = null; }

            Publish(outcome);
            completed.Completion.TrySetResult(outcome);
            if (_disposed) { return; }
        }

        if (_pending != null || _requests.Count == 0) { return; }

        var info = _project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!_saveGate.Wait(0)) { return; }

        var request = _requests.Dequeue();
        _current = request;
        Capture(request, info);
    }

    private void Capture(SaveRequest request, SubsystemGameInfo info)
    {
        var worldName = info.WorldSettings.Name;
        var directory = info.DirectoryName;
        if (_disposed || _project.IsDisposed)
        {
            _saveGate.Release();
            throw new ObjectDisposedException(nameof(WorldProjectPersistence));
        }

        var terrain = _project.FindSubsystem<SubsystemTerrain>();
        var previousTerrainSave = terrain?.PendingTerrainSave;
        ProjectData? data = null;
        Exception? captureFailure = null;
        try
        {
            terrain?.CompletePendingChunkInitialization();
            data = _project.Save();
        }
        catch (Exception error) { captureFailure = error; }

        var terrainSave = terrain?.PendingTerrainSave ?? Task.CompletedTask;
        if (ReferenceEquals(previousTerrainSave, terrainSave) && terrainSave.IsCompleted)
        {
            terrainSave = Task.CompletedTask;
        }

        if (_disposed || _project.IsDisposed)
        {
            captureFailure ??= new ObjectDisposedException(nameof(WorldProjectPersistence));
        }

        var requestId = request.Id;
        var io = Task.Run(async () =>
            {
                var failure = captureFailure;
                try
                {
                    // Metadata must not commit before this save point's terrain writes.
                    // Even a failed metadata capture must drain any terrain batch it queued.
                    await terrainSave.ConfigureAwait(false);
                    if (failure == null && !string.IsNullOrEmpty(directory))
                    {
                        var node = new XElement("Project");
                        XmlUtils.SetAttributeValue(node, "Version", WorldVersions.ProjectFormatVersion);
                        data!.Save(node);
                        node.Add(_mapping.ToXml());
                        Storage.CreateDirectory(directory);
                        var target = Storage.CombinePaths(directory, "Project.xml");
                        var temporary = Storage.CombinePaths(directory, "Project.xml.tmp");
                        using (var stream = Storage.OpenFile(temporary, OpenFileMode.Create))
                        {
                            XmlUtils.SaveXmlToStream(node, stream, null, true);
                        }

                        if (!IsProjectFileValid(temporary))
                        {
                            throw new InvalidOperationException("Generated project file is invalid.");
                        }

                        Storage.MoveFile(temporary, target);
                    }
                }
                catch (Exception error)
                {
                    failure = failure == null ? error : new AggregateException(failure, error);
                }
                finally { _saveGate.Release(); }

                if (failure != null) { Log.Error($"Failed to save world \"{worldName}\": {failure}"); }

                return new WorldSaveOutcome(requestId, worldName, failure);
            }
        );
        Volatile.Write(ref _lastWrite, io);
        _pending = io;
        _ = io.ContinueWith(
            completed =>
            {
                // A capture callback may dispose the project; completion must still settle without another owner tick.
                if (_disposed) { request.Completion.TrySetResult(completed.GetAwaiter().GetResult()); }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    public static void SaveMetadata(string directory, XElement node)
    {
        _saveGate.Wait();
        try
        {
            var target = Storage.CombinePaths(directory, "Project.xml");
            var temporary = Storage.CombinePaths(directory, "Project.xml.tmp");
            using (var stream = Storage.OpenFile(temporary, OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(node, stream, null, true);
            }

            if (!IsProjectFileValid(temporary))
            {
                throw new InvalidDataException("Generated project metadata is invalid.");
            }

            Storage.MoveFile(temporary, target);
        }
        finally { _saveGate.Release(); }
    }

    public static bool IsProjectFileValid(string path)
    {
        try
        {
            if (!Storage.FileExists(path)) { return false; }

            using var stream = Storage.OpenFile(path, OpenFileMode.Read);
            return XmlUtils.LoadXmlFromStream(stream, null, false).Name == "Project";
        }
        catch (Exception) { return false; }
    }

    private void Publish(WorldSaveOutcome outcome)
    {
        var failed = SaveFailed;
        var completed = SaveCompleted;
        if (outcome.Failure is not null) { Notify(failed, outcome); }

        Notify(completed, outcome);
    }

    private static void Notify(Action<WorldSaveOutcome>? handlers, WorldSaveOutcome outcome)
    {
        foreach (var callback in handlers?.GetInvocationList() ?? [])
        {
            try { ((Action<WorldSaveOutcome>)callback)(outcome); }
            catch (Exception error) { Log.Error($"World save completion subscriber failed: {error}"); }
        }
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        var pending = _pending;
        var current = _current;
        _pending = null;
        _current = null;
        if (pending != null)
        {
            var outcome = pending.GetAwaiter().GetResult();
            Publish(outcome);
            current!.Completion.TrySetResult(outcome);
        }

        while (_requests.TryDequeue(out var request))
        {
            request.Completion.TrySetException(new ObjectDisposedException(nameof(WorldProjectPersistence)));
        }

        _owners.Remove(_project);
        SaveCompleted = null;
        SaveFailed = null;
    }
}
