# Verification Block Mod

This mod is the end-to-end example for the current mod runtime. It references the
`Survivalcraft` project directly and demonstrates:

- code, block data, and namespaced content assets in one `.scpkg`;
- registration of `verification.block:verification_block` at runtime index `900`;
- mod lifecycle logging;
- player damage interception and modification;
- custom digging speed for the verification block;
- block placement and terrain change observation;
- entity-added and world-update notifications.

Build the core contract and then the mod:

```bash
dotnet build Survivalcraft/Survivalcraft.csproj -c Debug -f net10.0
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug
```

Copy `bin/Debug/net10.0/packages/verification.block.scpkg` into the game's `Mods` directory and restart the game. For the repository Linux build, that directory is `Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/Mods`.

The block appears near the beginning of the `Construction` creative category as `Verification Block` and uses existing texture slot `182`. While the mod is active, player damage is halved and the verification block can be mined ten times faster.
