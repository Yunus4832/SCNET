using System.Net;

namespace Engine.Network.Rendezvous;

public sealed class RendezvousException(
    string operation,
    RendezvousFailureKind kind,
    string errorCode,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null
)
    : IOException($"Rendezvous {operation} failed: {errorCode} ({kind}).", innerException)
{
    public string Operation { get; } = operation;

    public RendezvousFailureKind Kind { get; } = kind;

    public string ErrorCode { get; } = errorCode;

    public HttpStatusCode? StatusCode { get; } = statusCode;
}
