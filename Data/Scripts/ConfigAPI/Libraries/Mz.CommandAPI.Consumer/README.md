# Mz.CommandAPI.Consumer

`Mz.CommandAPI.Consumer` is the typed consumer facade for the
`MarcoZechner.CommandAPI` Space Engineers mod API.

Use this package from mods that want to register commands with CommandAPI.
Consumers work with `CommandApiClient`, `CommandRegistration`,
`CommandRequest`, and `CommandResponse`; the facade owns ApiProtocol discovery,
endpoint validation, payload conversion, reconnection, registration cleanup,
and disposal.

For lifecycle details and complete examples, see [Guide.md](Guide.md).

## Install

### Install with SELibs

[SELibs](https://github.com/Marco-Zechner/selibs) is the recommended way to
install the consumer facade and its exact dependencies.

From the consuming mod root:

    selibs init
    selibs add Mz.CommandAPI.Consumer

Skip `selibs init` when the mod already contains `selibs.json`.

SELibs installs `Mz.CommandAPI.Consumer` and resolves the exact dependency
versions required by that package release.

`Mz.CommandAPI.Consumer` 1.2.0 declares these exact SELibs dependencies:

- `Mz.ApiProtocol` 0.3.0
- `Mz.SemanticVersioning` 0.2.0

Inspect the installed dependency graph with:

    selibs status

The CommandAPI provider implementation and its `Mz.Networking` dependency are
not installed into consumer mods.

### Install manually

From the release component archive, copy the complete consumer facade folder
into the consuming mod:

    Libraries/Mz.CommandAPI.Consumer
        ->
    Data/Scripts/ExampleMod/Libraries/Mz.CommandAPI.Consumer

Also install the exact dependency graph declared by the matching
`Mz.CommandAPI.Consumer` release manifest:

    Data/Scripts/ExampleMod/Libraries/Mz.ApiProtocol.Core
    Data/Scripts/ExampleMod/Libraries/Mz.ApiProtocol.SpaceEngineers
    Data/Scripts/ExampleMod/Libraries/Mz.SemanticVersioning

Keep each library as a complete sibling folder and compile all contained `.cs`
files as part of the mod.

Do not mix dependency versions from different package releases. When installing
manually, use the dependency versions recorded in the selected
`Mz.CommandAPI.Consumer` release manifest.

## Basic usage

Create one `CommandApiClient` for the consuming mod, register commands, then
start discovery:

    private CommandApiClient _commandApi;
    private CommandRegistrationHandle _pingCommand;

    public override void BeforeStart()
    {
        _commandApi = new CommandApiClient(new SpaceEngineersModMessageBus(), "Example.Mod", "Example Mod", new SemanticVersion(1, 0, 0), true, "Registers Example Mod commands.");

        _pingCommand = _commandApi.Register(
            new CommandRegistration("/example", "ping", CommandExecutionLocation.Server, shortDescription: "Tests the Example Mod command."),
            request => new CommandResponse(true, "Pong", "Handled by Example Mod.", severity: CommandSeverity.Success)
        );

        _commandApi.Start();
    }

    protected override void UnloadData()
    {
        _pingCommand?.Dispose();
        _pingCommand = null;

        _commandApi?.Dispose();
        _commandApi = null;
    }

Registrations may be created before a provider is available. The facade keeps
those logical registrations and activates them when a compatible CommandAPI
provider connects.

## Main types

`CommandApiClient` owns discovery and all registrations belonging to one
consumer mod.

`CommandRegistration` describes a command:

- top-level prefix;
- canonical name;
- optional aliases;
- execution location;
- short description and help text;
- usage and category metadata;
- permission requirement.

`CommandRequest` contains the parsed command plus trusted requester context:

- request ID;
- requester Steam ID;
- requester identity ID;
- requester display name;
- permission level;
- execution-side flag;
- matched prefix;
- command name;
- arguments.

`CommandResponse` returns structured output to the requester:

- success state;
- title;
- summary;
- optional detail lines;
- severity;
- optional usage hint.

`CommandRegistrationHandle` represents one logical registration. Dispose it to
unregister that command without disposing the entire client.

## Connection state

Useful `CommandApiClient` members include:

- `IsStarted`
- `IsConnected`
- `ProviderModVersion`
- `ProviderApiVersion`
- `LastError`
- `Connected`
- `Disconnected`
- `RegistrationFailed`
- `RequestDiscovery()`
- `Rediscover()`
- `Stop()`

`Start()` begins discovery and requests a provider when necessary.

`Rediscover()` disconnects from the current provider and starts discovery
again.

`Stop()` releases active provider registrations and stops discovery while
leaving the client object available for a later `Start()`.

`Dispose()` permanently releases the client and its registrations.

## Version

The consumer package version and minimum supported provider API version are
defined by `ApiVersionFile.cs`.

The consumer package version is independent from the installed CommandAPI mod
version. Provider discovery also validates the exact `RegisterCommand` endpoint
delegate type before the client becomes connected.
