# Mz.ConfigAPI.Consumer

Typed consumer facade for the `MarcoZechner.ConfigAPI` Space Engineers mod API.

The package contains only the consumer-facing source required by another mod to use ConfigAPI. Provider implementation, persistence implementation, and ConfigAPI's own runtime infrastructure are not part of this package.

## SELibs dependencies

`Mz.ConfigAPI.Consumer` 2.0.1 has these exact source-package dependencies:

- `Mz.ApiProtocol` 0.3.0
- `Mz.SemanticVersioning` 0.2.0

Space Engineers API assemblies used by the storage adapter are game/runtime references, not SELibs package dependencies.

## Install

From the consuming mod root:

    selibs add Mz.ConfigAPI.Consumer@2.0.1

SELibs installs this package under the consuming mod's `Data/Scripts/.../Libraries` tree together with its exact transitive source dependencies.

## Compatibility

Consumer package version: `2.0.1`

Minimum ConfigAPI provider API version: `2.0.0`

The consumer accepts newer compatible provider API versions and validates the required endpoint contract when connecting.

## Ownership

`Consumer/Mz.ConfigAPI.Consumer` in the ConfigAPI repository is the canonical package source.

A published SELibs component contains only:

    Libraries/Mz.ConfigAPI.Consumer/

ConfigAPI provider source is never included in the consumer package.
