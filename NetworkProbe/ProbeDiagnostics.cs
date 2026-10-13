using System.Diagnostics.Tracing;
using System.Text.Json;

namespace NetworkProbe;

internal sealed class ProbeDiagnostics : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "SCNET-Engine-Network")
        {
            EnableEvents(eventSource, EventLevel.Verbose);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            Event = eventData.EventName,
            Timestamp = DateTimeOffset.UtcNow,
            Values = eventData.Payload
        }));
    }
}
