#!/usr/bin/env bash
set -euo pipefail

openal_rid="${1:-linux-x64}"
source_root="${OPENAL_SOURCE:?Set OPENAL_SOURCE to an unmodified OpenAL Soft 1.25.2 checkout}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[[ "$(git -C "$source_root" describe --tags --exact-match)" == "1.25.2" ]]
[[ -z "$(git -C "$source_root" status --porcelain)" ]]

common=(-G "Unix Makefiles" -DCMAKE_BUILD_TYPE=Release -DALSOFT_UTILS=OFF
    -DALSOFT_EXAMPLES=OFF -DALSOFT_TESTS=OFF -DALSOFT_UPDATE_BUILD_VERSION=OFF)
case "$openal_rid" in
    linux-x64)
        [[ "$(uname -m)" == x86_64 ]]
        common+=(-DALSOFT_REQUIRE_PIPEWIRE=ON -DALSOFT_REQUIRE_PULSEAUDIO=ON
            -DALSOFT_REQUIRE_ALSA=ON -DALSOFT_STATIC_STDCXX=ON -DALSOFT_STATIC_LIBGCC=ON)
        ;;
    win-x64)
        compiler_root="${OPENAL_WINDOWS_TOOLCHAIN:?Set OPENAL_WINDOWS_TOOLCHAIN to the LLVM-MinGW directory}"
        common+=(-DCMAKE_SYSTEM_NAME=Windows
            -DCMAKE_C_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-clang"
            -DCMAKE_CXX_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-clang++"
            -DCMAKE_RC_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-windres"
            -DCMAKE_SHARED_LINKER_FLAGS=-static
            -DALSOFT_REQUIRE_WASAPI=ON -DALSOFT_BACKEND_PIPEWIRE=OFF
            -DALSOFT_BACKEND_PULSEAUDIO=OFF -DALSOFT_BACKEND_ALSA=OFF -DALSOFT_BACKEND_JACK=OFF)
        ;;
    *) echo "Unsupported OpenAL desktop RID: $openal_rid" >&2; exit 2 ;;
esac

work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-openal-desktop.XXXXXX")"
cleanup() {
    local exit_code=$?
    if (( exit_code == 0 )); then
        rm -rf -- "$work"
    else
        echo "Build failed; temporary files retained at $work" >&2
    fi
}
trap cleanup EXIT
cp -a "$source_root/." "$work/source"
# New libc++ versions overload unique_ptr::operator*, making its member pointer ambiguous.
patch -d "$work/source" -p1 < "$project_root/Scripts/unique-ptr-transform.patch" > "$work/build.log" 2>&1
cmake -S "$work/source" -B "$work/build" "${common[@]}" >> "$work/build.log" 2>&1
cmake --build "$work/build" -j "${OPENAL_BUILD_JOBS:-4}" >> "$work/build.log" 2>&1

if [[ "$openal_rid" == win-x64 ]]; then
    # OpenAL Soft's non-router Windows build is named OpenAL32.dll; Silk searches soft_oal.dll.
    cp "$work/build/OpenAL32.dll" "$work/soft_oal.dll"
    "$compiler_root/bin/llvm-strip" --strip-unneeded "$work/soft_oal.dll"
    library="$work/soft_oal.dll"
else
    cp -L "$work/build/libopenal.so" "$work/libopenal.so"
    strip --strip-unneeded "$work/libopenal.so"
    library="$work/libopenal.so"
fi

destination="$project_root/runtimes/$openal_rid/native"
mkdir -p "$destination"
cp "$library" "$destination/"
(cd "$project_root" && find runtimes -type f -print0 | sort -z | xargs -0 sha256sum > "$work/SHA256SUMS")
mv "$work/SHA256SUMS" "$project_root/SHA256SUMS"
echo "OpenAL build complete: $destination"
