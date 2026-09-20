# Mz.CommandAPI.Consumer guide

This guide covers the normal lifecycle for a Space Engineers mod that registers
commands through `Mz.CommandAPI.Consumer`.

The consumer facade is an ordinary C# library. It is not a session component.
The consuming mod owns startup and unload and keeps the client alive for as long
as its commands should remain registered.

## 1. Create the client

Create one `CommandApiClient` for the consuming mod.

The identity passed to the constructor describes the consuming mod, not
CommandAPI:

    private CommandApiClient _commandApi;

    private void CreateCommandApi()
    {
        _commandApi = new CommandApiClient(
            new SpaceEngineersModMessageBus(),
            "Example.Mod",
            "Example Mod",
            new SemanticVersion(1, 0, 0),
            true,
            "Registers Example Mod commands."
        );
    }

The constructor arguments are:

1. the ModAPI message bus;
2. a stable consuming-mod ID;
3. the consuming mod's display name;
4. the consuming mod's version;
5. whether CommandAPI is a required dependency;
6. a human-readable dependency description.

The facade internally requests a provider API version at least as new as
`ApiVersionFile.MinimumProviderApiVersion`.

It does not impose a hardcoded future-version ceiling. The exact
`RegisterCommand` endpoint delegate type is still validated when a provider is
discovered.

## 2. Subscribe to lifecycle events

Subscribe before calling `Start()` because discovery may complete immediately
when CommandAPI is already running.

    _commandApi.Connected += OnCommandApiConnected;
    _commandApi.Disconnected += OnCommandApiDisconnected;
    _commandApi.RegistrationFailed += OnCommandRegistrationFailed;

    _commandApi.Start();

`Connected` runs after a compatible provider has been discovered, the
`RegisterCommand` endpoint has been validated, and pending registrations have
been activated.

`Disconnected` runs after the provider disappears.

`RegistrationFailed` reports a logical registration that could not be attached
to the current provider.

`LastError` exposes the latest facade or discovery error.

## 3. Register a command

A registration combines command metadata with a typed handler.

    private CommandRegistrationHandle _reloadCommand;

    private void RegisterCommands()
    {
        var registration = new CommandRegistration(
            "/example",
            "reload",
            CommandExecutionLocation.Server,
            aliases: new[] { "refresh" },
            shortDescription: "Reloads Example Mod state.",
            helpText: "Reloads Example Mod state from its current configuration.",
            usage: "reload",
            category: "Example Mod",
            permissionRequirement: 0
        );

        _reloadCommand = _commandApi.Register(registration, HandleReload);
    }

    private CommandResponse HandleReload(CommandRequest request)
    {
        return new CommandResponse(
            true,
            "Reload complete",
            "Example Mod state was reloaded.",
            new[] { "Requested by: " + request.RequesterDisplayName },
            CommandSeverity.Success
        );
    }

`Register()` returns immediately with a `CommandRegistrationHandle`.

If CommandAPI is already connected, the facade attempts to activate the
registration immediately.

If CommandAPI is unavailable, the registration remains pending and is
automatically attached after a compatible provider connects.

## 4. Prefixes, names, and aliases

Every command has a top-level prefix such as:

    /example

and a canonical command name such as:

    reload

which produces:

    /example reload

Prefixes must begin with `/` and cannot contain whitespace.

Command names and aliases are scoped to their prefix. Multiple mods can use the
same prefix when their command names and aliases do not collide.

For example, these can coexist:

    /config reload
    /config export

A second registration of the same command name, or an alias that collides with
an existing name or alias under the same prefix, is rejected by the provider.

## 5. Execution location

Public `CommandExecutionLocation` registrations support:

- `Client`
- `Server`
- `Either`

The enum also contains `Internal`, which is reserved for CommandAPI itself and
is rejected by the public consumer facade and provider registration path.

Use `Server` when the command changes authoritative game state or depends on
trusted server-derived requester information.

Use `Client` for commands whose work belongs only to the local client.

Use `Either` when the command can execute on the submission side without
requiring authoritative server execution.

The handler can inspect `CommandRequest.IsServer` to see which side is
executing it.

## 6. Request data

A `CommandRequest` contains:

    request.RequestId
    request.RequesterSteamId
    request.RequesterIdentityId
    request.RequesterDisplayName
    request.PermissionLevel
    request.IsServer
    request.Prefix
    request.CommandName
    request.Arguments

`Arguments` contains only the parsed arguments after the prefix and command
name.

For example:

    /example echo one two

for an `echo` registration under `/example` produces arguments equivalent to:

    new[] { "one", "two" }

Requester identity and permission information for server execution is supplied
by CommandAPI's execution context rather than parsed from user text.

## 7. Return structured results

Handlers return `CommandResponse`.

A successful response can contain a title, summary, detail lines, and severity:

    return new CommandResponse(
        true,
        "Export complete",
        "The configuration was exported.",
        new[]
        {
            "Entries: " + count,
            "Requested by: " + request.RequesterDisplayName
        },
        CommandSeverity.Success
    );

A failed response can include a usage hint:

    return new CommandResponse(
        false,
        "Invalid arguments",
        "Expected exactly one profile name.",
        severity: CommandSeverity.Error,
        usageHint: request.Prefix + " profile <name>"
    );

Available severities are:

- `Information`
- `Success`
- `Warning`
- `Error`

A handler must return a non-null `CommandResponse`.

## 8. Registration lifetime

Keep the returned `CommandRegistrationHandle` for as long as the command should
exist.

    _reloadCommand?.Dispose();
    _reloadCommand = null;

Disposing a handle removes that logical registration from the client and calls
the provider's unregister action when the registration is currently active.

The handle exposes:

- `Registration`
- `IsActive`
- `IsDisposed`
- `LastError`

`IsActive` means the logical registration is currently attached to a connected
provider.

## 9. Provider reconnects

Logical registrations belong to the `CommandApiClient`, not to one provider
connection.

When the provider disconnects, active provider registrations are released but
their logical handles remain.

When a compatible provider connects again, the facade attempts to reactivate
every pending registration.

To explicitly discard the current provider and search again:

    _commandApi.Rediscover();

To request discovery without first disconnecting:

    _commandApi.RequestDiscovery();

## 10. Stop versus dispose

`Stop()` releases active provider registrations, clears the current connection,
and stops ApiProtocol discovery:

    _commandApi.Stop();

The same client can later be started again:

    _commandApi.Start();

`Dispose()` permanently closes the client:

    _commandApi.Dispose();
    _commandApi = null;

After disposal the client cannot be restarted.

## 11. Complete session example

A normal session component can own the facade like this:

    using System;
    using Mz.ApiProtocol.SpaceEngineers;
    using Mz.CommandApi;
    using Mz.SemanticVersioning;
    using VRage.Game.Components;

    namespace Example.Mod
    {
        [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
        public sealed class ExampleSession : MySessionComponentBase
        {
            private CommandApiClient _commandApi;
            private CommandRegistrationHandle _pingCommand;

            public override void BeforeStart()
            {
                _commandApi = new CommandApiClient(new SpaceEngineersModMessageBus(), "Example.Mod", "Example Mod", new SemanticVersion(1, 0, 0), true, "Registers Example Mod commands.");

                _commandApi.Connected += OnConnected;
                _commandApi.Disconnected += OnDisconnected;
                _commandApi.RegistrationFailed += OnRegistrationFailed;

                _pingCommand = _commandApi.Register(
                    new CommandRegistration("/example", "ping", CommandExecutionLocation.Server, shortDescription: "Tests Example Mod.", usage: "ping"),
                    HandlePing
                );

                _commandApi.Start();
            }

            protected override void UnloadData()
            {
                if (_commandApi != null)
                {
                    _commandApi.Connected -= OnConnected;
                    _commandApi.Disconnected -= OnDisconnected;
                    _commandApi.RegistrationFailed -= OnRegistrationFailed;
                }

                _pingCommand?.Dispose();
                _pingCommand = null;

                _commandApi?.Dispose();
                _commandApi = null;
            }

            private static CommandResponse HandlePing(CommandRequest request)
            {
                return new CommandResponse(true, "Pong", "Command executed on " + (request.IsServer ? "server." : "client."), severity: CommandSeverity.Success);
            }

            private static void OnConnected()
            {
            }

            private static void OnDisconnected()
            {
            }

            private static void OnRegistrationFailed(CommandRegistration registration, Exception exception)
            {
            }
        }
    }

Downstream code does not need to know the CommandAPI provider's API ID,
endpoint name, raw delegate type, discovery messages, or dictionary payload
format. Those details remain inside `Mz.CommandAPI.Consumer`.
