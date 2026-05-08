using Engine.Core;

namespace Engine.Graphics;

public abstract class GraphicsResource : IDisposable
{
    internal static readonly HashSet<GraphicsResource> resources = [];

    internal bool isDisposed;

    internal GraphicsResource()
    {
        resources.Add(this);
    }

    public virtual void Dispose()
    {
        isDisposed = true;
        resources.Remove(this);
    }

    ~GraphicsResource()
    {
        Dispatcher.Dispatch(Dispose);
    }

    public abstract int GetGpuMemoryUsage();

    public abstract void HandleDeviceLost();

    public abstract void HandleDeviceReset();

    internal void VerifyNotDisposed()
    {
        if (isDisposed)
        {
            throw new InvalidOperationException("GraphicsResource is disposed.");
        }
    }
}
