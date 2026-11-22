using System.Diagnostics.Tracing;

namespace Engine.Network.Diagnostics;

/// <summary>可选的传输诊断，不输出凭据或数据包内容。</summary>
[EventSource(Name = "SCNET-Engine-Network")]
internal sealed class NetworkDiagnostics : EventSource
{
    internal static readonly NetworkDiagnostics log = new();

    [Event(1, Level = EventLevel.Verbose)]
    public void RelayPacket(string endpoint, string route, string outcome, int bytes)
    {
        if (IsEnabled())
        {
            WriteEvent(1, endpoint, route, outcome, bytes);
        }
    }
}
