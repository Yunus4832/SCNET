namespace Game.Configuration;

public static class ClientConfiguration
{
    public static ClientPreferences Preferences { get; } = new();

    public static event Action? BrightnessChanged;

    public static void SetBrightness(float value)
    {
        value = MathUtils.Clamp(value, 0f, 1f);
        if (value.CloseTo(Preferences.Brightness)) { return; }

        Preferences.Brightness = value;
        BrightnessChanged?.Invoke();
    }
}
