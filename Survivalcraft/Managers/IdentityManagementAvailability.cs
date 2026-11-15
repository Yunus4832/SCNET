namespace Game.Managers;

public static class IdentityManagementAvailability
{
    public static bool CanManage => GameManager.ClientProject is null &&
                                    GameManager.Runtime?.AuthorityExecution is null &&
                                    !Network.CommonLib.Net.IsTransportRunning &&
                                    Network.CommonLib.Net.CurrentStage == Network.NetNode.Stage.NotConnected;
}
