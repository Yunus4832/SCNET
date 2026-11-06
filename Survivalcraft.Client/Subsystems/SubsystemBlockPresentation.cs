using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Subsystems;

public sealed class SubsystemBlockPresentation : Subsystem
{
    private BlockPresentationCatalog? _catalog;

    private bool _disposed;

    public BlockPresentationCatalog Catalog => _catalog ??
                                               throw new InvalidOperationException(
                                                   "World block presentation was not configured."
                                               );

    public void Bind(BlockPresentationCatalog catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(catalog);
        if (_catalog is not null)
        {
            throw new InvalidOperationException("World block presentation is already configured.");
        }

        _catalog = catalog;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Block presentation requires a presentation world.");
        }

        _ = Catalog;
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        Interlocked.Exchange(ref _catalog, null)?.Dispose();
    }
}
