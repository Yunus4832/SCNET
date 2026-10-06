namespace Game.Network;

public readonly record struct NetworkPackageFanoutStatistics(
    Type PackageType,
    NetworkChannel Channel,
    long Messages,
    long RecipientDeliveries,
    int MaximumFanout)
{
    public double AverageFanout => Messages == 0 ? 0d : (double)RecipientDeliveries / Messages;
}
