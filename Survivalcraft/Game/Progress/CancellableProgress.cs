namespace Game;

public class CancellableProgress : Progress
{
    public readonly CancellationToken CancellationToken;

    public readonly CancellationTokenSource CancellationTokenSource = new();

    public CancellableProgress()
    {
        CancellationToken = CancellationTokenSource.Token;
    }

    public event Action? Cancelled;

    public void Cancel()
    {
        CancellationTokenSource.Cancel();
        Cancelled?.Invoke();
    }
}
