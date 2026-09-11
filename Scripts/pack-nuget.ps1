[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArguments
)

$ErrorActionPreference = "Stop"

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    Write-Host "[SCNET Pack] Packing SCNET.Engine.Core"
    & dotnet pack "Engine.Core/Engine.Core.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Core failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.Engine.Serialization"
    & dotnet pack "Engine.Serialization/Engine.Serialization.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Serialization failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.Engine"
    & dotnet pack "Engine/Engine.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.EntitySystem"
    & dotnet pack "EntitySystem/EntitySystem.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.EntitySystem failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.Content.Packaging"
    & dotnet pack "Content.Packaging/Content.Packaging.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Content.Packaging failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.ContentTool"
    & dotnet pack "ContentTool/ContentTool.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.ContentTool failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.Survivalcraft"
    & dotnet pack "Survivalcraft/Survivalcraft.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Survivalcraft failed with exit code $LASTEXITCODE." }

    Write-Host "[SCNET Pack] Packing SCNET.ModTemplates"
    & dotnet pack "Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj" --configuration $Configuration @AdditionalArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.ModTemplates failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

Write-Host "[SCNET Pack] All NuGet packages created successfully."
