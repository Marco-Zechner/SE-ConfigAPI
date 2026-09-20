# Mz.Networking copy-paste example

The package README explains envelopes, trusted sender validation, handlers, relay modes, and lifecycle ownership. This guide provides a complete server file and client file for a small multiplayer ping test.

## Setup

Install `Mz.Networking@0.2.0` in one Space Engineers mod. Copy both files below into that same mod. This example deliberately uses one fixed channel; see the package README for NetworkManager-managed and forced-channel sessions.

The example is intended for a multiplayer server with at least one remote client:

1. Start or join a multiplayer world with the mod enabled.
2. Join from a second game instance or another computer.
3. On the remote client, type `/netping hello`.
4. The authoritative server responds directly to that client.

The host runs the server component. The client component deliberately does not register on the host because both components would otherwise attempt to own the same secure-message channel.

## Server file

Copy the complete file below into the mod.

```csharp
using System;
using System.Text;
using Mz.Networking;
using Mz.Networking.SpaceEngineers;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Example.NetworkingMod
{
    /// <summary>
    /// Complete authoritative-server half of the networking example.
    ///
    /// Copy this file and ExampleNetworkClientSession.cs into the same mod.
    /// Install Mz.Networking in that mod before compiling it.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ExampleNetworkServerSession : MySessionComponentBase
    {
        // Pick one channel that is unique to the mod. The client file must use
        // exactly the same value.
        private const ushort ChannelId = 45123;

        // Stable network identity; this must also match the client file.
        private const string NetworkId = "example.networking";

        // Message types are case-sensitive and must match the client file.
        private const string PingRequestMessage = "example.networking.ping.request";

        private const string PingResponseMessage = "example.networking.ping.response";

        private SpaceEngineersNetworkSession _network;
        private NetworkMessageSubscription _pingSubscription;

        public override void BeforeStart()
        {
            if (MyAPIGateway.Multiplayer == null || !MyAPIGateway.Multiplayer.IsServer)
                return;

            _network = new SpaceEngineersNetworkSession(ChannelId, NetworkId, OnReceiveFailure);

            _pingSubscription = _network.Endpoint.RegisterHandler(
                PingRequestMessage,
                OnPingRequest
            );

            ShowMessage($"Server handler registered on channel {ChannelId}.");
        }

        protected override void UnloadData()
        {
            if (_pingSubscription != null)
            {
                _pingSubscription.Dispose();
                _pingSubscription = null;
            }

            if (_network != null)
            {
                _network.Dispose();
                _network = null;
            }
        }

        private void OnPingRequest(NetworkReceiveContext context)
        {
            string requestText = Encoding.UTF8.GetString(context.Envelope.Payload);

            // The server has already validated this ID against the trusted
            // transport sender.
            ulong requestingPeerId = context.Envelope.OriginalSenderId;

            string responseText = "Pong from the authoritative server. You sent: " + requestText;

            _network.Endpoint.SendToPlayer(PingResponseMessage, Encoding.UTF8.GetBytes(responseText), requestingPeerId);

            ShowMessage($"Answered peer {requestingPeerId}: {requestText}");
        }

        private static void OnReceiveFailure(SpaceEngineersNetworkReceiveFailure failure)
        {
            ShowMessage($"Rejected packet on channel {failure.ChannelId} from peer {failure.SenderPeerId}: {failure.Exception.Message}");
        }

        private static void ShowMessage(string message)
        {
            // Replace this with the mod's normal logger for production use.
            if (MyAPIGateway.Utilities == null)
                return;

            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            MyAPIGateway.Utilities.ShowMessage("Network Server", message);
        }
    }
}
```

## Client file

Copy the complete file below into the same mod.

```csharp
using System;
using System.Text;
using Mz.Networking;
using Mz.Networking.SpaceEngineers;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Example.NetworkingMod
{
    /// <summary>
    /// Complete remote-client half of the networking example.
    ///
    /// Copy this file and ExampleNetworkServerSession.cs into the same mod.
    /// Join a multiplayer server with a remote client and type /netping.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ExampleNetworkClientSession : MySessionComponentBase
    {
        // These values must match ExampleNetworkServerSession exactly.
        private const ushort ChannelId = 45123;

        private const string NetworkId = "example.networking";

        private const string PingRequestMessage = "example.networking.ping.request";

        private const string PingResponseMessage = "example.networking.ping.response";

        private const string ChatCommand = "/netping";

        private SpaceEngineersNetworkSession _network;
        private NetworkMessageSubscription _responseSubscription;
        private bool _chatHandlerRegistered;

        public override void BeforeStart()
        {
            // The host runs the server component. This component is only for a
            // remote, non-dedicated multiplayer client.
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            if (MyAPIGateway.Multiplayer == null || MyAPIGateway.Multiplayer.IsServer)
                return;

            _network = new SpaceEngineersNetworkSession(ChannelId, NetworkId, OnReceiveFailure);

            _responseSubscription = _network.Endpoint.RegisterHandler(
                PingResponseMessage,
                OnPingResponse
            );

            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;

            _chatHandlerRegistered = true;

            ShowMessage($"Type {ChatCommand} followed by optional text.");
        }

        protected override void UnloadData()
        {
            if (_chatHandlerRegistered && MyAPIGateway.Utilities != null)
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;

            _chatHandlerRegistered = false;

            if (_responseSubscription != null)
            {
                _responseSubscription.Dispose();
                _responseSubscription = null;
            }

            if (_network != null)
            {
                _network.Dispose();
                _network = null;
            }
        }

        private void OnMessageEntered(string message, ref bool sendToOthers)
        {
            if (message == null)
                return;

            bool exactCommand = string.Equals(message, ChatCommand, StringComparison.OrdinalIgnoreCase);

            bool commandWithText = message.StartsWith(ChatCommand + " ", StringComparison.OrdinalIgnoreCase);

            if (!exactCommand && !commandWithText)
                return;

            // Prevent the test command from being sent to normal chat.
            sendToOthers = false;

            string text = exactCommand
                ? "Hello from the remote client"
                : message.Substring(ChatCommand.Length).Trim();

            if (text.Length == 0)
                text = "Hello from the remote client";

            _network.Endpoint.SendToServer(
                PingRequestMessage,
                Encoding.UTF8.GetBytes(text)
            );

            ShowMessage("Ping sent: " + text);
        }

        private static void OnPingResponse(NetworkReceiveContext context)
        {
            // Clients only accept packets whose immediate transport sender was
            // identified as the authoritative server.
            string responseText = Encoding.UTF8.GetString(context.Envelope.Payload);

            ShowMessage(responseText);
        }

        private static void OnReceiveFailure(SpaceEngineersNetworkReceiveFailure failure)
        {
            ShowMessage($"Rejected packet on channel {failure.ChannelId} from peer {failure.SenderPeerId}: {failure.Exception.Message}");
        }

        private static void ShowMessage(string message)
        {
            if (MyAPIGateway.Utilities == null)
                return;

            MyAPIGateway.Utilities.ShowMessage("Network Client", message);
        }
    }
}
```

## Values that must match

The following values must be identical in both files:

- Secure-message channel: `45123`
- Network ID: `example.networking`
- Request message type: `example.networking.ping.request`
- Response message type: `example.networking.ping.response`

Choose a channel that is unique to the real mod before publishing it. Message types are case-sensitive application protocol identifiers and should remain stable after release.

The server reads the requesting player from `context.Envelope.OriginalSenderId`. On server receipt, Mz.Networking has already validated that value against the trusted transport sender, so the example never trusts a player ID supplied inside the payload.
