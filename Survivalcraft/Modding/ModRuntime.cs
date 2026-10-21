namespace Game.Modding;

public sealed record ModDescriptor(
    ModManifest Manifest,
    Func<IMod> Factory,
    IDisposable? Lifetime = null,
    string? PackageHash = null
);

public enum ModState
{
    Discovered = 0,

    Configuring = 1,

    Configured = 2,

    Starting = 3,

    Started = 4,

    Stopping = 5,

    Stopped = 6,

    Failed = 7
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
