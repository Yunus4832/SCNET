using Game.Localization;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.World.Presentation;

namespace Game.ClientContent;

/// <summary>Application-owned presentation factories and preview resources; world catalogs have their own owners.</summary>
public sealed class ClientContentRuntime : IDisposable
{
    private readonly BlockPresentationRegistration[] _registrations;

    private BlockPresentationCatalog? _previewCatalog;

    private bool _disposed;

    private readonly HashSet<ModId> _installedOwners = [];

    public void InstallResources(IEnumerable<RegisteredContentResource> resources)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BuiltInContentReaders.Register();
        foreach (var entry in resources)
        {
            if (ContentManager.ContainsOwned(entry.Id.Namespace, entry.ContentPath)) { continue; }

            using var source = entry.OpenRead();
            var bytes = new MemoryStream();
            source.CopyTo(bytes);
            bytes.Position = 0;
            var content = new ContentInfo(entry.ContentPath);
            content.SetContentStream(bytes);
            try
            {
                ContentManager.AddOwned(entry.Id.Namespace, content);
                _installedOwners.Add(entry.Id.Namespace);
            }
            catch
            {
                content.Dispose();
                throw;
            }
        }
    }

    public void PublishLanguage(LanguageCatalog catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClientLanguage.Publish(catalog);
    }


    public SeasonPresentation Seasons { get; } = new();

    public ClientContentRuntime(IEnumerable<IClientContentModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _registrations = modules.SelectMany(module => module.BlockPresentations).ToArray();
        if (_registrations.Select(registration => registration.Id).Distinct().Count() != _registrations.Length)
        {
            throw new InvalidOperationException("Duplicate block presentation registrations.");
        }
    }

    public BlockPresentationCatalog PreviewCatalog => _previewCatalog ??
                                                      throw new InvalidOperationException(
                                                          "Preview presentation content has not been initialized."
                                                      );

    public BlockPresentationCatalog CreateCatalog(BlockContentCatalog rules)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(rules);
        var catalog = new BlockPresentationCatalog(rules, _registrations);
        try
        {
            catalog.Initialize();
            return catalog;
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    public void InitializePreview(BlockContentCatalog rules)
    {
        var catalog = CreateCatalog(rules);
        var previous = _previewCatalog;
        _previewCatalog = catalog;
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        var errors = new List<Exception>();
        var preview = _previewCatalog;
        _previewCatalog = null;
        foreach (var owner in _installedOwners)
        {
            try { ContentManager.RemoveOwner(owner); }
            catch (Exception error) { errors.Add(error); }
        }

        _installedOwners.Clear();
        try { preview?.Dispose(); }
        catch (Exception error) { errors.Add(error); }

        if (errors.Count > 0) { throw new AggregateException("Failed to release client content.", errors); }
    }
}
