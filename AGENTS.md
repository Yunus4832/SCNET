# SCNET Agent Instructions

## Project stage and compatibility

SCNET is currently unreleased and has no external user base or compatibility ecosystem. Treat the
repository's current source, tests, assets, and documentation as one changeable unit.

Do not preserve compatibility by default. When changing APIs, configuration, serialization,
protocols, storage, commands, or architecture:

- prefer the clean current design over deprecated overloads, aliases, adapters, fallback parsers,
  dual read/write paths, or legacy branches;
- update all in-repository callers, tests, fixtures, tools, and documentation in the same change;
- remove superseded code and data paths instead of retaining them for hypothetical downstream users;
- do not add migration or version-detection logic unless a real supported boundary requires it.

Compatibility work is justified only when the user explicitly requests it, when the repository
documents an existing supported compatibility contract, or when interoperability with an external
format, protocol, service, or released dependency requires it. Identify that boundary explicitly
before implementing compatibility. Prefer a finite migration or upgrade step over indefinite
runtime compatibility when either approach satisfies the requirement.

Reassess and update this section when SCNET begins publishing releases, supporting external users,
or maintaining third-party integrations.
