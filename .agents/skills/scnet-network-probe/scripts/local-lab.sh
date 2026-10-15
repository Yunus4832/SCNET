#!/usr/bin/env bash
set -euo pipefail
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
export SCNET_LAB_HOST_NETNS
SCNET_LAB_HOST_NETNS=$(readlink /proc/self/ns/net)
if (( EUID == 0 )); then
    exec unshare --net python3 "$script_dir/local-lab.py" --isolated "$@"
fi
exec unshare --user --map-root-user --net python3 "$script_dir/local-lab.py" --isolated "$@"
