using System.Xml.Linq;

using Game.Content;

namespace Game.Managers;

public sealed class ContentRepositoryManager
{
    public static ContentRepositoryManager Current { get; private set; } =
        new(new ContentRepositorySettings(), _ => { });

    private readonly Lock _gate = new();
    private readonly Action<ContentRepositorySettings> _save;
    private Guid? _gameInformationSourceId;
    private IReadOnlyList<ContentRepository> _repositories;

    public static void Initialize()
    {
        Current = new ContentRepositoryManager(LoadOrDefault(), Save);
    }

    public ContentRepositoryManager(ContentRepositorySettings settings, Action<ContentRepositorySettings> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);
        _save = save;
        var normalized = Normalize(settings);
        _repositories = normalized.Repositories;
        _gameInformationSourceId = normalized.GameInformationSourceId;
    }

    public IReadOnlyList<ContentRepository> Snapshot()
    {
        lock (_gate)
        {
            return _repositories.ToArray();
        }
    }

    public ContentRepository? GameInformationSource
    {
        get
        {
            lock (_gate)
            {
                return _repositories.FirstOrDefault(item => item.Id == _gameInformationSourceId);
            }
        }
    }

    public void SelectGameInformationSource(Guid repositoryId)
    {
        lock (_gate)
        {
            var selected = _repositories.FirstOrDefault(item => item.Id == repositoryId);
            if (selected is null || !selected.IsEnabled)
            {
                throw new InvalidOperationException("Only an enabled repository can provide game information.");
            }

            Commit(_repositories, repositoryId);
        }
    }

    public void Add(ContentRepository repository)
    {
        lock (_gate)
        {
            Commit(_repositories.Append(repository), _gameInformationSourceId);
        }
    }

    public void Edit(ContentRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        lock (_gate)
        {
            EnsureExists(repository.Id);
            var sourceId = repository.Id == _gameInformationSourceId && !repository.IsEnabled
                ? null
                : _gameInformationSourceId;
            Commit(_repositories.Select(item => item.Id == repository.Id ? repository : item), sourceId);
        }
    }

    public void Delete(Guid repositoryId)
    {
        lock (_gate)
        {
            EnsureExists(repositoryId);
            Commit(_repositories.Where(item => item.Id != repositoryId),
                repositoryId == _gameInformationSourceId ? null : _gameInformationSourceId);
        }
    }

    public void SetOrder(IReadOnlyList<Guid> repositoryIds)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        lock (_gate)
        {
            if (repositoryIds.Count != _repositories.Count || repositoryIds.Distinct().Count() != repositoryIds.Count ||
                repositoryIds.Any(id => _repositories.All(repository => repository.Id != id)))
            {
                throw new ArgumentException("Repository order must contain every configured repository exactly once.",
                    nameof(repositoryIds));
            }

            var repositoriesById = _repositories.ToDictionary(repository => repository.Id);
            Commit(repositoryIds.Select((id, priority) => repositoriesById[id] with { Priority = priority }),
                _gameInformationSourceId);
        }
    }

    private void EnsureExists(Guid repositoryId)
    {
        if (_repositories.All(item => item.Id != repositoryId))
        {
            throw new KeyNotFoundException("The repository does not exist.");
        }
    }

    private void Commit(IEnumerable<ContentRepository> repositories, Guid? gameInformationSourceId)
    {
        var normalized = Normalize(new ContentRepositorySettings
        {
            GameInformationSourceId = gameInformationSourceId,
            Repositories = repositories.ToArray()
        });
        _save(normalized);
        _repositories = normalized.Repositories;
        _gameInformationSourceId = normalized.GameInformationSourceId;
    }

    public static ContentRepositorySettings Load()
    {
        if (!Storage.FileExists(GamePaths.ContentRepositoriesFile))
        {
            return new ContentRepositorySettings();
        }

        using var stream = Storage.OpenFile(GamePaths.ContentRepositoriesFile, OpenFileMode.Read);
        return Read(XElement.Load(stream));
    }

    private static ContentRepositorySettings LoadOrDefault()
    {
        try
        {
            return Load();
        }
        catch (Exception exception)
        {
            ExceptionManager.ReportExceptionToUser("Loading content repositories failed.", exception);
            return new ContentRepositorySettings();
        }
    }

    public static void Save(ContentRepositorySettings settings)
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }

        using var stream = Storage.OpenFile(GamePaths.ContentRepositoriesFile, OpenFileMode.Create);
        Write(settings).Save(stream);
    }

    public static ContentRepositorySettings Read(XElement document)
    {
        if (document.Name != "ContentRepositories")
        {
            throw new FormatException("Invalid content repositories root element.");
        }

        Guid? sourceId = null;
        if (document.Attribute("GameInformationSourceId") is { } sourceIdAttribute)
        {
            sourceId = Guid.Parse(sourceIdAttribute.Value);
        }

        return Normalize(new ContentRepositorySettings
        {
            GameInformationSourceId = sourceId,
            Repositories = ContentRepository.NormalizeAll(document.Elements("Repository").Select(item =>
                new ContentRepository
                {
                    Id = Guid.Parse(Required(item, "Id")),
                    Name = Required(item, "Name"),
                    BaseUrl = Required(item, "BaseUrl"),
                    IsEnabled = bool.Parse(Required(item, "IsEnabled")),
                    Priority = int.Parse(Required(item, "Priority"),
                        System.Globalization.CultureInfo.InvariantCulture)
                }))
        });
    }

    public static XElement Write(ContentRepositorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);
        return new XElement("ContentRepositories",
            normalized.GameInformationSourceId is { } sourceId
                ? new XAttribute("GameInformationSourceId", sourceId)
                : null,
            normalized.Repositories.Select(item => new XElement("Repository",
                new XAttribute("Id", item.Id),
                new XAttribute("Name", item.Name),
                new XAttribute("BaseUrl", item.BaseUrl),
                new XAttribute("IsEnabled", item.IsEnabled),
                new XAttribute("Priority", item.Priority))));
    }

    private static ContentRepositorySettings Normalize(ContentRepositorySettings settings)
    {
        var repositories = ContentRepository.NormalizeAll(settings.Repositories);
        if (settings.GameInformationSourceId is { } sourceId &&
            repositories.All(item => item.Id != sourceId || !item.IsEnabled))
        {
            throw new ArgumentException("The game information source must reference an enabled repository.");
        }

        return settings with { Repositories = repositories };
    }

    private static string Required(XElement item, string name)
    {
        return item.Attribute(name)?.Value ?? throw new FormatException($"Missing repository attribute '{name}'.");
    }
}
