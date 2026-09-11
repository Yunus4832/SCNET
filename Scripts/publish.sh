#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

echo "[SCNET Publish] Publishing Survivalcraft.Linux"
dotnet publish "Survivalcraft.Linux/Survivalcraft.Linux.csproj" --configuration "$configuration" "$@"

# Windows publishing is temporarily disabled.
# echo "[SCNET Publish] Publishing Survivalcraft.Windows"
# dotnet publish "Survivalcraft.Windows/Survivalcraft.Windows.csproj" --configuration "$configuration" "$@"

echo "[SCNET Publish] Publishing Survivalcraft.Android (Arm64)"
dotnet publish "Survivalcraft.Android/Survivalcraft.Android.csproj" --configuration "$configuration" "$@"

echo "[SCNET Publish] Publishing Survivalcraft.Android (Arm32)"
dotnet publish "Survivalcraft.Android.Arm32/Survivalcraft.Android.Arm32.csproj" --configuration "$configuration" "$@"

echo "[SCNET Publish] Publishing ContentServer"
dotnet publish "ContentServer/ContentServer.csproj" --configuration "$configuration" "$@"

echo "[SCNET Publish] All projects published successfully."
