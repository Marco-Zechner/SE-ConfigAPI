# Mz.ApiProtocol

`Mz.ApiProtocol` lets Space Engineers mods discover versioned APIs exposed by
other mods through the ModAPI message bus.

The package contains:

- `Mz.ApiProtocol.Core`  -  identities, version requirements, endpoint
  contracts, connections, and wire messages.
- `Mz.ApiProtocol.SpaceEngineers`  -  provider and consumer lifecycle classes
  backed by Space Engineers mod messages.

`Mz.SemanticVersioning` is installed automatically as an exact transitive
dependency.

For a complete provider session, provider-authored consumer facade, and
minimal downstream usage component, see the [copy-paste example](Guide.md).

## Install

### Install with SELibs

[SELibs](https://github.com/Marco-Zechner/selibs) is a source-library manager
for Space Engineers mods. Its repository contains the installer, command
reference, and project setup instructions.

After installing SELibs, run these commands from the root of the mod project:

```shell
    selibs init
    selibs add Mz.ApiProtocol@0.2.2
```

Skip `selibs init` when the project already contains `selibs.json`.

SELibs installs both ApiProtocol components and its exact
`Mz.SemanticVersioning` dependency. It records the complete dependency graph
and managed file checksums. Inspect that state with:

```shell
    selibs status
```

### Install manually

To install without SELibs, use source from the matching release tags and copy
these complete folders:

```text
    src/Mz.ApiProtocol.Core
    src/Mz.ApiProtocol.SpaceEngineers
    src/Mz.SemanticVersioning
```

Place them as sibling folders under the mod's script library directory:

```text
    Data/Scripts/ExampleMod/Libraries/Mz.ApiProtocol.Core
    Data/Scripts/ExampleMod/Libraries/Mz.ApiProtocol.SpaceEngineers
    Data/Scripts/ExampleMod/Libraries/Mz.SemanticVersioning
```

For `Mz.ApiProtocol` 0.2.2, use `Mz.SemanticVersioning` 0.1.1. Compile all
contained `.cs` files as part of the mod. Do not substitute a different
dependency version unless the package manifest for the selected ApiProtocol
release explicitly requires it.

## API identity and compatibility

API IDs and endpoint names are case-sensitive and should remain stable across
releases.

A provider describes the API version it exposes:

```csharp
    using Mz.ApiProtocol;
    using Mz.SemanticVersioning;

    var descriptor = new ApiDescriptor(
        "example.echo",
        new SemanticVersion(1, 0, 0)
    );
```

A consumer declares the accepted half-open version range:

```csharp
    var requirement = new ApiRequirement(
        "example.echo",
        new ApiVersionRange(
            new SemanticVersion(1, 0, 0),
            new SemanticVersion(2, 0, 0)
        )
    );
```

This range accepts versions from `1.0.0` inclusive to `2.0.0` exclusive.

## Expose an API

Create the provider during the active session lifecycle and keep it alive
until unload.

```csharp
    using System;
    using System.Collections.Generic;
    using Mz.ApiProtocol;
    using Mz.ApiProtocol.SpaceEngineers;
    using Mz.SemanticVersioning;

    private ApiDiscoveryProvider _provider;

    private void StartApiProvider()
    {
        var bus = new SpaceEngineersModMessageBus();

        var modIdentity = new ApiModIdentity(
            "example.provider-mod",
            "Example Provider",
            new SemanticVersion(1, 0, 0)
        );

        var apiDescriptor = new ApiDescriptor(
            "example.echo",
            new SemanticVersion(1, 0, 0)
        );

        var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
        {
            {
                "Echo",
                new Func<string, string>(Echo)
            }
        };

        _provider = new ApiDiscoveryProvider(
            bus,
            modIdentity,
            apiDescriptor,
            endpoints
        );

        _provider.Start();
    }

    private string Echo(string value)
    {
        return value;
    }
```

`Start` registers the discovery handler and immediately broadcasts an
announcement. `Stop` broadcasts a withdrawal and unregisters the handler.

## Consume an API

A consumer listens for compatible announcements and may explicitly request
discovery.

```csharp
    using System;
    using Mz.ApiProtocol;
    using Mz.ApiProtocol.SpaceEngineers;
    using Mz.SemanticVersioning;

    private ApiDiscoveryConsumer _consumer;
    private Func<string, string> _echo;

    private void StartApiConsumer()
    {
        var bus = new SpaceEngineersModMessageBus();

        var consumerIdentity = new ApiModIdentity(
            "example.consumer-mod",
            "Example Consumer",
            new SemanticVersion(1, 0, 0)
        );

        var requirement = new ApiRequirement(
            "example.echo",
            new ApiVersionRange(
                new SemanticVersion(1, 0, 0),
                new SemanticVersion(2, 0, 0)
            )
        );

        var dependency = new ApiDependencyDescriptor(
            consumerIdentity,
            requirement,
            ApiDependencyKind.Required,
            "Echo command integration"
        );

        _consumer = new ApiDiscoveryConsumer(
            bus,
            dependency
        );

        _consumer.Connected += args =>
        {
            Func<string, string> endpoint;

            if (args.Connection.TryGetEndpoint<Func<string, string>>("Echo", out endpoint))
            {
                _echo = endpoint;
            }
        };

        _consumer.Disconnected += args =>
        {
            _echo = null;
        };

        _consumer.Start();
        _consumer.RequestDiscovery();
    }
```

`Start` only begins listening. Call `RequestDiscovery` to actively request a
provider when no unsolicited announcement has already connected the consumer.

The consumer accepts the first compatible provider. Use `Disconnect` to
release it or `Rediscover` to disconnect and immediately request another
provider.

## Validate endpoint contracts

`ApiEndpointContract` can validate endpoint presence and exact delegate types
before consumer code starts using a connection.

```csharp
    var contract = new ApiEndpointContract(
        new ApiEndpointRequirement(
            "Echo",
            typeof(Func<string, string>)
        )
    );

    contract.EnsureCompatible(connection);
```

Endpoint delegate types must match exactly. A delegate with a different
signature is incompatible even when it has the same endpoint name.

## Diagnostics

Provider and consumer event subscribers do not break the shared message
handler. The first subscriber or processing failure is retained in
`LastError`.

Useful events include:

- `ProviderObserved`
- `Connected`
- `Disconnected`
- `ConsumerObserved`
- `WireIncompatibilityObserved`

Wire-protocol compatibility and API-version compatibility are evaluated
separately.

## Lifecycle

Dispose providers and consumers during mod unload:

```csharp
    _consumer?.Dispose();
    _consumer = null;

    _provider?.Dispose();
    _provider = null;
```

Do not construct the Space Engineers message bus from a static initializer;
the ModAPI utilities must already be active.

## Package version

The released package version is available through:

```csharp
    string packageVersion = Mz.ApiProtocol.LibraryVersionFile.VersionString;
```
