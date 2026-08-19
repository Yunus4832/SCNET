# Windows Debugging

Run PowerShell from the repository root. Use a distinct `--instance` for every process and never
debug against the default instance.

## Build and paths

```powershell
dotnet build .\Survivalcraft.Windows\Survivalcraft.Windows.csproj --no-restore
$Starter = (Resolve-Path .\Survivalcraft.Windows\bin\Debug\net10.0\win-x64\SurvivalcraftStarter.exe).Path
$Output = Split-Path $Starter
```

Instance data is stored below `$Output\Instances\<instance>`. Use the instance log as the authoritative
source for startup, networking, commands, exceptions, saving, and shutdown. Do not depend on capturing
the separate Headless console output.

Resolve the newest log for an instance after starting its process:

```powershell
$Logs = Join-Path $Output 'Instances\debug-server\Logs'
$Log = Get-ChildItem $Logs -Filter 'Game*.log' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
Get-Content $Log.FullName
Get-Content $Log.FullName -Tail 0 -Wait
```

If the log directory or file does not exist yet, poll briefly until the process creates it. Always use
`Get-ChildItem` instead of constructing today's filename because a run can cross midnight.

For an automated readiness check, poll the complete current log so an already-written marker is not missed:

```powershell
$Deadline = (Get-Date).AddSeconds(60)
$Ready = $false
while ((Get-Date) -lt $Deadline) {
    $Log = Get-ChildItem $Logs -Filter 'Game*.log' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($Log -and (Select-String -Path $Log.FullName -SimpleMatch 'Headless server started' -Quiet)) {
        $Ready = $true
        break
    }
    Start-Sleep -Milliseconds 250
}
if (-not $Ready) { throw 'Headless readiness marker was not observed before timeout.' }
```

## Headless server

```powershell
$Server = Start-Process -FilePath $Starter -PassThru -ArgumentList @(
    '--instance', 'debug-server',
    '--server',
    '--session', 'debug-server',
    '--world', 'DebugWorld',
    '--server-port', '28987',
    '--broadcast-port', '28988',
    '--log-level', 'Debug'
)
```

The Windows Starter allocates a separate console for Headless mode. Observe it through the runtime log
and require `Headless server started. Press Ctrl+C to stop.` before claiming readiness. Use the separate
console only to enter commands such as `permission players` or `stop`.

PowerShell redirection is not equivalent to the Linux PTY/FIFO helper because the process calls
`AllocConsole()`. Do not claim automated stdin control unless it has been independently verified.

## GUI server and clients

Create or start a GUI-hosted server with its local player and transient Session ports. The debugging
`--host` override forces and persists `WorldSettings.RunServer=true` for new or existing worlds:

```powershell
$GuiServer = Start-Process -FilePath $Starter -PassThru -ArgumentList @(
    '--instance', 'debug-gui-server',
    '--gui', '--host',
    '--session', 'debug-gui-server',
    '--world', 'DebugGuiWorld',
    '--player', 'DebugHost',
    '--server-port', '29987',
    '--broadcast-port', '29988',
    '--log-level', 'Debug'
)
```

Start each client with another instance and identity:

```powershell
$ClientA = Start-Process -FilePath $Starter -PassThru -ArgumentList @(
    '--instance', 'debug-client-a',
    '--gui',
    '--session', 'debug-client-a',
    '--connect', '127.0.0.1:29987',
    '--player', 'ClientA',
    '--log-level', 'Debug'
)
```

Resolve and read each instance's own newest log. Repeat with `debug-client-b` and `ClientB` for a second
client. Verify all of these in logs:

- the server reports `开启服务器成功` on the requested port;
- every process reaches `Entered screen "Game"`;
- the server accepts each connection and reports `已完成加入过程`;
- the expected player names report `加入游戏`.

## Process and evidence handling

Keep the `Process` objects returned by `Start-Process`; do not kill by a broad image-name pattern.
Inspect exact processes when recovering a lost handle:

```powershell
Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'SurvivalcraftStarter.exe' } |
    Select-Object ProcessId, CommandLine
```

Close GUI windows normally and enter `stop` in a Headless console. If graceful control is unavailable,
preserve logs first, then stop only the verified PID and report that shutdown was forced:

```powershell
Stop-Process -Id $ClientA.Id
```

Search the complete logs with PowerShell when `rg` is unavailable:

```powershell
Select-String -Path $Log.FullName -Pattern 'ERROR:|Unhandled exception|COMMAND ERROR|disconnect|连接关闭' -Context 8,8
```

Preserve the exact command lines, PIDs, logs, readiness markers, stop method, and exit codes according
to [evidence.md](evidence.md).

Before each run, record whether `$Output\Instances\<instance>` already exists. After all associated
processes have stopped and required logs have been copied, remove a newly created successful-run
instance with an exact validated path unless it is still needed for reproduction:

```powershell
$InstancePath = Join-Path $Output 'Instances\debug-server'
Remove-Item -LiteralPath $InstancePath -Recurse -Force
```

Never remove a pre-existing instance. Preserve failed-run instances by default and report their path;
delete them once diagnosis no longer needs their state.
