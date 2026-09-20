# Mz.ConfigAPI.Consumer

Typed consumer facade for the `MarcoZechner.ConfigAPI` Space Engineers mod API.

The package contains only the consumer-facing source required by another mod to use ConfigAPI. Provider implementation, persistence implementation, and ConfigAPI's own runtime infrastructure are not part of this package.

## SELibs dependencies

`Mz.ConfigAPI.Consumer` 2.1.0 has these exact source-package dependencies:

- `Mz.ApiProtocol` 0.3.0
- `Mz.SemanticVersioning` 0.2.0

Space Engineers API assemblies used by the storage adapter are game/runtime references, not SELibs package dependencies.

## Install

From the consuming mod root:

    selibs add Mz.ConfigAPI.Consumer@2.1.0

SELibs installs this package under the consuming mod's `Data/Scripts/.../Libraries` tree together with its exact transitive source dependencies.

## Compatibility

Consumer package version: `2.1.0`

Minimum ConfigAPI provider API version: `2.0.0`

The consumer accepts newer compatible provider API versions and validates the required endpoint contract when connecting.

## World configs

Providers at API 2.1.0 or newer may expose the optional server-authoritative World config endpoint set. `SupportsWorldConfigs` reports whether the connected provider exposes the complete set.

`OpenWorld(...)` and `SaveWorld(...)` are asynchronous requests. Authoritative snapshots, stale-write corrections, and errors are delivered through `WorldConfigResponseReceived`. Local and Global `Open`/`Save` remain synchronous and unchanged.

## Serialization contract

`ConfigDefinition<T>` requires the consuming mod to provide three operations: create current defaults, serialize `T` to a `ConfigDocument`, and deserialize a `ConfigDocument` back to `T`.

ConfigAPI does not reflect arbitrary CLR config models. CLR representation is owned by the consumer. Enums, nullable values, collections, dictionaries, nested models, or other application-specific types are supported when the consumer's serialization delegates map them to the semantic document model.

The semantic model supports null, Boolean, signed 64-bit Integer, double-precision Float, String, Object, Array, offset date-time, local date-time, local date, and local time values.

## Ownership

`Consumer/Mz.ConfigAPI.Consumer` in the ConfigAPI repository is the canonical package source.

A published SELibs component contains only:

    Libraries/Mz.ConfigAPI.Consumer/

ConfigAPI provider source is never included in the consumer package.
