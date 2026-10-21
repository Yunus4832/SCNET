namespace Rendezvous.Protocol;

/// <summary>Accepts reordered authenticated packets within a 64-sequence replay window.</summary>
public sealed class ReplayWindow
{
    private ulong _highest;

    private ulong _seen;

    public bool TryAccept(ulong sequence)
    {
        if (sequence == 0)
        {
            return false;
        }

        if (sequence > _highest)
        {
            var distance = sequence - _highest;
            _seen = distance >= 64 ? 1 : (_seen << (int)distance) | 1;
            _highest = sequence;
            return true;
        }

        var behind = _highest - sequence;
        if (behind >= 64 || (_seen & (1UL << (int)behind)) != 0)
        {
            return false;
        }

        _seen |= 1UL << (int)behind;
        return true;
    }
}
