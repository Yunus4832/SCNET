#!/usr/bin/env bash
set -euo pipefail
rid="${1:-linux-x64}"
source_root="${SDL_SOURCE:?Set SDL_SOURCE to SDL 2.32.10 source}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rg '^#define SDL_MAJOR_VERSION\s+2$' "$source_root/include/SDL_version.h" >/dev/null
rg '^#define SDL_MINOR_VERSION\s+32$' "$source_root/include/SDL_version.h" >/dev/null
rg '^#define SDL_PATCHLEVEL\s+10$' "$source_root/include/SDL_version.h" >/dev/null
common=(-G 'Unix Makefiles' -DCMAKE_BUILD_TYPE=Release -DSDL_SHARED=ON
    -DSDL_STATIC=OFF -DSDL_TEST=OFF -DSDL_TESTS=OFF)
case "$rid" in
    linux-x64)
        common+=(-DSDL_X11=ON -DSDL_WAYLAND=ON)
        library=libSDL2-2.0.so
        strip_tool=strip
        ;;
    win-x64)
        compiler_root="${SDL_WINDOWS_TOOLCHAIN:?Set SDL_WINDOWS_TOOLCHAIN to LLVM-MinGW}"
        common+=(-DCMAKE_SYSTEM_NAME=Windows
            -DCMAKE_C_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-clang"
            -DCMAKE_CXX_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-clang++"
            -DCMAKE_RC_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-windres"
            -DCMAKE_SHARED_LINKER_FLAGS=-static)
        library=SDL2.dll
        strip_tool="$compiler_root/bin/llvm-strip"
        ;;
    *) echo "Unsupported SDL RID: $rid" >&2; exit 2 ;;
esac
work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-sdl-desktop.XXXXXX")"
trap 'echo "Build files: $work"' EXIT
cmake -S "$source_root" -B "$work/build" "${common[@]}" > "$work/build.log" 2>&1
cmake --build "$work/build" -j "${SDL_BUILD_JOBS:-4}" >> "$work/build.log" 2>&1
cp -L "$work/build/$library" "$work/$library"
"$strip_tool" --strip-unneeded "$work/$library"
mkdir -p "$project_root/runtimes/$rid/native"
cp "$work/$library" "$project_root/runtimes/$rid/native/"
(cd "$project_root" && find runtimes buildTransitive/java -type f -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
rm -rf -- "$work"
trap - EXIT
