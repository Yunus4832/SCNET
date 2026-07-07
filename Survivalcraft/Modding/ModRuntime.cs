namespace Game.Modding;

public sealed record ModDescriptor(
    ModManifest Manifest,
    Func<IMod> Factory,
    IDisposable? Lifetime = null,
    string? PackageHash = null);

public enum ModState
{
    Discovered,
    Configuring,
    Configured,
    Starting,
    Started,
    Stopping,
    Stopped,
    Failed
}

public sealed class ModRuntime
{
    internal ModRuntime(ModDescriptor descriptor, IMod instance, IModContext context)
    {
        Descriptor = descriptor;
        Instance = instance;
        Context = context;
    }

    public ModDescriptor Descriptor { get; }

    public IMod Instance { get; }

    public IModContext Context { get; }

    public ModState State { get; internal set; } = ModState.Discovered;

    public Exception? Failure { get; internal set; }
}
