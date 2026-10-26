namespace SCNET.Authentication.Protocol.Contracts;

public enum AuthenticationError
{
    None = 0,

    InvalidRequest = 1,

    InvalidProof = 2,

    Expired = 3,

    Denied = 4,

    SubjectBanned = 5,

    GrantExpired = 6,

    GrantRevoked = 7,

    Cancelled = 8,

    NotFound = 9,

    RateLimited = 10,

    Unavailable = 11
}
