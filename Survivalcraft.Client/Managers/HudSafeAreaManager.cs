namespace Game.Managers;

public static class HudSafeAreaManager
{
    public static float GetEffectivePadding(float globalScale)
    {
        if (!Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled)
        {
            return 0f;
        }

        return Configuration.ClientConfiguration.Preferences.HudSafeAreaCustomPaddingEnabled
            ? Configuration.ClientConfiguration.Preferences.HudSafeAreaPadding
            : GetAutomaticPadding(globalScale);
    }

    public static float GetAutomaticPadding(float globalScale)
    {
        var insets = Window.DisplayCutoutInsets;
        var horizontalInset = MathUtils.Max(insets.X, insets.Z);
        return MathUtils.Clamp(horizontalInset / MathUtils.Max(globalScale, 0.001f), 0f, 80f);
    }
}
