# Wire payload baselines

Captured before protocol extraction from commit `67dfe654` on .NET 10, Release build.
Each `.bin` contains an uncompressed application frame and one `0x88 + message ID + payload` entry.
During the unreleased `PackageType` cleanup, only the message ID byte (offset 6) was explicitly updated to the current assignments. All other captured bytes remain unchanged. `package-ids.json` records the current explicit assignments, including the `ModEnvelope` ID. There is no runtime legacy-ID mapping. Do not regenerate payload samples using the new codecs to make tests pass.

Samples cover handshake, rejection, 100 body states, terrain request/delta/fragment, initial snapshot and a mod envelope.
The snapshot entity bytes are an opaque deterministic 4096-byte payload, not a playable world fixture.

Six additional samples (vital-state, skin-reply, furnace-state, electricity-state, sign-state and block-edit) were captured during extraction using the preserved **original** Release assemblies from the same checkpoint, not the new protocol writer. Original Survivalcraft.dll SHA-256: `7ac10ab26b81d545db3fd7176aa7af29ae0839921dfc1f3a32389ceee6a3556e`. The sign sample has two lines and two colors.

The text-command request sample was removed when frontend parsing moved to clients and command invocation adopted one binary format. It is no longer a supported wire contract.

The stage-two identity change removes the self-claimed client GUID (one length byte plus 16 data bytes) from the connection request. That sample was independently edited by removing original bytes 32–48 and subtracting 17 from its frame length; the token, protocol version, magic and mod hash remain unchanged. Identity now comes from the authenticated transport, not this payload.
