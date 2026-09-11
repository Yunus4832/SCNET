[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArguments
)

$ErrorActionPreference = "Stop"

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    Write-Host "[SCNET Publish] Publishing Survivalcraft.Linux"
    & dotnet publish "Survivalcraft.Linux/Survivalcraft.Linux.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing Survivalcraft.Linux failed with exit code $LASTEXITCODE."
    }

    # Windows publishing is temporarily disabled.
    # Write-Host "[SCNET Publish] Publishing Survivalcraft.Windows"
    # & dotnet publish "Survivalcraft.Windows/Survivalcraft.Windows.csproj" --configuration $Configuration @AdditionalArguments
    # if ($LASTEXITCODE -ne 0) {
    #     throw "Publishing Survivalcraft.Windows failed with exit code $LASTEXITCODE."
    # }

    Write-Host "[SCNET Publish] Publishing Survivalcraft.Android (Arm64)"
    & dotnet publish "Survivalcraft.Android/Survivalcraft.Android.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing Survivalcraft.Android (Arm64) failed with exit code $LASTEXITCODE."
    }

    Write-Host "[SCNET Publish] Publishing Survivalcraft.Android (Arm32)"
    & dotnet publish "Survivalcraft.Android.Arm32/Survivalcraft.Android.Arm32.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing Survivalcraft.Android (Arm32) failed with exit code $LASTEXITCODE."
    }

    Write-Host "[SCNET Publish] Publishing ContentServer"
    & dotnet publish "ContentServer/ContentServer.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing ContentServer failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host "[SCNET Publish] All projects published successfully."
