namespace Game.Network;

public readonly record struct NetworkInterestStatistics(
    long Updates, double TotalMilliseconds, double MaximumMilliseconds, long AllocatedBytes)
{
    internal NetworkInterestStatistics AddSample(double milliseconds, long allocatedBytes)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        return new NetworkInterestStatistics(Updates + 1, TotalMilliseconds + milliseconds,
            Math.Max(MaximumMilliseconds, milliseconds), AllocatedBytes + allocatedBytes);
    }
}
