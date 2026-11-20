using System.Collections.Concurrent;
using System.Net;

using LiteNetLib;

using Rendezvous.Protocol;

using RendezvousServer.Sessions;

namespace RendezvousServer;

internal sealed class UdpSignalHost(
    InvitationRegistry registry,
    IConfiguration configuration,
    ILogger<UdpSignalHost> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var port = configuration.GetValue("Rendezvous:UdpPort", 31990);
        var inbox = new ConcurrentQueue<(byte[] Buffer, IPEndPoint RemoteEndPoint)>();
        var budget = new UdpRequestBudget(
            TimeProvider.System,
            configuration.GetValue("Rendezvous:UdpRequestsPerAddressPerSecond", 120),
            configuration.GetValue("Rendezvous:UdpRequestsPerSecond", 4096)
        );
        var listener = new EventBasedNetListener();
        listener.NetworkReceiveUnconnectedEvent += (endpoint, reader, _) =>
        {
            try
            {
                if (reader.AvailableBytes >= 6 &&
                    reader.AvailableBytes <= SignalCodec.MaxDatagramBytes &&
                    inbox.Count < 256 &&
                    budget.TryAccept(endpoint.Address))
                {
                    inbox.Enqueue((reader.GetRemainingBytes(), endpoint));
                }
            }
            finally
            {
                reader.Recycle();
            }
        };
        var network = new NetManager(listener) { UnconnectedMessagesEnabled = true };
        if (!network.Start(port))
        {
            throw new InvalidOperationException("Unable to bind rendezvous UDP port.");
        }

        var endpoints = new EndpointRegistrations(
            registry,
            TimeProvider.System,
            configuration.GetValue("Rendezvous:RegistrationLifetimeSeconds", 60)
        );
        Console.WriteLine($"Rendezvous UDP listening on {port}");
        var nextCleanup = DateTimeOffset.UtcNow;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                network.PollEvents();
                if (DateTimeOffset.UtcNow >= nextCleanup)
                {
                    endpoints.CleanExpired();
                    nextCleanup = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                }

                if (!inbox.TryDequeue(out var received))
                {
                    await Task.Delay(5, cancellationToken);
                    continue;
                }

                if (received.Buffer.Length > SignalCodec.MaxDatagramBytes)
                {
                    continue;
                }

                if (!SignalCodec.TryDecode(received.Buffer, out var request) || request is null)
                {
                    continue;
                }

                SignalMessage response;
                try
                {
                    switch (request.Operation)
                    {
                        case "register":
                            var challenge = endpoints.Challenge(
                                request.SessionId,
                                request.ParticipantId,
                                request.Ticket,
                                received.RemoteEndPoint,
                                request.LocalEndpoints
                            );
                            response = new SignalMessage(
                                "challenge",
                                request.RequestId,
                                request.SessionId,
                                request.ParticipantId,
                                Challenge: challenge
                            );
                            break;
                        case "confirm":
                            if (!endpoints.Confirm(
                                    request.SessionId,
                                    request.ParticipantId,
                                    request.Ticket,
                                    request.Challenge,
                                    received.RemoteEndPoint
                                ))
                            {
                                throw new InvalidOperationException("registration_unauthorized");
                            }

                            response = new SignalMessage(
                                "registered",
                                request.RequestId,
                                request.SessionId,
                                request.ParticipantId,
                                Endpoint: received.RemoteEndPoint.ToString()
                            );
                            break;
                        case "exchange":
                            if (request.ParticipantId == Guid.Empty ||
                                !registry.AuthenticateTicket(
                                    request.SessionId,
                                    request.ParticipantId,
                                    request.Ticket
                                ) ||
                                !Equals(
                                    endpoints.Find(
                                        request.SessionId,
                                        request.ParticipantId,
                                        received.RemoteEndPoint.AddressFamily
                                    ),
                                    received.RemoteEndPoint
                                ))
                            {
                                throw new InvalidOperationException("invitation_unauthorized");
                            }

                            var hostEndpoint = endpoints.Find(
                                request.SessionId,
                                Guid.Empty,
                                received.RemoteEndPoint.AddressFamily
                            );
                            if (hostEndpoint is null)
                            {
                                throw new InvalidOperationException("host_not_registered");
                            }

                            var candidateChallenge = endpoints.ExchangeChallenge(
                                request.SessionId,
                                request.ParticipantId,
                                request.RequestId,
                                received.RemoteEndPoint,
                                hostEndpoint
                            );
                            var introduction = new SignalMessage(
                                "candidate",
                                request.RequestId,
                                request.SessionId,
                                request.ParticipantId,
                                Endpoint: received.RemoteEndPoint.ToString(),
                                Challenge: candidateChallenge,
                                LocalEndpoints: endpoints.FindLocalEndpoints(
                                    request.SessionId,
                                    request.ParticipantId,
                                    hostEndpoint.AddressFamily
                                )
                            );
                            network.SendUnconnectedMessage(SignalCodec.Encode(introduction), hostEndpoint);
                            response = new SignalMessage(
                                "candidate",
                                request.RequestId,
                                request.SessionId,
                                request.ParticipantId,
                                Endpoint: hostEndpoint.ToString(),
                                Challenge: candidateChallenge,
                                LocalEndpoints: endpoints.FindLocalEndpoints(
                                    request.SessionId,
                                    Guid.Empty,
                                    hostEndpoint.AddressFamily
                                )
                            );
                            break;
                        default:
                            throw new InvalidOperationException("unknown_operation");
                    }
                }
                catch (InvalidOperationException exception)
                {
                    response = new SignalMessage("error", request.RequestId, Error: exception.Message);
                }
                catch (ArgumentOutOfRangeException)
                {
                    response = new SignalMessage("error", request.RequestId, Error: "invalid_capacity");
                }

                logger.LogDebug(
                    "UDP signaling {Operation}: session {SessionId}, participant {ParticipantId}, attempt {AttemptId}, result {Result}, error {ErrorCode}",
                    request.Operation,
                    request.SessionId,
                    request.ParticipantId,
                    request.RequestId,
                    response.Operation,
                    response.Error
                );
                network.SendUnconnectedMessage(SignalCodec.Encode(response), received.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            network.Stop();
        }
    }
}
