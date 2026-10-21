#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

selected=false
pack_native_imagecodecs=false
pack_native_openal=false
pack_engine_core=false
pack_engine_serialization=false
pack_engine_media=false
pack_rendezvous_protocol=false
pack_engine_network=false
pack_engine=false
pack_entitysystem=false
pack_content_packaging=false
pack_serversource_protocol=false
pack_contenttool=false
pack_game_protocol=false
pack_survivalcraft=false
pack_survivalcraft_modtemplates=false
additional_arguments=()

select_project() {
    selected=true
    case "$1" in
        Native.ImageCodecs) pack_native_imagecodecs=true ;;
        Native.OpenAL) pack_native_openal=true ;;
        Engine.Core) pack_engine_core=true ;;
        Engine.Serialization) pack_engine_serialization=true ;;
        Engine.Media) pack_engine_media=true ;;
        Rendezvous.Protocol) pack_rendezvous_protocol=true ;;
        Engine.Network) pack_engine_network=true ;;
        Engine) pack_engine=true ;;
        EntitySystem) pack_entitysystem=true ;;
        Content.Packaging) pack_content_packaging=true ;;
        ServerSource.Protocol) pack_serversource_protocol=true ;;
        ContentTool) pack_contenttool=true ;;
        Game.Protocol) pack_game_protocol=true ;;
        Survivalcraft) pack_survivalcraft=true ;;
        Survivalcraft.ModTemplates) pack_survivalcraft_modtemplates=true ;;
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

if ! $selected || $pack_native_imagecodecs; then
    echo "[SCNET Pack] Packing SCNET.Native.ImageCodecs"
    dotnet pack "Native.ImageCodecs/Native.ImageCodecs.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_native_openal; then
    echo "[SCNET Pack] Packing SCNET.Native.OpenAL"
    dotnet pack "Native.OpenAL/Native.OpenAL.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_engine_core; then
    echo "[SCNET Pack] Packing SCNET.Engine.Core"
    dotnet pack "Engine.Core/Engine.Core.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_engine_serialization; then
    echo "[SCNET Pack] Packing SCNET.Engine.Serialization"
    dotnet pack "Engine.Serialization/Engine.Serialization.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_engine_media; then
    echo "[SCNET Pack] Packing SCNET.Engine.Media"
    dotnet pack "Engine.Media/Engine.Media.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_rendezvous_protocol; then
    echo "[SCNET Pack] Packing SCNET.Rendezvous.Protocol"
    dotnet pack "Rendezvous.Protocol/Rendezvous.Protocol.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_engine_network; then
    echo "[SCNET Pack] Packing SCNET.Engine.Network"
    dotnet pack "Engine.Network/Engine.Network.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_engine; then
    echo "[SCNET Pack] Packing SCNET.Engine"
    dotnet pack "Engine/Engine.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_entitysystem; then
    echo "[SCNET Pack] Packing SCNET.EntitySystem"
    dotnet pack "EntitySystem/EntitySystem.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_content_packaging; then
    echo "[SCNET Pack] Packing SCNET.Content.Packaging"
    dotnet pack "Content.Packaging/Content.Packaging.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_serversource_protocol; then
    echo "[SCNET Pack] Packing SCNET.ServerSource.Protocol"
    dotnet pack "ServerSource.Protocol/ServerSource.Protocol.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_contenttool; then
    echo "[SCNET Pack] Packing SCNET.ContentTool"
    dotnet pack "ContentTool/ContentTool.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_game_protocol; then
    echo "[SCNET Pack] Packing SCNET.Game.Protocol"
    dotnet pack "Game.Protocol/Game.Protocol.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_survivalcraft; then
    echo "[SCNET Pack] Packing SCNET.Survivalcraft"
    dotnet pack "Survivalcraft/Survivalcraft.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

if ! $selected || $pack_survivalcraft_modtemplates; then
    echo "[SCNET Pack] Packing SCNET.ModTemplates"
    dotnet pack "Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj" --configuration "$configuration" "${additional_arguments[@]}"
fi

echo "[SCNET Pack] Requested NuGet packages created successfully."
