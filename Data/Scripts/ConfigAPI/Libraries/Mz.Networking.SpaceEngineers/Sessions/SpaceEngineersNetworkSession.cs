using System;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Owns one active Space Engineers secure-message registration, which may
    /// remain fixed or move when a managed assignment is accepted, and exposes
    /// its transport-independent network endpoint.
    /// </summary>
    public sealed class SpaceEngineersNetworkSession : IDisposable
    {
        private readonly ISpaceEngineersNetworkGateway _gateway;
        private readonly Action<ushort, byte[], ulong, bool> _secureMessageHandler;
        private readonly Action<SpaceEngineersNetworkReceiveFailure> _receiveFailureHandler;

        private ApiDiscoveryConsumer _networkManagerConsumer;
        private Action _networkManagerUnregister;
        private Guid? _connectedNetworkManagerProviderInstanceId;
        private Guid? _activeNetworkManagerProviderInstanceId;
        private ulong? _activeAssignmentGeneration;
        private Action<ushort, ulong> _activeNetworkManagerConflictReporter;
        private bool _activeAssignmentConflictReported;
        private SpaceEngineersManagedNetworkConfiguration _managedConfiguration;

        private bool _disposed;

        /// <summary>
        /// Creates a compatibility session using the legacy unframed wire.
        /// Receive diagnostics can be observed through <see cref="Diagnostic"/>.
        /// </summary>
        public SpaceEngineersNetworkSession(ushort channelId) 
            : this(new SpaceEngineersNetworkGateway(), channelId, null, false, null) { }

        /// <summary>
        /// Creates a session using the active Space Engineers ModAPI and an
        /// explicit stable application network identity. Receive diagnostics
        /// can be observed through <see cref="Diagnostic"/>.
        /// </summary>
        public SpaceEngineersNetworkSession(ushort channelId, string networkId)
            : this(new SpaceEngineersNetworkGateway(), channelId, networkId, true, null) { }

        /// <summary>
        /// Creates a compatibility session over an explicit gateway using the
        /// legacy unframed wire. Receive diagnostics can be observed through
        /// <see cref="Diagnostic"/>.
        /// </summary>
        public SpaceEngineersNetworkSession(ISpaceEngineersNetworkGateway gateway, ushort channelId)
            : this(gateway, channelId, null, false, null) { }

        /// <summary>
        /// Creates a session over an explicit gateway and stable application
        /// network identity. Receive diagnostics can be observed through
        /// <see cref="Diagnostic"/>.
        /// </summary>
        public SpaceEngineersNetworkSession(ISpaceEngineersNetworkGateway gateway, ushort channelId, string networkId)
            : this(gateway, channelId, networkId, true, null) { }

        /// <summary>
        /// Creates a compatibility session using the legacy unframed wire.
        /// </summary>
        public SpaceEngineersNetworkSession(ushort channelId, Action<SpaceEngineersNetworkReceiveFailure> receiveFailureHandler)
            : this(new SpaceEngineersNetworkGateway(), channelId, null, false, receiveFailureHandler) { }

        /// <summary>
        /// Creates a session using the active Space Engineers ModAPI and an
        /// explicit stable application network identity.
        /// </summary>
        public SpaceEngineersNetworkSession(ushort channelId, string networkId, Action<SpaceEngineersNetworkReceiveFailure> receiveFailureHandler)
            : this(new SpaceEngineersNetworkGateway(), channelId, networkId, true, receiveFailureHandler) { }

        /// <summary>
        /// Creates a compatibility session over an explicit gateway using the
        /// legacy unframed wire.
        /// </summary>
        public SpaceEngineersNetworkSession(
            ISpaceEngineersNetworkGateway gateway, ushort channelId, Action<SpaceEngineersNetworkReceiveFailure> receiveFailureHandler)
            : this(gateway, channelId, null, false, receiveFailureHandler) { }

        /// <summary>
        /// Creates a session over an explicit gateway and stable application
        /// network identity.
        /// </summary>
        public SpaceEngineersNetworkSession(
            ISpaceEngineersNetworkGateway gateway, ushort channelId, string networkId, Action<SpaceEngineersNetworkReceiveFailure> receiveFailureHandler)
            : this(gateway, channelId, networkId, true, receiveFailureHandler) { }

        /// <summary>
        /// Creates a managed session using the active Space Engineers ModAPI.
        /// Forced-channel configuration creates no ApiProtocol message bus.
        /// </summary>
        public SpaceEngineersNetworkSession(SpaceEngineersManagedNetworkConfiguration configuration)
            : this(new SpaceEngineersNetworkGateway(), CreateDefaultMessageBus(configuration), configuration) { }

        /// <summary>
        /// Creates a managed session over an explicit networking gateway.
        /// ApiProtocol setup remains internal, and forced-channel
        /// configuration creates no message bus.
        /// </summary>
        public SpaceEngineersNetworkSession(ISpaceEngineersNetworkGateway gateway, SpaceEngineersManagedNetworkConfiguration configuration)
            : this(gateway, CreateDefaultMessageBus(configuration), configuration) { }

        /// <summary>
        /// Creates a session that starts immediately on its configured
        /// fallback channel and discovers a compatible NetworkManager provider
        /// unless a forced channel is configured.
        /// </summary>
        public SpaceEngineersNetworkSession(
            ISpaceEngineersNetworkGateway gateway, IModMessageBus messageBus, SpaceEngineersManagedNetworkConfiguration configuration)
            : this(gateway, GetManagedInitialChannel(messageBus, configuration), GetManagedNetworkId(configuration), 
                   true, null)
        {
            _managedConfiguration = configuration;
            IsForcedChannel = configuration.ForcedChannel.HasValue;

            if (IsForcedChannel)
                return;

            try
            {
                _networkManagerConsumer = NetworkManagerApiContract.CreateConsumer(messageBus, configuration);

                _networkManagerConsumer.Connected += OnNetworkManagerConnected;

                _networkManagerConsumer.Disconnected += OnNetworkManagerDisconnected;

                _networkManagerConsumer.Start();

                if (!_networkManagerConsumer.IsConnected)
                    _networkManagerConsumer.RequestDiscovery();
            }
            catch (Exception exception)
            {
                NetworkManagerError = exception;
            }
        }
        private SpaceEngineersNetworkSession(
            ISpaceEngineersNetworkGateway gateway, ushort channelId, string networkId, bool usesWireIdentity,
            Action<SpaceEngineersNetworkReceiveFailure> receiveFailureHandler)
        {
            if (gateway == null)
                throw new ArgumentNullException(nameof(gateway));

            _gateway = gateway;
            _receiveFailureHandler = receiveFailureHandler;
            UsesWireIdentity = usesWireIdentity;
            NetworkId = usesWireIdentity ? SpaceEngineersNetworkIdentity.Normalize(networkId) : null;

            Transport = usesWireIdentity 
                ? new SpaceEngineersNetworkTransport(gateway, channelId, NetworkId) 
                : new SpaceEngineersNetworkTransport(gateway, channelId);

            Endpoint = new NetworkEndpoint(Transport);
            _secureMessageHandler = ReceiveSecureMessage;

            _gateway.RegisterSecureMessageHandler(ChannelId, _secureMessageHandler);
        }

        /// <summary>
        /// Gets the owned secure-message channel.
        /// </summary>
        public ushort ChannelId => Transport.ChannelId;

        /// <summary>
        /// Gets whether the session uses a forced channel and therefore never
        /// creates an API discovery consumer or accepts reassignment.
        /// </summary>
        public bool IsForcedChannel { get; private set; }

        /// <summary>
        /// Gets the generation of the latest accepted managed assignment, or
        /// null before any assignment has been accepted.
        /// </summary>
        public ulong? AssignmentGeneration { get; private set; }

        /// <summary>
        /// Gets whether a provider with a valid endpoint contract currently
        /// owns this session's managed registration.
        /// </summary>
        public bool IsNetworkManagerConnected 
            => _activeNetworkManagerProviderInstanceId.HasValue
            && NetworkManagerError == null;

        /// <summary>
        /// Gets the most recent NetworkManager contract or registration error,
        /// or null when no managed integration error is active.
        /// </summary>
        public Exception NetworkManagerError { get; private set; }

        /// <summary>
        /// Gets whether this session uses the versioned Mz.Networking wire
        /// identity rather than the legacy unframed envelope.
        /// </summary>
        public bool UsesWireIdentity { get; }

        /// <summary>
        /// Gets the stable application network identity, or null for a legacy
        /// compatibility session.
        /// </summary>
        public string NetworkId { get; }

        /// <summary>
        /// Gets the concrete Space Engineers transport.
        /// </summary>
        public SpaceEngineersNetworkTransport Transport { get; }

        /// <summary>
        /// Gets the transport-independent mod-facing endpoint.
        /// </summary>
        public NetworkEndpoint Endpoint { get; }

        /// <summary>
        /// Raised after a newer provider-scoped assignment is accepted.
        /// Subscriber exceptions are isolated from assignment processing.
        /// </summary>
        public event Action<SpaceEngineersNetworkChannelAssignmentEventArgs> ChannelAssignmentApplied;

        /// <summary>
        /// Raised for each rejected packet after its structured bounded
        /// diagnostic fields have been prepared. Subscriber exceptions are
        /// isolated and do not interrupt packet processing.
        /// </summary>
        public event Action<SpaceEngineersNetworkReceiveFailure> Diagnostic;

        /// <summary>
        /// Removes the exact secure-message handler registration.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _connectedNetworkManagerProviderInstanceId = null;
                _activeNetworkManagerProviderInstanceId = null;
                _activeAssignmentGeneration = null;
                ReleaseNetworkManagerRegistration();

                if (_networkManagerConsumer == null)
                    return;
                
                _networkManagerConsumer.Connected -= OnNetworkManagerConnected;
                _networkManagerConsumer.Disconnected -= OnNetworkManagerDisconnected;

                _networkManagerConsumer.Dispose();
            }
            finally
            {
                _gateway.UnregisterSecureMessageHandler(ChannelId, _secureMessageHandler);
            }
        }

        private void ReceiveSecureMessage(ushort channelId, byte[] serialized, ulong senderPeerId, bool senderIsServer)
        {
            if (channelId != ChannelId)
            {
                ReportFailure(channelId, serialized, senderPeerId, senderIsServer, SpaceEngineersNetworkReceiveFailureKind.ProcessingFailure, 
                    null, new InvalidOperationException(
                        $"The secure-message handler received channel {channelId} instead of its configured channel {ChannelId}."
                    )
                );

                return;
            }

            if (serialized == null)
            {
                ReportFailure(channelId, null, senderPeerId, senderIsServer, SpaceEngineersNetworkReceiveFailureKind.ProcessingFailure, 
                    null, new ArgumentNullException(nameof(serialized))
                );

                return;
            }

            byte[] serializedEnvelope = serialized;
            string observedNetworkId = null;

            if (UsesWireIdentity)
            {
                SpaceEngineersNetworkWireDecodeResult decoded = SpaceEngineersNetworkWireCodec.Decode(serialized, NetworkId);

                if (decoded.Status != SpaceEngineersNetworkWireStatus.Success)
                {
                    ReportFailure(channelId, serialized, senderPeerId, senderIsServer, ToFailureKind(decoded.Status), 
                                  decoded.ObservedNetworkId, decoded.Exception);

                    return;
                }

                serializedEnvelope = decoded.SerializedEnvelope;
                observedNetworkId = decoded.ObservedNetworkId;
            }

            NetworkEnvelope envelope;

            try
            {
                envelope = _gateway.Deserialize(serializedEnvelope);

                if (envelope == null)
                    throw new InvalidOperationException("The serialized network envelope was empty.");
            }
            catch (Exception exception)
            {
                ReportFailure(channelId, serialized, senderPeerId, senderIsServer, SpaceEngineersNetworkReceiveFailureKind.MalformedOwnPacket,
                              observedNetworkId, exception);

                return;
            }

            Exception handlerFailure = null;

            try
            {
                NetworkReceiveContext ignored;

                Endpoint.Receive(
                    envelope, senderPeerId, senderIsServer, delegate(Exception exception)
                    {
                        handlerFailure = exception;
                    },
                    out ignored
                );
            }
            catch (Exception exception)
            {
                SpaceEngineersNetworkReceiveFailureKind kind = ReferenceEquals(handlerFailure, exception)
                    ? SpaceEngineersNetworkReceiveFailureKind.HandlerFailure
                    : SpaceEngineersNetworkReceiveFailureKind.ProcessingFailure;

                ReportFailure(channelId, serialized, senderPeerId, senderIsServer, kind, observedNetworkId, exception);
            }
        }

        private void ReportFailure(ushort channelId, byte[] serialized, ulong senderPeerId, bool senderIsServer,
                                   SpaceEngineersNetworkReceiveFailureKind kind, string observedNetworkId, Exception exception) 
        {
            byte[] packet = serialized ?? Array.Empty<byte>();

            SpaceEngineersNetworkDiagnosticData diagnostic = SpaceEngineersNetworkDiagnosticBuilder.Build(
                channelId, packet, senderPeerId, senderIsServer, kind, NetworkId, observedNetworkId,
                _gateway as ISpaceEngineersNetworkDiagnosticGateway
            );

            var failure = new SpaceEngineersNetworkReceiveFailure(
                channelId, packet, senderPeerId, senderIsServer, kind, NetworkId, observedNetworkId,
                exception, diagnostic
            );

            ReportManagedConflict(failure);

            if (_receiveFailureHandler == null)
            {
                PublishDiagnostic(failure);
                return;
            }

            try
            {
                _receiveFailureHandler(failure);
            }
            finally
            {
                PublishDiagnostic(failure);
            }
        }

        private void ReportManagedConflict(SpaceEngineersNetworkReceiveFailure failure)
        {
            if (failure == null || !failure.IsChannelConflict || _disposed)
                return;
            if (IsForcedChannel)
                return;
            if (!_activeNetworkManagerProviderInstanceId.HasValue || !_activeAssignmentGeneration.HasValue)
                return;
            if (_activeNetworkManagerConflictReporter == null || _activeAssignmentConflictReported)
                return;
            if (failure.ChannelId != ChannelId)
                return;

            var reportConflict = _activeNetworkManagerConflictReporter;

            ulong generation = _activeAssignmentGeneration.Value;
            
            _activeAssignmentConflictReported = true;

            try
            {
                reportConflict(failure.ChannelId, generation);
            }
            catch
            {
                // ignored
            }
        }

        private void PublishDiagnostic(SpaceEngineersNetworkReceiveFailure failure)
        {
            var handlers = Diagnostic;

            if (handlers == null)
                return;

            var subscribers = handlers.GetInvocationList();

            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<SpaceEngineersNetworkReceiveFailure>)subscriber)(failure);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void OnNetworkManagerConnected(ApiConnectedEventArgs eventArgs)
        {
            Guid providerInstanceId = eventArgs.Connection.ProviderInstanceId;

            _connectedNetworkManagerProviderInstanceId = providerInstanceId;

            ReleaseNetworkManagerRegistration();
            _activeNetworkManagerProviderInstanceId = null;
            _activeAssignmentGeneration = null;
            NetworkManagerError = null;

            try
            {
                var registerNetwork = NetworkManagerApiContract.GetRegisterNetworkEndpoint(eventArgs.Connection);

                Func<string, string, Version, string, string, ushort, Action<ushort, ulong, Action<ushort, ulong>>, Action> 
                    registerNetworkWithConflictReporting;

                bool supportsConflictReporting = NetworkManagerApiContract.TryGetRegisterNetworkWithConflictReportingEndpoint(
                    eventArgs.Connection, out registerNetworkWithConflictReporting);

                _activeNetworkManagerProviderInstanceId = providerInstanceId;

                Action unregister;

                
                var version = new Version(
                    _managedConfiguration.ModVersion.Major,
                    _managedConfiguration.ModVersion.Minor,
                    _managedConfiguration.ModVersion.Patch
                );
                
                if (supportsConflictReporting)
                {
                    Action<ushort, ulong, Action<ushort, ulong>> reportConflictHandler = 
                        delegate(ushort channelId, ulong generation, Action<ushort, ulong> reportConflict) {
                        ApplyManagedAssignment(providerInstanceId, channelId, generation, reportConflict);
                    };
                    
                    unregister = registerNetworkWithConflictReporting(
                        _managedConfiguration.ModId, _managedConfiguration.ModDisplayName, version, 
                        _managedConfiguration.NetworkId, _managedConfiguration.NetworkName, _managedConfiguration.PreferredChannel,
                        reportConflictHandler
                    );
                }
                else
                {
                    unregister = registerNetwork(
                        _managedConfiguration.ModId, _managedConfiguration.ModDisplayName, version,
                        _managedConfiguration.NetworkId, _managedConfiguration.NetworkName, _managedConfiguration.PreferredChannel,
                        delegate(ushort channelId, ulong generation) {
                            ApplyManagedAssignment(providerInstanceId, channelId, generation, null);
                        });
                }

                if (unregister == null)
                    throw new InvalidOperationException("NetworkManager returned no registration cleanup action.");

                _networkManagerUnregister = unregister;
            }
            catch (Exception exception)
            {
                if (_activeNetworkManagerProviderInstanceId == providerInstanceId)
                {
                    _activeNetworkManagerProviderInstanceId = null;
                    _activeAssignmentGeneration = null;
                }

                ReleaseNetworkManagerRegistration();
                NetworkManagerError = exception;
            }
        }

        private void OnNetworkManagerDisconnected(ApiDisconnectedEventArgs eventArgs)
        {
            Guid providerInstanceId = eventArgs.PreviousConnection.ProviderInstanceId;

            if (_connectedNetworkManagerProviderInstanceId != providerInstanceId)
                return;

            _connectedNetworkManagerProviderInstanceId = null;

            if (_activeNetworkManagerProviderInstanceId == providerInstanceId)
            {
                _activeNetworkManagerProviderInstanceId = null;
                _activeAssignmentGeneration = null;
                ReleaseNetworkManagerRegistration();
            }

            NetworkManagerError = null;
        }

        private void ApplyManagedAssignment(Guid providerInstanceId, ushort channelId, ulong generation, Action<ushort, ulong> conflictReporter)
        {
            if (!CanApplyManagedAssignment(providerInstanceId, generation))
                return;

            if (channelId != ChannelId)
            {
                var schedulingGateway = _gateway as ISpaceEngineersNetworkSchedulingGateway;

                if (schedulingGateway != null)
                {
                    schedulingGateway.InvokeOnGameThread(
                        delegate {
                            ApplyManagedAssignmentImmediately(providerInstanceId, channelId, generation, conflictReporter);
                        }
                    );

                    return;
                }
            }

            ApplyManagedAssignmentImmediately(providerInstanceId, channelId, generation, conflictReporter);
        }

        private void ApplyManagedAssignmentImmediately(Guid providerInstanceId, ushort channelId, ulong generation,
                                                       Action<ushort, ulong> conflictReporter)
        {
            if (!CanApplyManagedAssignment(providerInstanceId, generation))
                return;

            ushort previousChannel = ChannelId;

            if (channelId != previousChannel)
                ChangeChannel(channelId);

            _activeAssignmentGeneration = generation;
            _activeNetworkManagerConflictReporter = conflictReporter;
            _activeAssignmentConflictReported = false;
            AssignmentGeneration = generation;
            
            PublishChannelAssignment(new SpaceEngineersNetworkChannelAssignmentEventArgs(previousChannel, channelId, generation));
        }

        private bool CanApplyManagedAssignment(Guid providerInstanceId, ulong generation)
        {
            if (_disposed || IsForcedChannel || _activeNetworkManagerProviderInstanceId != providerInstanceId)
                return false;

            return !_activeAssignmentGeneration.HasValue || generation > _activeAssignmentGeneration.Value;
        }

        private void PublishChannelAssignment(SpaceEngineersNetworkChannelAssignmentEventArgs eventArgs)
        {
            var handlers = ChannelAssignmentApplied;

            if (handlers == null)
                return;

            var subscribers = handlers.GetInvocationList();

            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<SpaceEngineersNetworkChannelAssignmentEventArgs>)subscriber)(eventArgs);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void ChangeChannel(ushort channelId)
        {
            ushort previousChannel = ChannelId;

            _gateway.RegisterSecureMessageHandler(channelId, _secureMessageHandler);

            try
            {
                _gateway.UnregisterSecureMessageHandler(previousChannel, _secureMessageHandler);
            }
            catch
            {
                try
                {
                    _gateway.UnregisterSecureMessageHandler(channelId, _secureMessageHandler);
                }
                catch
                {
                    // ignored
                }

                throw;
            }

            Transport.ChangeChannel(channelId);
        }

        private void ReleaseNetworkManagerRegistration()
        {
            _activeNetworkManagerConflictReporter = null;
            _activeAssignmentConflictReported = false;

            Action unregister = _networkManagerUnregister;
            _networkManagerUnregister = null;

            if (unregister == null)
                return;

            try
            {
                unregister();
            }
            catch
            {
                // ignored
            }
        }

        private static IModMessageBus CreateDefaultMessageBus(SpaceEngineersManagedNetworkConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return configuration.ForcedChannel.HasValue ? null : new SpaceEngineersModMessageBus();
        }

        private static ushort GetManagedInitialChannel(IModMessageBus messageBus, SpaceEngineersManagedNetworkConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (!configuration.ForcedChannel.HasValue && messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            return configuration.InitialChannel;
        }

        private static string GetManagedNetworkId(SpaceEngineersManagedNetworkConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return configuration.NetworkId;
        }
        private static SpaceEngineersNetworkReceiveFailureKind ToFailureKind(SpaceEngineersNetworkWireStatus status)
        {
            if (status == SpaceEngineersNetworkWireStatus.ForeignPacket)
                return SpaceEngineersNetworkReceiveFailureKind.ForeignPacket;
            if (status == SpaceEngineersNetworkWireStatus.NetworkMismatch)
                return SpaceEngineersNetworkReceiveFailureKind.NetworkMismatch;
            if (status == SpaceEngineersNetworkWireStatus.UnsupportedWireVersion)
                return SpaceEngineersNetworkReceiveFailureKind.UnsupportedWireVersion;
            if (status == SpaceEngineersNetworkWireStatus.MalformedWirePacket)
                return SpaceEngineersNetworkReceiveFailureKind.MalformedWirePacket;
            
            throw new InvalidOperationException("A successful wire result cannot be converted to a receive failure.");
        }
    }
}
