using System.Diagnostics;

namespace ServerLoadTool;

public sealed class ProcessProbe(int pid) : IDisposable
{
    private readonly Process _process = Process.GetProcessById(pid);
    private TimeSpan _previousCpu;
    private double _previousTime;

    public object Capture(double seconds)
    {
        _process.Refresh();
        var cpu = _process.TotalProcessorTime;
        var elapsed = seconds - _previousTime;
        var percent = _previousTime > 0 && elapsed > 0 ? (cpu - _previousCpu).TotalSeconds / elapsed * 100 : (double?)null;
        _previousCpu = cpu;
        _previousTime = seconds;
        return new
        {
            Pid = _process.Id,
            CpuOneCorePercent = percent,
            WorkingSetBytes = _process.WorkingSet64,
            Threads = _process.Threads.Count
        };
    }

    public void Dispose() => _process.Dispose();
}
