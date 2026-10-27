[CmdletBinding()]
param(
    [switch]$Refresh,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $packages = @(
        @{ Id = "SCNET.Native.ImageCodecs"; Project = "Native.ImageCodecs/Native.ImageCodecs.csproj" },
        @{ Id = "SCNET.Native.OpenAL"; Project = "Native.OpenAL/Native.OpenAL.csproj" },
        @{ Id = "SCNET.Native.SDL"; Project = "Native.SDL/Native.SDL.csproj" },
        @{ Id = "SCNET.Engine.Core"; Project = "Engine.Core/Engine.Core.csproj" },
        @{ Id = "SCNET.Engine.Media"; Project = "Engine.Media/Engine.Media.csproj" },
        @{ Id = "SCNET.Engine.Serialization"; Project = "Engine.Serialization/Engine.Serialization.csproj" },
        @{ Id = "SCNET.Rendezvous.Protocol"; Project = "Rendezvous.Protocol/Rendezvous.Protocol.csproj" },
        @{ Id = "SCNET.Engine.Network"; Project = "Engine.Network/Engine.Network.csproj" },
        @{ Id = "SCNET.Engine"; Project = "Engine/Engine.csproj" },
        @{ Id = "SCNET.EntitySystem"; Project = "EntitySystem/EntitySystem.csproj" },
        @{ Id = "SCNET.Content.Packaging"; Project = "Content.Packaging/Content.Packaging.csproj" },
        @{ Id = "SCNET.ServerSource.Protocol"; Project = "ServerSource.Protocol/ServerSource.Protocol.csproj" },
        @{ Id = "SCNET.ContentTool"; Project = "ContentTool/ContentTool.csproj" },
        @{ Id = "SCNET.Authentication.Protocol"; Project = "Authentication.Protocol/Authentication.Protocol.csproj" },
        @{ Id = "SCNET.Game.Protocol"; Project = "Game.Protocol/Game.Protocol.csproj" },
        @{ Id = "SCNET.Survivalcraft"; Project = "Survivalcraft/Survivalcraft.csproj" },
        @{ Id = "SCNET.ModTemplates"; Project = "Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj" }
    )
    $needsPack = $Refresh.IsPresent
    foreach ($package in $packages) {
        $version = (& dotnet msbuild $package.Project -nologo -getProperty:PackageVersion).Trim()
        if ($LASTEXITCODE -ne 0) { throw "Could not read the package version for $($package.Id)." }
        if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$') { throw "Invalid package version: $version" }
        $package.Version = $version
        if (!(Test-Path "Publish/NuGet/$($package.Id).$version.nupkg" -PathType Leaf)) { $needsPack = $true }
    }
    if ($needsPack) {
        $cacheOutput = & dotnet nuget locals global-packages --list --force-english-output
        if ($LASTEXITCODE -ne 0) { throw "Could not locate the NuGet cache." }
        $cacheRoot = ($cacheOutput -replace '^global-packages: ', '').Trim()
        if (![IO.Path]::IsPathRooted($cacheRoot)) {
            throw "Invalid NuGet cache path: $cacheRoot"
        }
        foreach ($package in $packages) {
            $cacheDirectory = Join-Path $cacheRoot ($package.Id.ToLowerInvariant() + "/$($package.Version)")
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
