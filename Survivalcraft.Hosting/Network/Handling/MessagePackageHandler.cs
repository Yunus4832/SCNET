namespace Game.Network.Handling;

/// <summary>接收服务端生成的消息。客户端通过命令发起聊天请求。</summary>
public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    public override void Handle(MessagePackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (isServer ||
            netNode is null ||
            context.Project is not { } project)
        {
            return;
        }

        project.FindSubsystem<SubsystemMessages>(true)!.Service.Receive(package.GameMessage);
    }
}
