#!/usr/bin/env bash
set -euo pipefail
source_root="${IMAGE_CODEC_SOURCES:-$HOME/Desktop/code}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-image-codecs-android.XXXXXX")"
cleanup() {
    local exit_code=$?
    if (( exit_code == 0 )); then
        rm -rf -- "$work"
    else
        echo "Build failed; temporary files retained at $work" >&2
    fi
}
trap cleanup EXIT
ndk="${ANDROID_NDK_ROOT:-$HOME/Android/Sdk/ndk/29.0.14206865}"
toolchain="$ndk/build/cmake/android.toolchain.cmake"
llvm="$ndk/toolchains/llvm/prebuilt/linux-x86_64/bin"
nasm="${IMAGE_CODEC_NASM:-$(command -v nasm || true)}"
mkdir -p "$work/logs" "$work/out"
[[ -x "$nasm" && -f "$toolchain" ]]
for repo_tag in libpng:v1.6.59 libwebp:v1.6.0 libjpeg-turbo:3.2.0 zlib:v1.3.2; do
    repo="${repo_tag%%:*}"
    tag="${repo_tag#*:}"
    [[ -z "$(git -C "$source_root/$repo" status --porcelain)" ]]
    timeout 60 git -C "$source_root/$repo" fetch --depth 1 origin "refs/tags/$tag:refs/tags/$tag" > "$work/logs/$repo-fetch.log" 2>&1
    git -C "$source_root/$repo" checkout --detach "$tag" >> "$work/logs/$repo-fetch.log" 2>&1
    echo "SOURCE $repo $tag $(git -C "$source_root/$repo" rev-parse HEAD)"
done
for abi in armeabi-v7a x86 x86_64 arm64-v8a; do
    case "$abi" in
        armeabi-v7a) rid=android-arm; triple=armv7a-linux-androideabi23 ;;
        x86) rid=android-x86; triple=i686-linux-android23 ;;
        x86_64) rid=android-x64; triple=x86_64-linux-android23 ;;
        arm64-v8a) rid=android-arm64; triple=aarch64-linux-android23 ;;
    esac
    output="$work/out/$rid/native"
    mkdir -p "$output"
    common=(-G "Unix Makefiles" -DCMAKE_TOOLCHAIN_FILE="$toolchain" -DANDROID_ABI="$abi"
        -DANDROID_PLATFORM=android-23 -DCMAKE_BUILD_TYPE=Release -DCMAKE_POSITION_INDEPENDENT_CODE=ON)
    echo "BUILD zlib + PNG $rid"
        cmake -S "$source_root/zlib" -B "$work/build/$rid/zlib" "${common[@]}" \
            -DZLIB_BUILD_SHARED=OFF -DZLIB_BUILD_STATIC=ON -DZLIB_BUILD_TESTING=OFF \
            -DCMAKE_INSTALL_PREFIX="$work/install/$rid/zlib" > "$work/logs/$rid-zlib.log" 2>&1
        cmake --build "$work/build/$rid/zlib" -j 4 >> "$work/logs/$rid-zlib.log" 2>&1
        cmake --install "$work/build/$rid/zlib" >> "$work/logs/$rid-zlib.log" 2>&1
        cmake -S "$source_root/libpng" -B "$work/build/$rid/png" "${common[@]}" \
            -DPNG_SHARED=ON -DPNG_STATIC=OFF -DPNG_TESTS=OFF -DPNG_TOOLS=OFF \
            -DZLIB_LIBRARY="$work/install/$rid/zlib/lib/libz.a" \
            -DZLIB_INCLUDE_DIR="$work/install/$rid/zlib/include" > "$work/logs/$rid-png.log" 2>&1
        cmake --build "$work/build/$rid/png" -j 4 >> "$work/logs/$rid-png.log" 2>&1
        cp -L "$work/build/$rid/png/libpng16.so" "$output/libpng.so"
    echo "BUILD WebP $rid"
    cmake -S "$source_root/libwebp" -B "$work/build/$rid/webp" "${common[@]}" \
        -DBUILD_SHARED_LIBS=OFF -DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEBP=OFF \
        -DWEBP_BUILD_DWEBP=OFF -DWEBP_BUILD_GIF2WEBP=OFF -DWEBP_BUILD_IMG2WEBP=OFF \
        -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF -DWEBP_BUILD_WEBPMUX=OFF \
        -DWEBP_BUILD_EXTRAS=OFF -DWEBP_BUILD_LIBWEBPMUX=ON > "$work/logs/$rid-webp.log" 2>&1
    cmake --build "$work/build/$rid/webp" --target webp webpdemux libwebpmux -j 4 >> "$work/logs/$rid-webp.log" 2>&1
    "$llvm/clang" --target="$triple" -shared -Wl,--no-undefined -Wl,-soname,libwebp.so -Wl,-z,max-page-size=16384 \
        -Wl,--whole-archive "$work/build/$rid/webp/libwebp.a" "$work/build/$rid/webp/libsharpyuv.a" \
        -Wl,--no-whole-archive -lm -o "$output/libwebp.so"
    for name in webpdemux webpmux; do
        "$llvm/clang" --target="$triple" -shared -Wl,--no-undefined -Wl,-soname,"lib$name.so" -Wl,-z,max-page-size=16384 \
            -Wl,--whole-archive "$work/build/$rid/webp/lib$name.a" -Wl,--no-whole-archive \
            -L"$output" -lwebp -lm -o "$output/lib$name.so"
    done
    echo "BUILD TurboJPEG $rid"
    cmake -S "$source_root/libjpeg-turbo" -B "$work/build/$rid/jpeg" "${common[@]}" \
        -DENABLE_SHARED=ON -DENABLE_STATIC=OFF -DWITH_SIMD=ON \
        -DCMAKE_ASM_NASM_COMPILER="$nasm" > "$work/logs/$rid-jpeg.log" 2>&1
    cmake --build "$work/build/$rid/jpeg" --target turbojpeg -j 4 >> "$work/logs/$rid-jpeg.log" 2>&1
    cp -L "$work/build/$rid/jpeg/libturbojpeg.so" "$output/libturbojpeg.so"
    "$llvm/llvm-strip" --strip-unneeded "$output/"*.so
    echo "COMPLETE $rid"
done
for rid in android-arm android-arm64 android-x86 android-x64; do
    destination="$project_root/runtimes/$rid/native"
    mkdir -p "$destination"
    for library in libpng.so libwebp.so libturbojpeg.so; do
        cp "$work/out/$rid/native/$library" "$destination/$library"
    done
done
(cd "$project_root" && find runtimes -type f -print0 | sort -z | xargs -0 sha256sum > "$work/SHA256SUMS")
mv "$work/SHA256SUMS" "$project_root/SHA256SUMS"
echo "COMPLETE ALL: $project_root/runtimes"
