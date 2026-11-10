#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
refresh=false
for argument in "$@"; do
    case "$argument" in
        --refresh) refresh=true ;;
        --help|-h) echo "Usage: Scripts/pre-build.sh [--refresh]"; exit 0 ;;
        *) echo "Unknown argument: $argument" >&2; exit 1 ;;
    esac
done
cd "$repository_root"
mkdir -p Publish/NuGet
packages=(
    "SCNET.Native.ImageCodecs|Native.ImageCodecs/Native.ImageCodecs.csproj"
    "SCNET.Native.OpenAL|Native.OpenAL/Native.OpenAL.csproj"
    "SCNET.Native.SDL|Native.SDL/Native.SDL.csproj"
    "SCNET.Engine.Core|Engine.Core/Engine.Core.csproj"
    "SCNET.Engine.Media|Engine.Media/Engine.Media.csproj"
    "SCNET.Rendezvous.Protocol|Rendezvous.Protocol/Rendezvous.Protocol.csproj"
    "SCNET.Engine.Network|Engine.Network/Engine.Network.csproj"
    "SCNET.Engine|Engine/Engine.csproj"
    "SCNET.EntitySystem|EntitySystem/EntitySystem.csproj"
    "SCNET.Content.Packaging|Content.Packaging/Content.Packaging.csproj"
    "SCNET.ServerSource.Protocol|ServerSource.Protocol/ServerSource.Protocol.csproj"
    "SCNET.ContentTool|ContentTool/ContentTool.csproj"
    "SCNET.Authentication.Protocol|Authentication.Protocol/Authentication.Protocol.csproj"
    "SCNET.Game.Protocol|Game.Protocol/Game.Protocol.csproj"
    "SCNET.Survivalcraft.Core|Survivalcraft.Core/Survivalcraft.Core.csproj"
    "SCNET.Survivalcraft.Hosting|Survivalcraft.Hosting/Survivalcraft.Hosting.csproj"
    "SCNET.Survivalcraft.Client|Survivalcraft.Client/Survivalcraft.Client.csproj"
    "SCNET.Survivalcraft|Survivalcraft/Survivalcraft.csproj"
    "SCNET.ModTemplates|Survivalcraft.ModTemplate/Survivalcraft.ModTemplate.csproj"
)
versions=()
needs_pack="$refresh"
for entry in "${packages[@]}"; do
    package_id="${entry%%|*}"
    project="${entry#*|}"
    version="$(dotnet msbuild "$project" -nologo -getProperty:PackageVersion)"
    [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$ ]] || { echo "Invalid package version: $version" >&2; exit 1; }
    versions+=("$version")
    [[ -f "Publish/NuGet/$package_id.$version.nupkg" ]] || needs_pack=true
done
if [[ "$needs_pack" == true ]]; then
    # Repacking an unchanged version must not reuse an older SDK from the global cache.
    cache_root="$(dotnet nuget locals global-packages --list --force-english-output)"
    cache_root="${cache_root#global-packages: }"
    [[ "$cache_root" == /* ]] || { echo "Invalid NuGet cache path: $cache_root" >&2; exit 1; }
    clear_prepared_package_cache() {
        for index in "${!packages[@]}"; do
            package_id="${packages[$index]%%|*}"
            package_id="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
            rm -rf -- "$cache_root/$package_id/${versions[$index]}"
        done
    }
    clear_prepared_package_cache
    bash Scripts/pack-nuget.sh
    # Restore during packing can rehydrate a package before its replacement is written.
    clear_prepared_package_cache
fi
dotnet restore SCNET.slnx --force
echo "[SCNET Pre-build] Dependencies are ready. Run dotnet build or build in your IDE."
