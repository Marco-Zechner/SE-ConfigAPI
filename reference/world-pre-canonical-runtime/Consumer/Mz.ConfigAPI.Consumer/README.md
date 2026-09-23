# Mz.ConfigAPI.Consumer

Typed consumer facade for the `MarcoZechner.ConfigAPI` Space Engineers mod API.

The package contains only the consumer-facing source required by another mod to use ConfigAPI. Provider implementation, persistence implementation, and ConfigAPI's own runtime infrastructure are not part of this package.

## SELibs dependencies

`Mz.ConfigAPI.Consumer` 2.3.0 has these exact source-package dependencies:

- `Mz.ApiProtocol` 0.3.1
- `Mz.Collections` 0.1.0
- `Mz.SemanticVersioning` 0.2.0
- `Mz.Storage` 0.1.0

Space Engineers API assemblies used by the storage adapter are game/runtime references, not SELibs package dependencies.

## Install

From the consuming mod root:

    selibs add Mz.ConfigAPI.Consumer@2.3.0

SELibs installs this package under the consuming mod's `Data/Scripts/.../Libraries` tree together with its exact transitive source dependencies.

## Compatibility

Consumer package version: `2.3.0`

Minimum ConfigAPI provider API version: `2.0.0`

The consumer accepts newer compatible provider API versions and validates the required endpoint contract when connecting.

## World configs

Providers at API 2.1.0 or newer may expose the optional server-authoritative World config endpoint set. `SupportsWorldConfigs` reports whether the connected provider exposes the complete Open/Save set.

Providers at API 2.2.0 or newer may additionally expose the complete World file-operation set. `SupportsWorldFileOperations` reports whether `ReloadWorld(...)`, `LoadAndSwitchWorld(...)`, `SaveAndSwitchWorld(...)`, and `ExportWorld(...)` are available. Providers that expose only the 2.1 World contract remain compatible.

ConfigAPI 2.3 adds indexed Local and Global variant discovery and typed runtime state. `ConfigHandle<T>` opens the `default` variant initially and derives editable files as `<ConfigKey>.<variant>.toml`. `ListVariants()` returns exact indexed variants, `Load(variant)` switches the active variant, and `SaveAs(variant)` persists the current `Applied` values to a new variant before switching `CurrentVariant`.

`ConfigHandle<T>` separates `Defaults`, `Stored`, `Applied`, and mutable `Draft` state. Editing `Draft` does not change runtime state; `Apply()` updates runtime state without saving; `Save()` persists `Applied`; `DiscardDraft()` restores the draft from `Applied`; and `ResetDraftToDefaults()` edits only the draft. `HasDraftChanges` and `HasUnsavedChanges` report semantic differences between those states.

All World operations are asynchronous requests. Authoritative snapshots, stale-write corrections, export confirmations, and errors are delivered through `WorldConfigResponseReceived`. Reload, LoadAndSwitch, Save, and SaveAndSwitch operate against the authoritative server iteration. Export writes the requested target file without switching authoritative state or advancing its iteration.
## Serialization contract

`ConfigDefinition<T>` requires the consuming mod to provide three operations: create current defaults, serialize `T` to a `ConfigDocument`, and deserialize a `ConfigDocument` back to `T`.

ConfigAPI does not reflect arbitrary CLR config models. CLR representation is owned by the consumer. Enums, nullable values, collections, dictionaries, nested models, or other application-specific types are supported when the consumer's serialization delegates map them to the semantic document model.

The semantic model supports null, Boolean, signed 64-bit Integer, double-precision Float, String, Object, Array, offset date-time, local date-time, local date, and local time values.

## Ownership

`Consumer/Mz.ConfigAPI.Consumer` in the ConfigAPI repository is the canonical package source.

A published SELibs component contains only:

    Libraries/Mz.ConfigAPI.Consumer/

ConfigAPI provider source is never included in the consumer package.
