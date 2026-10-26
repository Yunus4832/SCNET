namespace SCNET.Authentication.Protocol.Contracts;

public enum AuthenticationTransactionState
{
    Pending = 0,

    Completed = 1,

    Denied = 2,

    Cancelled = 3,

    Expired = 4
}
