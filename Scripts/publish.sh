#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
selected=false
publish_linux=false
publish_windows=false
publish_android_arm64=false
publish_android_arm32=false
publish_content_server=false
additional_arguments=()

select_project() {
    selected=true
    case "$1" in
        Survivalcraft.Linux) publish_linux=true ;;
        Survivalcraft.Windows) publish_windows=true ;;
        Survivalcraft.Android) publish_android_arm64=true ;;
        Survivalcraft.Android.Arm32) publish_android_arm32=true ;;
        ContentServer) publish_content_server=true ;;
        *) echo "Unknown project: $1" >&2; exit 2 ;;
    esac
}

while (($#)); do
    case "$1" in
        --project)
            if (($# < 2)); then
                echo "--project requires a project name." >&2
                exit 2
            fi
            select_project "$2"
            shift 2
            ;;
        --project=*) select_project "${1#*=}"; shift ;;
        --)
            shift
            additional_arguments+=("$@")
            break
            ;;
        *) additional_arguments+=("$1"); shift ;;
    esac
done

cd "$repository_root"

if ! $selected || $publish_linux; then
    echo "[SCNET Publish] Publishing Survivalcraft.Linux"
    dotnet publish "Survivalcraft.Linux/Survivalcraft.Linux.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $publish_windows; then
    echo "[SCNET Publish] Publishing Survivalcraft.Windows"
    dotnet publish "Survivalcraft.Windows/Survivalcraft.Windows.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $publish_android_arm64; then
    echo "[SCNET Publish] Publishing Survivalcraft.Android (Arm64)"
    dotnet publish "Survivalcraft.Android/Survivalcraft.Android.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $publish_android_arm32; then
    echo "[SCNET Publish] Publishing Survivalcraft.Android (Arm32)"
    dotnet publish "Survivalcraft.Android.Arm32/Survivalcraft.Android.Arm32.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $publish_content_server; then
    echo "[SCNET Publish] Publishing ContentServer"
    dotnet publish "ContentServer/ContentServer.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

echo "[SCNET Publish] Requested projects published successfully."
