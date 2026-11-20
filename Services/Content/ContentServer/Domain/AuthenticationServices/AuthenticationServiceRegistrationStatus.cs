namespace ContentServer.Domain.AuthenticationServices;

public enum AuthenticationServiceRegistrationStatus
{
    Pending = 0,

    Active = 1,

    Rejected = 2,

    Withdrawn = 3,

    Disabled = 4
}
