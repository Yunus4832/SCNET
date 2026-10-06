namespace Game.Network;

public readonly record struct NetworkRoutingStatistics(
    long Evaluations, long SelectedPackages, double TotalMilliseconds, double MaximumMilliseconds);
