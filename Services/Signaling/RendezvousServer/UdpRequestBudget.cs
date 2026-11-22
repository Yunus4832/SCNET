using System.Net;

namespace RendezvousServer;

/// <summary>按一秒窗口限制控制流量及所跟踪的来源数量。</summary>
public sealed class UdpRequestBudget(TimeProvider clock, int perAddressLimit = 120, int totalLimit = 4096)
{
    private readonly Dictionary<IPAddress, int> _counts = new();

    private DateTimeOffset _resetAt = clock.GetUtcNow() + TimeSpan.FromSeconds(1);

    private int _total;

    public bool TryAccept(IPAddress source)
    {
        if (clock.GetUtcNow() >= _resetAt)
        {
            _counts.Clear();
            _total = 0;
            _resetAt = clock.GetUtcNow() + TimeSpan.FromSeconds(1);
        }

        if (_total >= totalLimit)
        {
            return false;
        }

        _counts.TryGetValue(source, out var count);
        if (count >= perAddressLimit)
        {
            return false;
        }

        _counts[source] = count + 1;
        _total++;
        return true;
    }
}
