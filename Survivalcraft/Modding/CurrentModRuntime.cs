namespace Game.Modding;

public static class CurrentModRuntime
{
    public static GameModRuntime? Value { get; private set; }

    public static void Set(GameModRuntime? runtime)
    {
        Value = runtime;
    }
}
