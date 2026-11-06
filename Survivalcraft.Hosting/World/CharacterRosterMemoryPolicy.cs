namespace Game.World;

public sealed class CharacterRosterMemoryPolicy : IDisposable
{
    private readonly WorldRuntimeContext _runtime;

    private readonly SubsystemCharacters _characters;

    private readonly Func<bool> _shouldCollect;

    private readonly Action _collect;

    private bool _disposed;

    public CharacterRosterMemoryPolicy(
        WorldRuntimeContext runtime,
        SubsystemCharacters characters,
        Func<bool> shouldCollect,
        Action collect
    )
    {
        _runtime = runtime;
        _characters = characters;
        _shouldCollect = shouldCollect;
        _collect = collect;
        characters.RosterMutationCompleted += OnRosterMutationCompleted;
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _characters.RosterMutationCompleted -= OnRosterMutationCompleted;
    }

    private void OnRosterMutationCompleted(CharacterRosterMutation mutation)
    {
        if (!_disposed && !_runtime.IsClosed && _shouldCollect()) { _collect(); }
    }
}
