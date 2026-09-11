---
name: scnet-release-publishing
description: "Publish SCNET distributable application, server, or NuGet artifacts through the repository release scripts and verify their outputs. Use when the user explicitly asks to publish, package release artifacts, or produce distributables; do not use for ordinary builds, tests, or development validation."
---

# SCNET Release Publishing

Use this workflow only for an explicit request to create distributable artifacts. Publishing writes to `Publish/`, may restore packages, and may build or replace a local ContentServer container image as part of the publish target. Do not interpret a request to build, compile, test, or validate as authorization to publish.

## Choose the release path

- Application and service artifacts: run `Scripts/publish.sh` on Bash hosts or `Scripts/publish.ps1` on PowerShell hosts from the repository root.
- NuGet SDK and template packages: run `Scripts/pack-nuget.sh` or `Scripts/pack-nuget.ps1` from the repository root.
- A user-requested single target: run only that target's explicit `dotnet publish` or `dotnet pack` command as written in the corresponding script. Keep target-specific arguments local to that command.

Treat the repository scripts as the canonical inventory and ordering. Read the relevant script before publishing. Do not replace it with solution-level `dotnet publish` or `dotnet pack`, parallelize its entries, or introduce a loop: shared projects are multi-targeted and individual release entries may require different parameters.

## Critical platform constraints

- Publish `Survivalcraft.Windows/Survivalcraft.Windows.csproj` without adding `-r win-x64`. The project already owns its runtime identifier. Passing the RID globally propagates it into Android target frameworks of shared projects and can request the nonexistent `Microsoft.NETCore.App.Runtime.Mono.win-x64` package.
- Android release publishing includes Arm64 and Arm32 only. Do not add X64 or X86 unless the user changes the supported release set.
- ContentServer Release publish creates the portable archive and, on Linux, a container image bundle. It requires Podman or Docker and the configured base image policy; report an unavailable container engine or base image as an environmental publishing failure.
- Never move release-copy or packaging targets back to `Build`. Ordinary builds must not update the repository `Publish/` directory.

## Execute and verify

Run release entries sequentially and stop at the first failure. Preserve the complete failing command and the first actionable error; distinguish restore/network/toolchain failures from repository defects. Do not silently retry with different target frameworks, runtime identifiers, or package sources.

After a successful run, verify the requested files exist under `Publish/` or `Publish/NuGet`. Do not infer success from an old file: use command output and current file metadata, and identify unrelated stale artifacts without deleting them. Report which targets succeeded, which were skipped, the generated filenames, and any non-fatal warnings that affect deployability.

Refer to `Doc/BuildAndConfig.md`, `Doc/NuGet.md`, and `Doc/ContentServer.md` for the maintained artifact contracts. If publishing behavior or the release inventory changes, update both shell variants, those affected documents, and this Skill in the same change.
