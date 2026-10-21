[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidateSet("Native.ImageCodecs", "Native.OpenAL", "Engine.Core", "Engine.Serialization", "Engine.Media", "Rendezvous.Protocol", "Engine.Network", "Engine", "EntitySystem", "Content.Packaging", "ServerSource.Protocol", "ContentTool", "Game.Protocol", "Survivalcraft", "Survivalcraft.ModTemplates")]
    [string[]]$Project = @(),
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArguments
)

$ErrorActionPreference = "Stop"

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    if ($Project.Count -eq 0 -or $Project -contains "Native.ImageCodecs") {
        Write-Host "[SCNET Pack] Packing SCNET.Native.ImageCodecs"
        & dotnet pack "Native.ImageCodecs/Native.ImageCodecs.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Native.ImageCodecs failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Native.OpenAL") {
        Write-Host "[SCNET Pack] Packing SCNET.Native.OpenAL"
        & dotnet pack "Native.OpenAL/Native.OpenAL.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Native.OpenAL failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Engine.Core") {
        Write-Host "[SCNET Pack] Packing SCNET.Engine.Core"
        & dotnet pack "Engine.Core/Engine.Core.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Core failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Engine.Serialization") {
        Write-Host "[SCNET Pack] Packing SCNET.Engine.Serialization"
        & dotnet pack "Engine.Serialization/Engine.Serialization.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Serialization failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Engine.Media") {
        Write-Host "[SCNET Pack] Packing SCNET.Engine.Media"
        & dotnet pack "Engine.Media/Engine.Media.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Media failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Rendezvous.Protocol") {
        Write-Host "[SCNET Pack] Packing SCNET.Rendezvous.Protocol"
        & dotnet pack "Rendezvous.Protocol/Rendezvous.Protocol.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Rendezvous.Protocol failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Engine.Network") {
        Write-Host "[SCNET Pack] Packing SCNET.Engine.Network"
        & dotnet pack "Engine.Network/Engine.Network.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine.Network failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Engine") {
        Write-Host "[SCNET Pack] Packing SCNET.Engine"
        & dotnet pack "Engine/Engine.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Engine failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "EntitySystem") {
        Write-Host "[SCNET Pack] Packing SCNET.EntitySystem"
        & dotnet pack "EntitySystem/EntitySystem.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.EntitySystem failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Content.Packaging") {
        Write-Host "[SCNET Pack] Packing SCNET.Content.Packaging"
        & dotnet pack "Content.Packaging/Content.Packaging.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Content.Packaging failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "ServerSource.Protocol") {
        Write-Host "[SCNET Pack] Packing SCNET.ServerSource.Protocol"
        & dotnet pack "ServerSource.Protocol/ServerSource.Protocol.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.ServerSource.Protocol failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "ContentTool") {
        Write-Host "[SCNET Pack] Packing SCNET.ContentTool"
        & dotnet pack "ContentTool/ContentTool.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.ContentTool failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Game.Protocol") {
        Write-Host "[SCNET Pack] Packing SCNET.Game.Protocol"
        & dotnet pack "Game.Protocol/Game.Protocol.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Game.Protocol failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Survivalcraft") {
        Write-Host "[SCNET Pack] Packing SCNET.Survivalcraft"
        & dotnet pack "Survivalcraft/Survivalcraft.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.Survivalcraft failed with exit code $LASTEXITCODE." }
    }

    if ($Project.Count -eq 0 -or $Project -contains "Survivalcraft.ModTemplates") {
        Write-Host "[SCNET Pack] Packing SCNET.ModTemplates"
        & dotnet pack "Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj" --configuration $Configuration @AdditionalArguments
        if ($LASTEXITCODE -ne 0) { throw "Packing SCNET.ModTemplates failed with exit code $LASTEXITCODE." }
    }
}
finally {
    Pop-Location
}

Write-Host "[SCNET Pack] Requested NuGet packages created successfully."
