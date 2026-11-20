#!/usr/bin/env bash
set -euo pipefail

source_root="${OPENAL_SOURCE:?Set OPENAL_SOURCE to an unmodified OpenAL Soft 1.25.2 checkout}"
oboe_source="${OBOE_SOURCE:?Set OBOE_SOURCE to the Oboe 5714d5362afac77778558e2ba9800b0f53a3f6ea checkout}"
ndk="${ANDROID_NDK_ROOT:-$HOME/Android/Sdk/ndk/29.0.14206865}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[[ "$(git -C "$source_root" describe --tags --exact-match)" == "1.25.2" ]]
[[ -z "$(git -C "$source_root" status --porcelain)" ]]
[[ "$(git -C "$oboe_source" rev-parse HEAD)" == "5714d5362afac77778558e2ba9800b0f53a3f6ea" ]]
[[ -z "$(git -C "$oboe_source" status --porcelain)" ]]
[[ -f "$ndk/build/cmake/android.toolchain.cmake" ]]

work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-openal-android.XXXXXX")"
cleanup() {
    local exit_code=$?
    if (( exit_code == 0 )); then
        rm -rf -- "$work"
    else
        echo "Build failed; temporary files retained at $work" >&2
    fi
}
trap cleanup EXIT

for abi in armeabi-v7a arm64-v8a x86 x86_64; do
    case "$abi" in
        armeabi-v7a) rid=android-arm ;;
        arm64-v8a) rid=android-arm64 ;;
        x86) rid=android-x86 ;;
        x86_64) rid=android-x64 ;;
    esac
    extra=()
    if [[ "$abi" == armeabi-v7a || "$abi" == x86 ]]; then
        # NDK 29's function-effects analysis misclassifies consteval size literals on 32-bit targets.
        extra+=(-DHAVE_WFUNCTION_EFFECTS=OFF)
    fi
    echo "Building OpenAL 1.25.2: $rid"
    cmake -S "$source_root" -B "$work/$rid" -G "Unix Makefiles" \
        -DCMAKE_TOOLCHAIN_FILE="$ndk/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI="$abi" -DANDROID_PLATFORM=android-23 -DANDROID_STL=c++_static \
        -DCMAKE_BUILD_TYPE=Release -DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384 \
        -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_TESTS=OFF \
        -DALSOFT_UPDATE_BUILD_VERSION=OFF -DOBOE_SOURCE="$oboe_source" \
        -DOBOE_BUILD_SHARED=OFF -DALSOFT_REQUIRE_OBOE=ON -DALSOFT_REQUIRE_OPENSL=ON \
        -DALSOFT_BACKEND_PIPEWIRE=OFF -DALSOFT_BACKEND_PULSEAUDIO=OFF \
        -DALSOFT_BACKEND_ALSA=OFF -DALSOFT_BACKEND_JACK=OFF -DALSOFT_BACKEND_OSS=OFF \
        "${extra[@]}" \
        > "$work/$rid.log" 2>&1
    cmake --build "$work/$rid" -j "${OPENAL_BUILD_JOBS:-4}" >> "$work/$rid.log" 2>&1
    "$ndk/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip" \
        --strip-unneeded "$work/$rid/libopenal.so"
done

# Replace project assets only after all four ABIs have built successfully.
for rid in android-arm android-arm64 android-x86 android-x64; do
    destination="$project_root/runtimes/$rid/native"
    mkdir -p "$destination"
    cp "$work/$rid/libopenal.so" "$destination/"
done
(cd "$project_root" && find runtimes -type f -print0 | sort -z | xargs -0 sha256sum > "$work/SHA256SUMS")
mv "$work/SHA256SUMS" "$project_root/SHA256SUMS"
echo "OpenAL Android build complete: $project_root/runtimes"
