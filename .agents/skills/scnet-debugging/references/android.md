# Android ADB Debugging

Use Android as a client in a multiplayer lab. Prefer a desktop Headless or GUI server and one
Android device or emulator per concurrent Android client. A single installed package can switch
between data instances, but Android does not provide concurrent isolated game processes for those
instances.

## Prerequisites

Confirm ADB can see exactly which target will be controlled:

```bash
adb devices -l
adb -s <serial> get-state
```

Always include `-s <serial>` when more than one device or emulator is attached. Build only the
Android project when validating Android startup behavior:

Choose the project that matches the device ABI (`Survivalcraft.Android`,
`Survivalcraft.Android.X86`, or another repository Android variant). For Debug builds use the .NET
Android `Install` target so the current managed assemblies are deployed along with the APK:

```bash
dotnet build <android-project> -t:Install --no-restore
```

A plain `adb install -r` of a Debug APK can leave an older Fast Deployment `.__override__` assembly
set active. If logs show code older than the installed APK, inspect the app-private override through
`adb shell run-as com.candy.scnet`; do not clear application or SCNET data as a routine workaround.
Release APKs embed the managed assemblies and can be installed normally.

SCNET currently uses external shared storage rooted at `/storage/emulated/0/scnet`. On Android 11+
the first launch can stop at the all-files-access settings screen. Grant that access manually before
an automated run, or use the device's supported `appops` workflow when available. Do not report a
network timeout while the permission screen is still blocking startup.

## Start directly into a server

The exported entry activity is `com.candy.scnet/com.candy.scnet.MainActivity`. Android startup uses
two Intent extras:

- `Survivalcraft.Android.InstanceId` selects the isolated Starter instance;
- `Survivalcraft.Android.CommandLine` carries the ordinary game startup arguments.

SCNET game transport uses UDP, so `adb reverse` cannot carry the game connection. An Android Studio
emulator reaches the development host at `10.0.2.2`; a physical device must use a reachable host LAN
address. Start an emulator client with:

```bash
adb -s <serial> shell am force-stop com.candy.scnet
adb -s <serial> shell \
  "am start -W -n com.candy.scnet/com.candy.scnet.MainActivity \
  --es Survivalcraft.Android.InstanceId android-client \
  --es Survivalcraft.Android.CommandLine \
  '--gui --session android-client --connect 10.0.2.2:28987 --player AndroidPlayer --log-level Debug'"
```

The outer double quotes make ADB send one remote shell command, while the inner single quotes keep
the complete command line in one Intent Extra. Without this two-level quoting, the device shell can
misinterpret `--session` and later tokens as `am start` options.

These options use the same `RunningSettingManager` path as desktop startup. They are transient unless
the command line explicitly contains `--save`. Without `--player`, the client must return to the
normal player-selection screen. Use a unique instance, session, and player name for every test device.

Confirm the server is listening on a non-loopback interface and that the emulator or device can
reach the selected UDP port. Keep `adb forward`/`adb reverse` for TCP facilities such as the HTTP
command adapter, not for the game transport.

## Observe and preserve evidence

Capture platform and native failures with logcat while treating the instance game log as the
authoritative application record:

```bash
adb -s <serial> logcat -c
adb -s <serial> logcat --pid="$(adb -s <serial> shell pidof com.candy.scnet | tr -d '\r')"
adb -s <serial> pull \
  /storage/emulated/0/scnet/Instances/android-client/Logs \
  artifacts/android-client-logs
```

If the process dies before a PID-filtered logcat starts, collect a bounded unfiltered logcat and
search for the package name, `AndroidRuntime`, `libc`, and crash markers. Preserve the exact Intent
extras, device serial, package version, logcat, and pulled `Game*.log` in the run artifact directory.

To access an HTTP command adapter listening on the Android device, forward a host port to it:

```bash
adb -s <serial> forward tcp:<host-port> tcp:<device-command-port>
```

## Stop and clean up

Prefer the game's normal exit path when lifecycle behavior is under test. Otherwise stop only the
selected package and remove the port mappings created for this run:

```bash
adb -s <serial> shell am force-stop com.candy.scnet
adb -s <serial> forward --remove tcp:<host-port>
```

Do not delete `/storage/emulated/0/scnet` as cleanup. Record whether the selected named instance
existed before the run. After force-stopping the package and pulling required evidence, remove only
the exact disposable instance created by the current task unless it must be retained for diagnosis.
Never remove a pre-existing instance. Report the path and reason for every retained debug instance.
