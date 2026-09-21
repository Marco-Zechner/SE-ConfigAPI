# Mz.ConfigAPI.Consumer

Typed consumer facade for the `MarcoZechner.ConfigAPI` Space Engineers mod API.

The package contains only the consumer-facing source required by another mod to use ConfigAPI. Provider implementation, persistence implementation, and ConfigAPI's own runtime infrastructure are not part of this package.

## SELibs dependencies

`Mz.ConfigAPI.Consumer` 2.3.0 has these exact source-package dependencies:

- `Mz.ApiProtocol` 0.3.0
- `Mz.SemanticVersioning` 0.2.0

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

Providers at API 2.3.0 or newer may additionally expose independent preset capabilities. `SupportsPresets` reports synchronous Local/Global `ApplyPreset(...)`; `SupportsPresetSaving` reports synchronous Local/Global `SavePreset(...)`. `SupportsWorldPresets` reports asynchronous `ApplyPresetWorld(...)`; `SupportsWorldPresetSaving` reports asynchronous `SavePresetWorld(...)`. The World preset capabilities require the base World config contract but remain independent of `SupportsWorldFileOperations`, so 2.0-2.2 providers remain compatible.

All World operations are asynchronous requests. Authoritative snapshots, stale-write corrections, export confirmations, and errors are delivered through `WorldConfigResponseReceived`. Reload, LoadAndSwitch, Save, and SaveAndSwitch operate against the authoritative server iteration. Export writes the requested target file without switching authoritative state or advancing its iteration. Local and Global `Open`/`Save` remain synchronous and unchanged.

Applying a preset copies the preset's semantic config values into the canonical active file. For typed Local/Global configs, that canonical file is `ConfigDefinition<T>.DefaultFile`; `ConfigHandle<T>.ApplyPreset(...)` returns to that canonical filename even if legacy `SwitchFile(...)` previously selected another file. The preset file itself is retained and is not made the active config identity. Missing presets fail instead of synthesizing defaults.

Saving a preset writes the supplied semantic config values to a retained preset file with normal provenance and defaults to `overwrite: false`. It does not change `ConfigHandle<T>.CurrentFile`, the canonical active file, World `CurrentFile`, or World `ServerIteration`. Local/Global preset targets may not be `ConfigDefinition<T>.DefaultFile`; World preset targets may not be the current authoritative file.

## Serialization contract

`ConfigDefinition<T>` requires the consuming mod to provide three operations: create current defaults, serialize `T` to a `ConfigDocument`, and deserialize a `ConfigDocument` back to `T`.

ConfigAPI does not reflect arbitrary CLR config models. CLR representation is owned by the consumer. Enums, nullable values, collections, dictionaries, nested models, or other application-specific types are supported when the consumer's serialization delegates map them to the semantic document model.

The semantic model supports null, Boolean, signed 64-bit Integer, double-precision Float, String, Object, Array, offset date-time, local date-time, local date, and local time values.

## Ownership

`Consumer/Mz.ConfigAPI.Consumer` in the ConfigAPI repository is the canonical package source.

A published SELibs component contains only:

    Libraries/Mz.ConfigAPI.Consumer/

ConfigAPI provider source is never included in the consumer package.
