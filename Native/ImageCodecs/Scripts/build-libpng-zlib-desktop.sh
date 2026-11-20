#!/usr/bin/env bash
set -euo pipefail
codec_rid="${1:-linux-x64}"
source_root="${IMAGE_CODEC_SOURCES:-$HOME/Desktop/code}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
common=(-G "Unix Makefiles" -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_STANDARD=17 -DCMAKE_POSITION_INDEPENDENT_CODE=ON)
case "$codec_rid" in
    linux-x64) ;;
    win-x64)
        compiler_root="${IMAGE_CODEC_WINDOWS_TOOLCHAIN:?Set IMAGE_CODEC_WINDOWS_TOOLCHAIN to the LLVM-MinGW directory}"
        common+=(-DCMAKE_SYSTEM_NAME=Windows -DCMAKE_C_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-clang"
            -DCMAKE_RC_COMPILER="$compiler_root/bin/x86_64-w64-mingw32-windres") ;;
    *) echo "Unsupported PNG desktop RID: $codec_rid" >&2; exit 2 ;;
esac
for repo_tag in libpng:v1.6.59 zlib:v1.3.2; do
    repo="${repo_tag%%:*}"
    tag="${repo_tag#*:}"
    [[ "$(git -C "$source_root/$repo" describe --tags --exact-match)" == "$tag" ]]
    [[ -z "$(git -C "$source_root/$repo" status --porcelain)" ]]
done
work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-libpng-zlib-desktop.XXXXXX")"
cleanup() {
    local exit_code=$?
    if (( exit_code == 0 )); then
        rm -rf -- "$work"
    else
        echo "Build failed; temporary files retained at $work" >&2
    fi
}
trap cleanup EXIT
mkdir -p "$work/logs" "$work/out/$codec_rid/native"
cmake -S "$source_root/zlib" -B "$work/build/$codec_rid/zlib" "${common[@]}" \
    -DZLIB_BUILD_SHARED=OFF -DZLIB_BUILD_STATIC=ON -DZLIB_BUILD_TESTING=OFF \
    -DCMAKE_INSTALL_PREFIX="$work/install/$codec_rid/zlib" > "$work/logs/$codec_rid-zlib.log" 2>&1
cmake --build "$work/build/$codec_rid/zlib" -j 4 >> "$work/logs/$codec_rid-zlib.log" 2>&1
cmake --install "$work/build/$codec_rid/zlib" >> "$work/logs/$codec_rid-zlib.log" 2>&1
zlib_archives=("$work/install/$codec_rid/zlib/lib/"*.a)
[[ ${#zlib_archives[@]} == 1 && -f "${zlib_archives[0]}" ]]
cmake -S "$source_root/libpng" -B "$work/build/$codec_rid/png" "${common[@]}" \
    -DPNG_SHARED=ON -DPNG_STATIC=OFF -DPNG_TESTS=OFF -DPNG_TOOLS=OFF \
    -DZLIB_LIBRARY="${zlib_archives[0]}" \
    -DZLIB_INCLUDE_DIR="$work/install/$codec_rid/zlib/include" > "$work/logs/$codec_rid-png.log" 2>&1
cmake --build "$work/build/$codec_rid/png" -j 4 >> "$work/logs/$codec_rid-png.log" 2>&1
if [[ "$codec_rid" == win-x64 ]]; then
    cp "$work/build/$codec_rid/png/libpng16.dll" "$work/out/$codec_rid/native/libpng16.dll"
    "$compiler_root/bin/llvm-strip" --strip-unneeded "$work/out/$codec_rid/native/libpng16.dll"
else
    cp -L "$work/build/$codec_rid/png/libpng16.so" "$work/out/$codec_rid/native/libpng.so"
    strip --strip-unneeded "$work/out/$codec_rid/native/libpng.so"
fi
destination="$project_root/runtimes/$codec_rid/native"
mkdir -p "$destination"
cp "$work/out/$codec_rid/native/"* "$destination/"
(cd "$project_root" && find runtimes -type f -print0 | sort -z | xargs -0 sha256sum > "$work/SHA256SUMS")
mv "$work/SHA256SUMS" "$project_root/SHA256SUMS"
echo "PNG build complete: $destination"
