[CmdletBinding()]
param(
    [switch]$Refresh,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $version = (& dotnet msbuild Native.ImageCodecs/Native.ImageCodecs.csproj -nologo -getProperty:PackageVersion).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Could not read the package version." }
    $packages = @("Native.ImageCodecs", "Native.OpenAL.Android", "Engine.Core", "Engine.Media",
        "Engine.Serialization", "Rendezvous.Protocol", "Engine.Network", "Engine", "EntitySystem",
        "Content.Packaging", "ServerSource.Protocol", "ContentTool", "Survivalcraft", "ModTemplates")
    $needsPack = $Refresh.IsPresent
    foreach ($package in $packages) {
        if (!(Test-Path "Publish/NuGet/SCNET.$package.$version.nupkg" -PathType Leaf)) { $needsPack = $true }
    }
    if ($needsPack) {
        $cacheOutput = & dotnet nuget locals global-packages --list --force-english-output
        if ($LASTEXITCODE -ne 0) { throw "Could not locate the NuGet cache." }
        $cacheRoot = ($cacheOutput -replace '^global-packages: ', '').Trim()
        if (![IO.Path]::IsPathRooted($cacheRoot)) {
            throw "Invalid NuGet cache path: $cacheRoot"
        }
        if ($version -notmatch '^[0-9]+(\.[0-9]+){1,3}(-[A-Za-z0-9.-]+)?$') { throw "Invalid package version: $version" }
        foreach ($package in $packages) {
            $cacheDirectory = Join-Path $cacheRoot ("scnet.$package".ToLowerInvariant() + "/$version")
            if (Test-Path -LiteralPath $cacheDirectory) { Remove-Item -LiteralPath $cacheDirectory -Recurse -Force }
        }
        & "$PSScriptRoot/pack-nuget.ps1" -Configuration $Configuration
    }
    & dotnet restore SCNET.slnx --force
    if ($LASTEXITCODE -ne 0) { throw "Solution restore failed with exit code $LASTEXITCODE." }
    Write-Host "[SCNET Pre-build] Dependencies are ready. Run dotnet build or build in your IDE."
}
finally {
    Pop-Location
}
