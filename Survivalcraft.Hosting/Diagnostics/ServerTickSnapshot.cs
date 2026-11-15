namespace Game.Diagnostics;

public sealed record ServerTickSnapshot(
    long Ticks,
    long OverBudgetTicks,
    double WorkMilliseconds,
    double MaximumWorkMilliseconds,
    double MaximumStartLatenessMilliseconds
);
