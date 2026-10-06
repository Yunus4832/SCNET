namespace Game.Network;

internal sealed class NetworkMessageFanout(NetworkSendStatistics statistics, Type packageType, NetworkChannel channel)
{
    private readonly object _gate = new();
    private Client? _firstRecipient;
    private HashSet<Client>? _otherRecipients;
    private int _pending = 1;

    public void DeliveredTo(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_gate)
        {
            if (_pending == 0)
            {
                throw new InvalidOperationException("The message has already completed.");
            }

            if (_firstRecipient == null)
            {
                _firstRecipient = client;
            }
            else if (!ReferenceEquals(_firstRecipient, client))
            {
                _otherRecipients ??= new HashSet<Client>(ReferenceEqualityComparer.Instance);
                _otherRecipients.Add(client);
            }
        }
    }

    public void Advance(int consumed, int deferred)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(consumed, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(deferred);
        lock (_gate)
        {
            if (_pending == 0 || consumed > _pending)
            {
                throw new InvalidOperationException("Invalid message continuation count.");
            }

            _pending = checked(_pending - consumed + deferred);
            if (_pending == 0)
            {
                statistics.RecordFanout(packageType, channel,
                    (_firstRecipient == null ? 0 : 1) + (_otherRecipients?.Count ?? 0));
            }
        }
    }
}
