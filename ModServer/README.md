# ModServer

This project provides the v1 mod repository service used by the current
Survivalcraft mod distribution model.

## Container Deployment

The container deployment files live in [deploy/](./deploy):

- [deploy/Dockerfile](./deploy/Dockerfile)
- [deploy/compose.yaml](./deploy/compose.yaml)

The compose setup uses:

- host port `9527`
- container port `8080`
- data volume `/home/yunus/Desktop/temp/ModServer/data:/data`
- upload API key `local-dev-upload-key`

The image intentionally uses `mcr.microsoft.com/dotnet/sdk:10.0` for both build
and runtime stages so it can run on a machine that already has only the `sdk`
and `runtime` base images prepared, without introducing an extra ASP.NET base
image dependency.

## Start

```bash
PATH="$HOME/.local/bin:$PATH" podman compose -f ModServer/deploy/compose.yaml up -d
```

## Stop

```bash
PATH="$HOME/.local/bin:$PATH" podman compose -f ModServer/deploy/compose.yaml down
```

## Health Check

```bash
curl http://127.0.0.1:9527/api/v1/health
```

## Upload a Mod Package

Build the verification mod first:

```bash
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug -v minimal
```

Then upload:

```bash
curl -X POST http://127.0.0.1:9527/api/v1/mods/upload \
  -H "X-Api-Key: local-dev-upload-key" \
  -F modId=verification.block \
  -F version=1.0.0 \
  -F side=common \
  -F description="Verification Block example mod" \
  -F package=@VerificationBlockMod/bin/Debug/net10.0/packages/verification.block.scpak
```

## Repository Data

After upload, the mounted host directory contains:

- `index.json`
- `packages/<packageHash>.scpak`

Location:

```text
/home/yunus/Desktop/temp/ModServer/data
```
