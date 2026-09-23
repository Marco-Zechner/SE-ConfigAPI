# Pre-canonical World runtime reference

This directory is an inert source snapshot retained while ConfigAPI's unreleased World configuration stack is rewritten around the canonical runtime-state model.

Snapshot source:
- branch: feature/config-presets
- source checkpoint: 959a6ff (`feat: add world named variant service`)
- captured before removing the legacy World transport/provider/consumer operation surface

Why this exists:
- ConfigAPI has not released this World API, so backward compatibility with the archived contract is not required.
- The live implementation is being replaced rather than incrementally preserving `LoadAndSwitch`, `SaveAndSwitch`, `Export`, `ApplyPreset`, `SavePreset`, combined `Save(draft)`, `BaseIteration`, raw file identity, and related compatibility machinery.
- Networking itself is still required. The replacement keeps authoritative server/client synchronization, trusted requester identity, snapshots, broadcasts, optimistic revisions, private client drafts, and bootstrap behavior, but expresses them using the canonical model.

Canonical direction:
- authoritative state: active variant + Stored + Applied + Revision
- private client state: Draft + DraftBaseApplied + DraftBaseRevision
- operations: Open, Apply, Save, Reload, Load(variant), SaveAs(variant), ListVariants
- Apply changes runtime only
- Save persists Applied only
- variant files are derived as `<ConfigKey>.<variant>.toml`
- network mutations use expected Revision
- stale requests preserve private drafts
- no public compatibility aliases are required before first release

This directory is outside the Data and test project roots and must remain excluded from the live build. It is reference material only and should be removed once the replacement has equivalent verified coverage.