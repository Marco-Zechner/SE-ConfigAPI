using System;

namespace Mz.ApiProtocol.SpaceEngineers
{
    /// <summary>
    /// Discovers and accepts the first compatible provider for one API
    /// requirement.
    /// </summary>
    /// <remarks>
    /// The consumer listens for unsolicited provider announcements and may
    /// request discovery explicitly through <see cref="RequestDiscovery"/>.
    /// A connected consumer may release its provider with
    /// <see cref="Disconnect"/> or atomically disconnect and request again
    /// with <see cref="Rediscover"/>.
    /// </remarks>
    public sealed class ApiDiscoveryConsumer : IDisposable
    {
        private readonly IModMessageBus _messageBus;

        private ModMessageSubscription _subscription;
        private Guid _pendingCorrelationId;
        private bool _isDisposed;

        /// <summary>
        /// Occurs when a provider for the requested API is observed and its
        /// compatibility has been evaluated.
        /// </summary>
        /// <remarks>
        /// Subscriber failures are captured in <see cref="LastError"/>.
        /// One failing subscriber does not prevent later subscribers from
        /// receiving the event.
        /// </remarks>
        public event Action<ApiProviderObservedEventArgs> ProviderObserved;

        /// <summary>
        /// Occurs after a compatible provider connection has been accepted.
        /// </summary>
        /// <remarks>
        /// Subscriber failures are captured in <see cref="LastError"/> and do
        /// not undo the accepted connection.
        /// </remarks>
        public event Action<ApiConnectedEventArgs> Connected;

        /// <summary>
        /// Occurs after an accepted provider connection has been removed.
        /// </summary>
        /// <remarks>
        /// Subscriber failures are captured in <see cref="LastError"/> and do
        /// not restore the removed connection.
        /// </remarks>
        public event Action<ApiDisconnectedEventArgs> Disconnected;

        /// <summary>
        /// Occurs when a provider for the requested API uses an incompatible wire
        /// protocol.
        /// </summary>
        /// <remarks>
        /// Subscriber failures are captured in <see cref="LastError"/> and do not
        /// escape through the shared mod-message handler.
        /// </remarks>
        public event Action<ApiWireIncompatibilityEventArgs> WireIncompatibilityObserved;
        
        /// <summary>
        /// Gets the consumer and its API dependency declaration.
        /// </summary>
        public ApiDependencyDescriptor Dependency { get; }

        /// <summary>
        /// Gets the API requirement declared by the consumer.
        /// </summary>
        public ApiRequirement Requirement => Dependency.Requirement;

        /// <summary>
        /// Gets whether the consumer is currently listening for provider
        /// announcements.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Gets whether a compatible provider has been accepted.
        /// </summary>
        public bool IsConnected => Connection != null;

        /// <summary>
        /// Gets the accepted provider connection, or null when no compatible
        /// provider has been accepted.
        /// </summary>
        public ApiConnection Connection { get; private set; }

        /// <summary>
        /// Gets the correlation identifier of the most recent unresolved
        /// discovery request.
        /// </summary>
        /// <remarks>
        /// <see cref="Guid.Empty"/> means there is no unresolved request.
        /// An incompatible response does not resolve the request because
        /// another provider may still respond compatibly.
        /// </remarks>
        public Guid PendingCorrelationId => _pendingCorrelationId;

        /// <summary>
        /// Gets the most recently observed provider descriptor for the
        /// requested API.
        /// </summary>
        public ApiDescriptor LastObservedProvider { get; private set; }

        /// <summary>
        /// Gets the compatibility result for the most recently observed
        /// provider for the requested API.
        /// </summary>
        public ApiCompatibilityStatus? LastCompatibilityStatus { get; private set; }

        /// <summary>
        /// Gets the last unexpected processing, transport, or subscriber
        /// error.
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Creates an API discovery consumer.
        /// </summary>
        /// <param name="messageBus">
        /// The mod-message transport used for discovery.
        /// </param>
        /// <param name="dependency">
        /// The API identity and supported provider versions.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="messageBus"/> or
        /// <paramref name="dependency"/> is null.
        /// </exception>
        public ApiDiscoveryConsumer(IModMessageBus messageBus, ApiDependencyDescriptor dependency)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (dependency == null)
                throw new ArgumentNullException(nameof(dependency));

            _messageBus = messageBus;
            Dependency = dependency;
        }

        /// <summary>
        /// Registers the provider announcement handler.
        /// </summary>
        /// <remarks>
        /// This method does not automatically send a request. Repeated calls
        /// while already started have no effect.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has been disposed.
        /// </exception>
        public void Start()
        {
            ThrowIfDisposed();

            if (IsStarted)
                return;

            _subscription = new ModMessageSubscription(_messageBus, ApiProtocolChannels.Discovery, HandleMessage);

            IsStarted = true;
        }

        /// <summary>
        /// Sends a discovery request for the configured API.
        /// </summary>
        /// <returns>
        /// The non-empty correlation identifier assigned to the request.
        /// </returns>
        /// <remarks>
        /// Requests may be sent repeatedly while disconnected. A newer
        /// request replaces the previous unresolved correlation identifier.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has not been started or already has an
        /// accepted connection.
        /// </exception>
        // ReSharper disable once MemberCanBePrivate.Global
        public Guid RequestDiscovery()
        {
            ThrowIfDisposed();
            EnsureStarted();

            if (IsConnected)
                throw new InvalidOperationException("The API discovery consumer is already connected. Call Rediscover to replace the current connection.");

            var correlationId = Guid.NewGuid();
            _pendingCorrelationId = correlationId;
            LastError = null;
            
            var payload = ApiDiscoveryWireProtocol.CreateRequest(Dependency, correlationId);

            try
            {
                _messageBus.Send(ApiProtocolChannels.Discovery, payload);
            }
            catch (Exception exception)
            {
                if (_pendingCorrelationId == correlationId)
                    _pendingCorrelationId = Guid.Empty;

                LastError = exception;
                throw;
            }

            return correlationId;
        }

        /// <summary>
        /// Removes the accepted provider connection.
        /// </summary>
        /// <returns>
        /// True when a connection was removed; false when the consumer was
        /// already disconnected.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has been disposed.
        /// </exception>
        public bool Disconnect()
        {
            ThrowIfDisposed();

            return DisconnectInternal(ApiDisconnectReason.ConsumerRequested);
        }

        /// <summary>
        /// Removes the current provider connection and immediately sends a
        /// new discovery request.
        /// </summary>
        /// <returns>
        /// The correlation identifier assigned to the new request.
        /// </returns>
        /// <remarks>
        /// The method also works while currently disconnected. In that case
        /// it behaves like <see cref="RequestDiscovery"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer has not been started.
        /// </exception>
        public Guid Rediscover()
        {
            ThrowIfDisposed();
            EnsureStarted();

            DisconnectInternal(ApiDisconnectReason.RediscoveryRequested);

            return RequestDiscovery();
        }

        /// <summary>
        /// Stops listening and clears the current connection and pending
        /// request.
        /// </summary>
        /// <remarks>
        /// Repeated calls while already stopped have no effect. The consumer
        /// may be started again after being stopped.
        /// </remarks>
        // ReSharper disable once MemberCanBePrivate.Global
        public void Stop()
        {
            if (_isDisposed || !IsStarted)
                return;

            _subscription.Dispose();

            _subscription = null;
            IsStarted = false;
            _pendingCorrelationId = Guid.Empty;

            DisconnectInternal(ApiDisconnectReason.ConsumerStopped);

            LastObservedProvider = null;
            LastCompatibilityStatus = null;
        }

        /// <summary>
        /// Stops the consumer and permanently releases its registration.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _isDisposed = true;
        }

        private void HandleMessage(object payload)
        {
            if (!IsStarted || _isDisposed)
                return;

            try
            {
                ApiWireEnvelope envelope;

                if (!ApiDiscoveryWireProtocol.TryParseEnvelope(payload, out envelope))
                    return;

                if (!string.Equals(Requirement.ApiId, envelope.ApiId, StringComparison.Ordinal))
                    return;

                ApiWireCompatibilityStatus wireCompatibility = ApiProtocolInfo.EvaluateWireProtocol(envelope.WireProtocolVersion);

                if (wireCompatibility != ApiWireCompatibilityStatus.Compatible)
                {
                    LastError = RaiseEvent(
                        WireIncompatibilityObserved,
                        new ApiWireIncompatibilityEventArgs(envelope, wireCompatibility)
                    );

                    return;
                }

                if (envelope.MessageKind == ApiWireMessageKind.Withdrawal)
                {
                    ApiProviderWithdrawal withdrawal;

                    if (ApiDiscoveryWireProtocol.TryParseWithdrawal(payload, out withdrawal))
                        HandleWithdrawal(withdrawal);

                    return;
                }

                if (envelope.MessageKind != ApiWireMessageKind.Announcement || IsConnected)
                    return;

                ApiAnnouncement announcement;

                if (!ApiDiscoveryWireProtocol.TryParseAnnouncement(payload, out announcement))
                    return;

                
                if (!ApiProtocolInfo.IsWireProtocolCompatible(announcement.WireProtocolVersion))
                    return;

                if (!string.Equals(Requirement.ApiId, announcement.Descriptor.ApiId, StringComparison.Ordinal))
                    return;

                if (announcement.CorrelationId != Guid.Empty && announcement.CorrelationId != _pendingCorrelationId)
                    return;

                var compatibility = Requirement.Evaluate(announcement.Descriptor);

                LastObservedProvider = announcement.Descriptor;
                LastCompatibilityStatus = compatibility;

                var observedError = RaiseEvent(
                    ProviderObserved,
                    new ApiProviderObservedEventArgs(announcement, compatibility)
                );

                if (compatibility != ApiCompatibilityStatus.Compatible)
                {
                    LastError = observedError;
                    return;
                }

                var connection = new ApiConnection(announcement);

                Connection = connection;
                _pendingCorrelationId = Guid.Empty;

                var connectedError = RaiseEvent(
                    Connected,
                    new ApiConnectedEventArgs(connection, announcement.CorrelationId)
                );

                LastError = observedError ?? connectedError;
            }
            catch (Exception exception)
            {
                LastError = exception;
            }
        }
        
        private void HandleWithdrawal(ApiProviderWithdrawal withdrawal)
        {
            if (!string.Equals(Requirement.ApiId, withdrawal.ApiId, StringComparison.Ordinal))
                return;

            ApiConnection connection = Connection;

            if (connection == null)
                return;

            if (connection.ProviderInstanceId == Guid.Empty)
                return;

            if (connection.ProviderInstanceId != withdrawal.ProviderInstanceId)
                return;

            DisconnectInternal(ApiDisconnectReason.ProviderWithdrawn);
        }

        private bool DisconnectInternal(ApiDisconnectReason reason)
        {
            var previousConnection = Connection;

            if (previousConnection == null)
                return false;

            Connection = null;
            _pendingCorrelationId = Guid.Empty;

            LastError = RaiseEvent(
                Disconnected,
                new ApiDisconnectedEventArgs(previousConnection, reason)
            );

            return true;
        }

        private Exception RaiseEvent<TEventArgs>(Action<TEventArgs> handler, TEventArgs eventArgs)
        {
            if (handler == null)
                return null;

            Exception firstError = null;
            var subscribers = handler.GetInvocationList();

            foreach (var subscriberItem in subscribers)
            {
                try
                {
                    var subscriber = (Action<TEventArgs>)subscriberItem;

                    subscriber(eventArgs);
                }
                catch (Exception exception)
                {
                    if (firstError == null)
                        firstError = exception;
                }
            }

            return firstError;
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
                throw new InvalidOperationException("The API discovery consumer has not been started.");
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The API discovery consumer has been disposed.");
        }
    }
}