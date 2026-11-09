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

    private Task _pending = Task.CompletedTask;

    private bool _disposed;

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

    public Guid Save(bool waitForCompletion, Guid operationId = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (operationId == Guid.Empty) { operationId = Guid.NewGuid(); }

        var info = _project.FindSubsystem<SubsystemGameInfo>(true)!;
        var worldName = info.WorldSettings.Name;
        var directory = info.DirectoryName;
        _saveGate.Wait();
        if (_disposed || _project.IsDisposed)
        {
            _saveGate.Release();
            throw new ObjectDisposedException(nameof(WorldProjectPersistence));
        }

        var terrain = _project.FindSubsystem<SubsystemTerrain>();
        var previousTerrainSave = terrain?.PendingTerrainSave;
        ProjectData? data = null;
        Exception? captureFailure = null;
        try { data = _project.Save(); }
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

        var requestId = operationId;
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
        _pending = io;
        var notification = io.ContinueWith(
            completed => Publish(completed.GetAwaiter().GetResult()),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
        if (waitForCompletion)
        {
            notification.GetAwaiter().GetResult();
            if (io.GetAwaiter().GetResult().Failure is { } failure)
            {
                throw new WorldSaveException(worldName, failure);
            }
        }

        return operationId;
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
        _pending.GetAwaiter().GetResult();
        _owners.Remove(_project);
        SaveCompleted = null;
        SaveFailed = null;
    }
}
