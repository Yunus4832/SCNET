namespace ContentServer.Application.Commands;

public enum AuthenticationServiceMutationResult
{
    Completed = 0,

    NotFound = 1,

    Conflict = 2
}
