namespace Game.Network.Diagnostics;

public readonly record struct NetworkRoutingStatistics(
    long Evaluations, long SelectedPackages, double TotalMilliseconds, double MaximumMilliseconds);
