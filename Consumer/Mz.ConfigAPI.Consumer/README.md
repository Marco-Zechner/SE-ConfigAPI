# Mz.ConfigAPI.Consumer

Typed consumer facade for the `MarcoZechner.ConfigAPI` Space Engineers mod API.

The package contains only the consumer-facing source required by another mod to use ConfigAPI. Provider implementation, persistence implementation, and ConfigAPI's own runtime infrastructure are not part of this package.

## SELibs dependencies

`Mz.ConfigAPI.Consumer` 2.3.0 has these exact source-package dependencies:

- `Mz.ApiProtocol` 0.3.1
- `Mz.Collections` 0.1.0
- `Mz.SemanticVersioning` 0.2.0
- `Mz.Storage` 0.1.1

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

World configs use the same `Defaults`, `Stored`, `Applied`, and editable `Draft` model as Local and Global configs, but authoritative state is owned by the server and synchronized over ConfigAPI networking.

`SupportsWorldConfigs` is true only when the connected provider exposes the complete canonical World endpoint set: Open, Apply, Save, Reload, Load, SaveAs, and ListVariants.

`OpenWorld(...)` opens the `default` variant for a config key. Physical World filenames are derived internally as `<ConfigKey>.<variant>.toml`; callers work with config keys and variant names rather than raw filenames.

World operations are asynchronous and results are delivered through `WorldConfigResponseReceived`:

- `ApplyWorld(...)` sends a draft to the authoritative server. On success it updates `Applied` and advances `Revision` without writing the active variant.
- `SaveWorld(...)` persists the current authoritative `Applied` values into `Stored` for the current variant. It does not accept a draft payload.
- `ReloadWorld(...)` reloads the current variant from server storage into `Stored` and `Applied`.
- `LoadWorld(..., variant)` switches to an existing named variant and loads it into `Stored` and `Applied`.
- `SaveAsWorld(..., variant)` persists the current authoritative `Applied` values as a new variant and switches `CurrentVariant` only after successful persistence.
- `ListWorldVariants(...)` returns the currently indexed named variants.

Mutating World requests use optimistic `Revision` concurrency. A stale request does not overwrite authoritative state; the response reports `IsStale` and carries the current authoritative snapshot. `WorldConfigResponse` exposes `Stored`, `Applied`, `Revision`, `CurrentVariant`, `HasUnsavedChanges`, `IsChanged`, and `IsStale`. Variant-list responses expose `Variants`.
## Serialization contract

`ConfigDefinition<T>` requires the consuming mod to provide three operations: create current defaults, serialize `T` to a `ConfigDocument`, and deserialize a `ConfigDocument` back to `T`.

ConfigAPI does not reflect arbitrary CLR config models. CLR representation is owned by the consumer. Enums, nullable values, collections, dictionaries, nested models, or other application-specific types are supported when the consumer's serialization delegates map them to the semantic document model.

The semantic model supports null, Boolean, signed 64-bit Integer, double-precision Float, String, Object, Array, offset date-time, local date-time, local date, and local time values.

## Ownership

`Consumer/Mz.ConfigAPI.Consumer` in the ConfigAPI repository is the canonical package source.

A published SELibs component contains only:

    Libraries/Mz.ConfigAPI.Consumer/

ConfigAPI provider source is never included in the consumer package.
