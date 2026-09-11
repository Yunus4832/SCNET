#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

echo "[SCNET Pack] Packing SCNET.Engine.Core"
dotnet pack "Engine.Core/Engine.Core.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.Engine.Serialization"
dotnet pack "Engine.Serialization/Engine.Serialization.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.Engine"
dotnet pack "Engine/Engine.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.EntitySystem"
dotnet pack "EntitySystem/EntitySystem.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.Content.Packaging"
dotnet pack "Content.Packaging/Content.Packaging.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.ContentTool"
dotnet pack "ContentTool/ContentTool.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.Survivalcraft"
dotnet pack "Survivalcraft/Survivalcraft.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] Packing SCNET.ModTemplates"
dotnet pack "Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj" --configuration "$configuration" "$@"

echo "[SCNET Pack] All NuGet packages created successfully."
