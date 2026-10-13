#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
archive_name="__RENDEZVOUSSERVER_ARCHIVE__"
image_reference="__RENDEZVOUSSERVER_IMAGE__"
import_image=true
start_container=true
config_file="$script_dir/server.conf"

while (($#)); do
  case "$1" in
    --skip-import) import_image=false; shift ;;
    --no-start) start_container=false; shift ;;
    --config)
      (($# >= 2)) || { echo "--config requires a path." >&2; exit 2; }
      config_file="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: ./deploy.sh [--config PATH] [--skip-import] [--no-start]"
      exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

if [[ -n "${CONTAINER_ENGINE:-}" ]]; then
  container_engine="$CONTAINER_ENGINE"
elif command -v podman >/dev/null 2>&1; then
  container_engine=podman
else
  container_engine=docker
fi
case "$container_engine" in
  podman|docker) ;;
  *) echo "CONTAINER_ENGINE must be podman or docker." >&2; exit 2 ;;
esac
command -v "$container_engine" >/dev/null

# Resolve caller-supplied paths before entering the deployment directory.
if [[ "$config_file" != /* ]]; then
  config_file="$PWD/$config_file"
fi
cd "$script_dir"
if [[ ! -f "$config_file" ]]; then
  if [[ "$config_file" != "$script_dir/server.conf" ]]; then
    echo "Configuration file not found: $config_file" >&2
    exit 2
  fi
  cp server.conf.example "$config_file"
  chmod 600 "$config_file"
  echo "Created server.conf. Configure advertised addresses and HTTPS before public deployment."
fi
bash -n "$config_file"
set -a
source "$config_file"
set +a
unset Kestrel__Certificates__Default__Path Kestrel__Certificates__Default__KeyPath Kestrel__Certificates__Default__Password
if [[ -n "${TLS_CERTIFICATE_PATH:-}" ]]; then
  export Kestrel__Certificates__Default__Path="$TLS_CERTIFICATE_PATH"
  if [[ -n "${TLS_CERTIFICATE_KEY_PATH:-}" ]]; then
    export Kestrel__Certificates__Default__KeyPath="$TLS_CERTIFICATE_KEY_PATH"
  fi
  if [[ -n "${TLS_CERTIFICATE_PASSWORD:-}" ]]; then
    export Kestrel__Certificates__Default__Password="$TLS_CERTIFICATE_PASSWORD"
  fi
fi

if $import_image; then
  sha256sum -c "$archive_name.sha256"
  "$container_engine" load -i "$archive_name"
fi
if ! $start_container; then
  exit 0
fi
"$container_engine" image inspect "$image_reference" >/dev/null
mkdir -p Certificates
if [[ "$container_engine" == docker ]]; then
  docker compose version >/dev/null
  docker compose -f compose.yaml up -d --force-recreate rendezvous-server
elif podman compose version >/dev/null 2>&1; then
  podman compose -f compose.yaml up -d --force-recreate rendezvous-server
elif command -v podman-compose >/dev/null 2>&1; then
  podman-compose -f compose.yaml up -d --force-recreate rendezvous-server
else
  echo "Podman Compose is required." >&2
  exit 1
fi
echo "RendezvousServer started. Configuration: $config_file"
