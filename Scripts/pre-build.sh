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
version="$(dotnet msbuild Native.ImageCodecs/Native.ImageCodecs.csproj -nologo -getProperty:PackageVersion)"
packages=(Native.ImageCodecs Native.OpenAL.Android Engine.Core Engine.Media Engine.Serialization
    Rendezvous.Protocol Engine.Network Engine EntitySystem Content.Packaging ServerSource.Protocol
    ContentTool Survivalcraft ModTemplates)
needs_pack="$refresh"
for package in "${packages[@]}"; do
    [[ -f "Publish/NuGet/SCNET.$package.$version.nupkg" ]] || needs_pack=true
done
if [[ "$needs_pack" == true ]]; then
    # Repacking an unchanged version must not reuse an older SDK from the global cache.
    cache_root="$(dotnet nuget locals global-packages --list --force-english-output)"
    cache_root="${cache_root#global-packages: }"
    [[ "$cache_root" == /* ]] || { echo "Invalid NuGet cache path: $cache_root" >&2; exit 1; }
    [[ "$version" =~ ^[0-9]+(\.[0-9]+){1,3}(-[A-Za-z0-9.-]+)?$ ]] || { echo "Invalid package version: $version" >&2; exit 1; }
    for package in "${packages[@]}"; do
        package_id="$(printf 'SCNET.%s' "$package" | tr '[:upper:]' '[:lower:]')"
        rm -rf -- "$cache_root/$package_id/$version"
    done
    bash Scripts/pack-nuget.sh
fi
dotnet restore SCNET.slnx --force
echo "[SCNET Pre-build] Dependencies are ready. Run dotnet build or build in your IDE."
