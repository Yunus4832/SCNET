namespace Game.Characters;

public static class StaminaSimulation
{
    public static float Step(
        float stamina,
        float dt,
        float effort,
        bool jumped,
        bool swimming,
        float speedFactor,
        bool ill
    )
    {
        var cost = 1f / MathUtils.Max(speedFactor, .75f) * (ill ? 5 : 1);
        var value = MathUtils.Saturate(stamina + dt * .07f);
        if (jumped) { value = MathUtils.Saturate(value - .025f * cost); }

        return MathUtils.Saturate(
            value -
            dt *
            (swimming
                ? .07f + .006f * cost + .008f * effort
                : (.07f + .006f * cost) * effort)
        );
    }
}
