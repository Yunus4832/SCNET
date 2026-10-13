namespace Rendezvous.Protocol;

/// <summary>A short-lived capability for registering one participant's transport endpoint.</summary>
public sealed record RegistrationTicket(string Token, DateTimeOffset ExpiresAt);
