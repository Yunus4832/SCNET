namespace Game.Authentication;

public enum AuthenticationServiceInstallationStatus
{
    Available = 0,

    Installed = 1,

    Disabled = 2,

    EndpointChange = 3,

    KeyConflict = 4
}
