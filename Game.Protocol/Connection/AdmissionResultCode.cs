namespace Game.Protocol.Connection;

public enum AdmissionResultCode
{
    Accepted = 0,

    IncorrectPassword = 1,

    ApprovalRejected = 2,

    Expired = 3,

    RateLimited = 4,

    Cancelled = 5,

    Busy = 6,

    Denied = 7
}
