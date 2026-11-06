using EntitySystem.Core;

namespace Game.Hosting;

public sealed class WorldProjectActivity
{
    private readonly object _sync = new();

    private readonly Dictionary<WorldRuntimeContext, Registration> _worlds = [];

    private readonly HashSet<FileOperation> _fileOperations = [];

    public IReadOnlyList<Project> Projects
    {
        get
        {
            lock (_sync)
            {
                return _worlds.Keys.Where(world => !world.IsClosed)
                    .Select(world => world.BoundProject)
                    .OfType<Project>().Where(project => !project.IsDisposed).ToArray();
            }
        }
    }

    public void Track(WorldRuntimeContext world, string? sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(world);
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        var registration = new Registration(this, world, sourceDirectory);
        lock (_sync)
        {
            if (sourceDirectory is not null &&
                _fileOperations.Any(operation =>
                    Overlaps(operation.SystemPath, Storage.GetSystemPath(sourceDirectory))
                ))
            {
                throw new InvalidOperationException("The world files are currently being modified.");
            }

            if (!_worlds.TryAdd(world, registration))
            {
                throw new InvalidOperationException("The world already has a storage activity registration.");
            }
        }

        try { world.OwnFinalWorldResource(registration); }
        catch
        {
            registration.Dispose();
            throw;
        }
    }

    public bool IsDirectoryInUse(string directory)
    {
        var target = Storage.GetSystemPath(directory);
        lock (_sync)
        {
            return _worlds.Values.Any(registration => registration.SourceDirectory is { } source &&
                                                      Overlaps(
                                                          target,
                                                          Storage.GetSystemPath(source)
                                                      )
            );
        }
    }

    public IDisposable BeginFileOperation(string directory)
    {
        var target = Storage.GetSystemPath(directory);
        lock (_sync)
        {
            if (IsDirectoryInUse(directory) || _fileOperations.Any(operation => Overlaps(target, operation.SystemPath)))
            {
                throw new InvalidOperationException(
                    "Close the world before replacing, deleting, or restoring its files."
                );
            }

            var operation = new FileOperation(this, target);
            _fileOperations.Add(operation);
            return operation;
        }
    }

    private sealed class FileOperation(WorldProjectActivity owner, string systemPath) : IDisposable
    {
        public string SystemPath { get; } = systemPath;

        public void Dispose()
        {
            lock (owner._sync) { owner._fileOperations.Remove(this); }
        }
    }

    private static bool Overlaps(string left, string right) => Contains(left, right) || Contains(right, left);

    private static bool Contains(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return relative == "." ||
               !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private sealed class Registration(WorldProjectActivity owner, WorldRuntimeContext world, string? sourceDirectory)
        : IDisposable
    {
        public string? SourceDirectory { get; } = sourceDirectory;

        public void Dispose()
        {
            lock (owner._sync)
            {
                if (owner._worlds.TryGetValue(world, out var current) && ReferenceEquals(current, this))
                {
                    owner._worlds.Remove(world);
                }
            }
        }
    }
}
