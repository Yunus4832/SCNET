namespace SCNET.Authentication.Protocol;

/// <summary>Safe protocol failure text; never embeds credentials or untrusted token content.</summary>
public class AuthenticationProtocolException(string message) : Exception(message);
