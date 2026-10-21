using System.Net.Sockets;

namespace Engine.Network;

/// <summary>Selects a confirmed candidate only after all losing preparations have ended.</summary>
internal static class CandidatePreparationRace
{
    internal static async Task<ConnectionPreparation> SelectAsync(
        IReadOnlyList<Func<CancellationToken, Task<ConnectionPreparation>>> preparations,
        Action<int> discardRegistration,
        CancellationToken cancellationToken
    )
    {
        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var attempts = preparations.Select(async prepare => await prepare(race.Token).ConfigureAwait(false)).ToArray();
        var pending = attempts.ToList();
        ConnectionPreparation? selected = null;
        Task<ConnectionPreparation>? selectedAttempt = null;
        Exception? failure = null;
        var selectedReleased = false;
        try
        {
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(completed);
                try
                {
                    selected = await completed.ConfigureAwait(false);
                    selectedAttempt = completed;
                    cancellationToken.ThrowIfCancellationRequested();
                    break;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                                  exception is RendezvousException
                                                  {
                                                      Kind: RendezvousFailureKind.UdpUnavailable
                                                      or RendezvousFailureKind.HolePunchUnavailable
                                                  } or SocketException)
                {
                    failure = exception is SocketException
                        ? new RendezvousException(
                            "candidate",
                            RendezvousFailureKind.UdpUnavailable,
                            "candidate_socket_error",
                            innerException: exception
                        )
                        : exception;
                }
            }

            if (selected is null)
            {
                throw failure ?? new InvalidOperationException("No candidate preparation was started.");
            }
        }
        finally
        {
            await race.CancelAsync().ConfigureAwait(false);
            foreach (var attempt in attempts)
            {
                try
                {
                    var prepared = await attempt.ConfigureAwait(false);
                    if (!ReferenceEquals(prepared, selected) || cancellationToken.IsCancellationRequested)
                    {
                        await prepared.DisposeAsync().ConfigureAwait(false);
                        selectedReleased |= ReferenceEquals(prepared, selected);
                    }
                }
                catch (Exception) when (attempt.IsFaulted || attempt.IsCanceled)
                {
                    // All attempt outcomes are observed; the selected failure is reported by the race.
                }
                finally
                {
                    if (attempt != selectedAttempt || cancellationToken.IsCancellationRequested)
                    {
                        discardRegistration(Array.IndexOf(attempts, attempt));
                    }
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await selected.DisposeAsync().ConfigureAwait(false);
            if (!selectedReleased)
            {
                discardRegistration(Array.IndexOf(attempts, selectedAttempt));
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return selected;
    }
}
