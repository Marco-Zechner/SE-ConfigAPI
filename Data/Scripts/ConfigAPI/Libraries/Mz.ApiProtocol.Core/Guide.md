# Mz.ApiProtocol copy-paste example

`Mz.ApiProtocol` can be used directly by any modder, but an API should normally hide those details from downstream consumers.

Install `Mz.ApiProtocol@0.2.2` in the provider mod and in every consuming mod
that copies the provider-authored facade.

The recommended workflow has three files:

1. The API provider's session component.
2. A provider-authored consumer facade that the API author distributes.
3. A small example showing how another mod uses that facade.

The facade is an ordinary C# class, not a `MySessionComponentBase`. It owns discovery, compatibility checks, endpoint delegates, readiness state, reconnection state, and cleanup. A downstream modder installs `Mz.ApiProtocol`, copies the facade file, and calls normal strongly-typed methods.

## 1. Provider session component

The API author places this file in the provider mod.

```csharp
using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Example.ApiProviderMod
{
    /// <summary>
    /// Complete example mod that exposes a small Echo API.
    ///
    /// Copy this entire file into the provider mod. Install Mz.ApiProtocol in
    /// that mod before compiling it.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ExampleApiProviderSession : MySessionComponentBase
    {
        // These values form the public contract. Consumers must use the same
        // API ID, endpoint names, and exact delegate types.
        public const string ApiId = "Example.EchoApi";
        public const string EchoEndpoint = "Echo";
        public const string AddEndpoint = "Add";

        private ApiDiscoveryProvider _provider;

        public override void BeforeStart()
        {
            var providerIdentity = new ApiModIdentity(
                "example.echo-api-provider",
                "Example Echo API Provider",
                new SemanticVersion(1, 0, 0)
            );

            var apiDescriptor = new ApiDescriptor(ApiId, new SemanticVersion(1, 0, 0));

            var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                {
                    EchoEndpoint,
                    new Func<string, string>(Echo)
                },
                {
                    AddEndpoint,
                    new Func<int, int, int>(Add)
                }
            };

            _provider = new ApiDiscoveryProvider(
                new SpaceEngineersModMessageBus(),
                providerIdentity,
                apiDescriptor,
                endpoints
            );

            _provider.ConsumerObserved += OnConsumerObserved;
            _provider.WireIncompatibilityObserved += OnWireIncompatibilityObserved;

            // Start registers the discovery handler and immediately announces
            // that this provider is available.
            _provider.Start();

            ShowMessage($"Provider started for {ApiId} 1.0.0.");
        }

        protected override void UnloadData()
        {
            if (_provider == null)
                return;

            try
            {
                // Dispose sends a withdrawal and unregisters the exact
                // discovery handler.
                _provider.Dispose();
            }
            catch (Exception exception)
            {
                ShowMessage("Provider shutdown failed: " + exception.Message);
            }
            finally
            {
                _provider = null;
            }
        }

        private static string Echo(string value)
        {
            return "Provider echoed: " + (value ?? string.Empty);
        }

        private static int Add(int left, int right)
        {
            return left + right;
        }

        private static void OnConsumerObserved(ApiConsumerObservedEventArgs eventArgs)
        {
            ShowMessage($"Observed consumer {eventArgs.Consumer.DisplayName}. Compatibility: {eventArgs.CompatibilityStatus}.");
        }

        private static void OnWireIncompatibilityObserved(ApiWireIncompatibilityEventArgs eventArgs)
        {
            ShowMessage($"Incompatible discovery protocol from {eventArgs.RemoteMod.DisplayName}: {eventArgs.CompatibilityStatus}.");
        }

        private static void ShowMessage(string message)
        {
            // Replace this helper with the provider mod's normal logger when
            // moving beyond the example.
            if (MyAPIGateway.Utilities == null)
                return;

            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            MyAPIGateway.Utilities.ShowMessage("API Provider", message);
        }
    }
}
```

The provider author changes the API ID, version, endpoint names, endpoint delegate types, and endpoint implementations to describe the real API.

## 2. Consumer facade distributed by the API author

The API author also writes and distributes this file. Consumers should be able to copy it unchanged.

It intentionally does not derive from `MySessionComponentBase`. The consuming mod controls lifecycle by calling `Init` and `Close` from its own session component.

```csharp
using System;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace Example.EchoApi
{
    /// <summary>
    /// Consumer-side wrapper distributed by the Echo API author.
    ///
    /// A consuming mod copies this file unchanged and uses the public methods
    /// below. All ApiProtocol discovery and endpoint details stay private.
    ///
    /// This is an ordinary static class, not a session component. The
    /// consuming mod calls Init during its own startup and Close during unload.
    /// </summary>
    public static class ExampleEchoApi
    {
        private const string ApiId = "Example.EchoApi";
        private const string EchoEndpoint = "Echo";
        private const string AddEndpoint = "Add";

        private static readonly ApiEndpointContract EndpointContract =
            new ApiEndpointContract(
                new[]
                {
                    new ApiEndpointRequirement(
                        EchoEndpoint,
                        typeof(Func<string, string>)
                    ),
                    new ApiEndpointRequirement(
                        AddEndpoint,
                        typeof(Func<int, int, int>)
                    )
                }
            );

        private static ApiDiscoveryConsumer _consumer;
        private static Func<string, string> _echo;
        private static Func<int, int, int> _add;

        /// <summary>
        /// Raised whenever a compatible provider has connected and all
        /// required endpoint delegates are ready to call.
        /// </summary>
        public static event Action Ready;

        /// <summary>
        /// Raised when a ready provider disconnects or the discovered provider
        /// cannot satisfy this wrapper's endpoint contract.
        /// </summary>
        public static event Action Unavailable;

        /// <summary>
        /// Gets whether Init has created the underlying discovery consumer.
        /// </summary>
        public static bool IsInitialized
        {
            get { return _consumer != null; }
        }

        /// <summary>
        /// Gets whether all public API methods are currently callable.
        /// </summary>
        public static bool IsReady
        {
            get
            {
                return _echo != null
                    && _add != null;
            }
        }

        /// <summary>
        /// Gets the latest human-readable connection or contract error.
        /// </summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Starts discovery for the API.
        ///
        /// The identity arguments describe the consuming mod, not the API
        /// provider. Subscribe to Ready and Unavailable before calling Init,
        /// because an already-running provider may connect synchronously.
        /// </summary>
        public static void Init(
            string consumerModId,
            string consumerDisplayName,
            int consumerVersionMajor,
            int consumerVersionMinor,
            int consumerVersionPatch
        )
        {
            if (_consumer != null)
                return;

            var consumerIdentity = new ApiModIdentity(
                consumerModId,
                consumerDisplayName,
                new SemanticVersion(
                    consumerVersionMajor,
                    consumerVersionMinor,
                    consumerVersionPatch
                )
            );

            var requirement = new ApiRequirement(
                ApiId,
                new ApiVersionRange(
                    new SemanticVersion(1, 0, 0),
                    new SemanticVersion(2, 0, 0)
                )
            );

            var dependency = new ApiDependencyDescriptor(
                consumerIdentity,
                requirement,
                ApiDependencyKind.Required,
                "Uses the Example Echo API"
            );

            var consumer = new ApiDiscoveryConsumer(
                new SpaceEngineersModMessageBus(),
                dependency
            );

            consumer.Connected += OnConnected;
            consumer.Disconnected += OnDisconnected;
            consumer.WireIncompatibilityObserved +=
                OnWireIncompatibilityObserved;

            _consumer = consumer;
            LastError = null;

            try
            {
                consumer.Start();
                consumer.RequestDiscovery();
            }
            catch
            {
                consumer.Connected -= OnConnected;
                consumer.Disconnected -= OnDisconnected;
                consumer.WireIncompatibilityObserved -=
                    OnWireIncompatibilityObserved;

                _consumer = null;
                ClearEndpoints();
                consumer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Releases the discovery listener and all endpoint delegates.
        /// Call this from the consuming mod's unload lifecycle.
        /// </summary>
        public static void Close()
        {
            var consumer = _consumer;

            _consumer = null;
            ClearEndpoints();
            LastError = null;

            if (consumer != null)
            {
                consumer.Connected -= OnConnected;
                consumer.Disconnected -= OnDisconnected;
                consumer.WireIncompatibilityObserved -=
                    OnWireIncompatibilityObserved;

                consumer.Dispose();
            }

            Ready = null;
            Unavailable = null;
        }

        /// <summary>
        /// Disconnects the current provider, if any, and requests discovery
        /// again.
        /// </summary>
        public static void Refresh()
        {
            EnsureInitialized();
            ClearEndpoints();
            LastError = null;
            _consumer.Rediscover();
        }

        /// <summary>
        /// Calls the provider's Echo endpoint.
        /// </summary>
        public static string Echo(string value)
        {
            EnsureReady();
            return _echo(value);
        }

        /// <summary>
        /// Calls the provider's Add endpoint.
        /// </summary>
        public static int Add(int left, int right)
        {
            EnsureReady();
            return _add(left, right);
        }

        private static void OnConnected(
            ApiConnectedEventArgs eventArgs
        )
        {
            try
            {
                EndpointContract.EnsureCompatible(
                    eventArgs.Connection
                );

                Func<string, string> echo;
                Func<int, int, int> add;

                if (
                    !eventArgs.Connection.TryGetEndpoint(
                        EchoEndpoint,
                        out echo
                    )
                ) {
                    throw new InvalidOperationException(
                        "The Echo endpoint could not be loaded."
                    );
                }

                if (
                    !eventArgs.Connection.TryGetEndpoint(
                        AddEndpoint,
                        out add
                    )
                ) {
                    throw new InvalidOperationException(
                        "The Add endpoint could not be loaded."
                    );
                }

                _echo = echo;
                _add = add;
                LastError = null;

                RaiseEvent(Ready);
            }
            catch (Exception exception)
            {
                ClearEndpoints();

                LastError =
                    "The connected provider has an invalid endpoint "
                    + "contract: "
                    + exception.Message;

                RaiseEvent(Unavailable);
            }
        }

        private static void OnDisconnected(
            ApiDisconnectedEventArgs eventArgs
        )
        {
            bool wasReady = IsReady;

            ClearEndpoints();

            LastError =
                "The API provider disconnected: "
                + eventArgs.Reason
                + ".";

            if (wasReady)
                RaiseEvent(Unavailable);
        }

        private static void OnWireIncompatibilityObserved(
            ApiWireIncompatibilityEventArgs eventArgs
        )
        {
            LastError =
                "The provider uses an incompatible discovery protocol: "
                + eventArgs.CompatibilityStatus
                + ".";

            RaiseEvent(Unavailable);
        }

        private static void ClearEndpoints()
        {
            _echo = null;
            _add = null;
        }

        private static void EnsureInitialized()
        {
            if (_consumer == null)
            {
                throw new InvalidOperationException(
                    "Call ExampleEchoApi.Init before using the API."
                );
            }
        }

        private static void EnsureReady()
        {
            EnsureInitialized();

            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "The Example Echo API is not ready. "
                    + (LastError ?? "No compatible provider is connected.")
                );
            }
        }

        private static void RaiseEvent(Action callback)
        {
            if (callback == null)
                return;

            Delegate[] handlers = callback.GetInvocationList();

            for (int index = 0; index < handlers.Length; index++)
            {
                try
                {
                    ((Action)handlers[index])();
                }
                catch (Exception exception)
                {
                    LastError =
                        "An API wrapper event subscriber failed: "
                        + exception.Message;
                }
            }
        }
    }
}
```

The facade's public surface is the API that downstream modders use:

- `Init(...)` starts discovery.
- `IsReady` reports whether methods can currently be called.
- `Ready` fires after connection and endpoint validation.
- `Unavailable` fires when a ready provider disappears or validation fails.
- `Echo(...)` and `Add(...)` are normal strongly-typed API methods.
- `Refresh()` requests discovery again.
- `Close()` releases the message-bus registration during unload.

For a real API, the provider author renames `ExampleEchoApi`, exposes methods matching the real feature set, and keeps the raw endpoint delegates private.

## 3. What a consuming mod writes

The downstream mod installs `Mz.ApiProtocol`, copies the provider's facade file, and uses it from its own lifecycle code.

The consuming mod does not need to understand `ApiDiscoveryConsumer`, API IDs, endpoint names, delegate dictionaries, version ranges, or endpoint contract validation.

```csharp
using Example.EchoApi;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Example.ApiConsumerMod
{
    /// <summary>
    /// Minimal example showing what a downstream modder writes after copying
    /// ExampleEchoApi.cs into their mod.
    ///
    /// This file contains no ApiProtocol discovery or endpoint code.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ExampleApiConsumerUsageSession :
        MySessionComponentBase
    {
        public override void BeforeStart()
        {
            // Subscribe before Init. Discovery can complete synchronously when
            // the provider is already running.
            ExampleEchoApi.Ready += OnApiReady;
            ExampleEchoApi.Unavailable += OnApiUnavailable;

            ExampleEchoApi.Init(
                "example.echo-api-consumer",
                "Example Echo API Consumer",
                1,
                0,
                0
            );

            // IsReady may already be true when Init returns. The Ready event
            // above has already run in that case.
            if (!ExampleEchoApi.IsReady)
            {
                ShowMessage(
                    "Waiting for the Example Echo API provider."
                );
            }
        }

        protected override void UnloadData()
        {
            ExampleEchoApi.Ready -= OnApiReady;
            ExampleEchoApi.Unavailable -= OnApiUnavailable;
            ExampleEchoApi.Close();
        }

        private static void OnApiReady()
        {
            // From this point onward the consuming mod calls normal,
            // strongly-typed methods. It does not deal with discovery,
            // endpoint names, delegates, or compatibility checks.
            string echoResult = ExampleEchoApi.Echo(
                "Hello from the consuming mod"
            );

            int sumResult = ExampleEchoApi.Add(20, 22);

            ShowMessage(
                echoResult
                + ". 20 + 22 = "
                + sumResult
                + "."
            );
        }

        private static void OnApiUnavailable()
        {
            ShowMessage(
                ExampleEchoApi.LastError
                ?? "The Example Echo API is unavailable."
            );
        }

        private static void ShowMessage(string message)
        {
            if (MyAPIGateway.Utilities == null)
                return;

            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            MyAPIGateway.Utilities.ShowMessage(
                "API Consumer",
                message
            );
        }
    }
}
```

Subscribe to `Ready` before calling `Init`, because a provider that is already running may answer the discovery request synchronously.

Call `Close` during unload. After `Ready`, the rest of the mod calls methods such as `ExampleEchoApi.Echo(...)` and `ExampleEchoApi.Add(...)`.

## Contract values owned by the API author

These values must agree between the provider and the distributed facade:

- API ID: `Example.EchoApi`
- Supported API version range
- Endpoint name: `Echo`
- Endpoint delegate type: `Func<string, string>`
- Endpoint name: `Add`
- Endpoint delegate type: `Func<int, int, int>`

The downstream mod supplies only its own mod identity and lifecycle calls. Advanced consumers may still use `ApiDiscoveryConsumer` directly, but that should not be required for normal use.
