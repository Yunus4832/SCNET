using System.Reflection;

using Engine.Core.Runtime;

namespace Survivalcraft.Test.Fixtures;

/// <summary>Pumps the existing frame dispatcher while an isolated test waits for an asynchronous host operation.</summary>
internal sealed class FrameTestLoop : IDisposable
{
    private static readonly FieldInfo _thread = typeof(Dispatcher).GetField(
        "_mainThreadId",
        BindingFlags.Static | BindingFlags.NonPublic
    )!;

    private readonly object? _previous = _thread.GetValue(null);

    public FrameTestLoop() => Dispatcher.Initialize();

    public void Complete(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < until)
        {
            Dispatcher.BeforeFrame();
            Thread.Sleep(1);
        }

        Assert.True(task.IsCompleted, "Host operation did not complete while the frame dispatcher was being pumped.");
        task.GetAwaiter().GetResult();
        Dispatcher.BeforeFrame();
    }

    public T Complete<T>(Task<T> task)
    {
        Complete((Task)task);
        return task.GetAwaiter().GetResult();
    }

    public void Dispose() => _thread.SetValue(null, _previous);
}
