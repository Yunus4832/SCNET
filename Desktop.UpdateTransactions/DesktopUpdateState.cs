namespace Scnet.Desktop.Updates;

public enum DesktopUpdateState
{
    Prepared = 0,

    Applying = 1,

    Completed = 2,

    RolledBack = 3,

    RollbackFailed = 4
}
