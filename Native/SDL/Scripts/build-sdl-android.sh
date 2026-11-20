#!/usr/bin/env bash
set -euo pipefail
source_root="${SDL_SOURCE:?Set SDL_SOURCE to SDL 2.32.10 source}"
ndk="${ANDROID_NDK_ROOT:?Set ANDROID_NDK_ROOT to Android NDK r29}"
android_jar="${ANDROID_JAR:?Set ANDROID_JAR to Android SDK platforms/android-35/android.jar}"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rg '^#define SDL_MAJOR_VERSION\s+2$' "$source_root/include/SDL_version.h" >/dev/null
rg '^#define SDL_MINOR_VERSION\s+32$' "$source_root/include/SDL_version.h" >/dev/null
rg '^#define SDL_PATCHLEVEL\s+10$' "$source_root/include/SDL_version.h" >/dev/null
work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-sdl-android.XXXXXX")"
trap 'echo "Build files: $work"' EXIT
llvm="$ndk/toolchains/llvm/prebuilt/linux-x86_64/bin"
mkdir -p "$work/classes"
javac -source 8 -target 8 -Xlint:-options -classpath "$android_jar" \
    -d "$work/classes" "$source_root"/android-project/app/src/main/java/org/libsdl/app/*.java \
    > "$work/java.log" 2>&1
jar --create --file "$work/SDL2.jar" -C "$work/classes" .
for abi in armeabi-v7a arm64-v8a x86 x86_64; do
    case "$abi" in
        armeabi-v7a) rid=android-arm; triple=armv7a-linux-androideabi ;;
        arm64-v8a) rid=android-arm64; triple=aarch64-linux-android ;;
        x86) rid=android-x86; triple=i686-linux-android ;;
        x86_64) rid=android-x64; triple=x86_64-linux-android ;;
    esac
    echo "Building SDL 2.32.10: $rid"
    cmake -S "$source_root" -B "$work/$rid" -G 'Unix Makefiles' \
        -DCMAKE_TOOLCHAIN_FILE="$ndk/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI="$abi" -DANDROID_PLATFORM=android-23 -DANDROID_STL=c++_static \
        -DCMAKE_BUILD_TYPE=Release -DSDL_SHARED=ON -DSDL_STATIC=OFF \
        -DSDL_TEST=OFF -DSDL_TESTS=OFF -DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384 \
        > "$work/$rid.log" 2>&1
    cmake --build "$work/$rid" -j "${SDL_BUILD_JOBS:-4}" >> "$work/$rid.log" 2>&1
    "$llvm/${triple}23-clang" -shared -fPIC -O2 -fvisibility=hidden \
        -Wl,-z,max-page-size=16384 -Wl,-soname,libmain.so \
        "$project_root/bridge/main.c" -o "$work/$rid/libmain.so"
    "$llvm/llvm-strip" --strip-unneeded "$work/$rid/libSDL2.so" "$work/$rid/libmain.so"
done
for rid in android-arm android-arm64 android-x86 android-x64; do
    mkdir -p "$project_root/runtimes/$rid/native"
    cp "$work/$rid/libSDL2.so" "$work/$rid/libmain.so" "$project_root/runtimes/$rid/native/"
done
mkdir -p "$project_root/buildTransitive/java"
cp "$work/SDL2.jar" "$project_root/buildTransitive/java/SDL2.jar"
(cd "$project_root" && find runtimes buildTransitive/java -type f -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
rm -rf -- "$work"
trap - EXIT
