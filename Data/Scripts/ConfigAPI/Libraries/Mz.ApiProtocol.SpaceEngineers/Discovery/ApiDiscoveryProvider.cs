using System;
using System.Collections.Generic;

namespace Mz.ApiProtocol.SpaceEngineers
{
    /// <summary>
    /// Exposes one API through the Space Engineers mod-message discovery
    /// protocol.
    /// </summary>
    public sealed class ApiDiscoveryProvider : IDisposable
    {
        private readonly IModMessageBus _messageBus;
        private readonly Dictionary<string, Delegate> _endpoints;

        private ModMessageSubscription _subscription;
        private bool _withdrawalSent;
        private bool _isDisposed;

        /// <summary>
        /// Occurs when a mod requests this API using an incompatible wire
        /// protocol.
        /// </summary>
        /// <remarks>
        /// Subscriber failures are captured in <see cref="LastError"/> and do not
        /// escape through the shared mod-message handler.
        /// </remarks>
        public event Action<ApiWireIncompatibilityEventArgs> WireIncompatibilityObserved;

        /// <summary>
        /// Occurs when a mod requests the API and its supported API range has been
        /// evaluated.
        /// </summary>
        /// <remarks>
        /// The event is raised for compatible and incompatible consumers. Subscriber
        /// failures are captured in <see cref="LastError"/> and do not escape through
        /// the shared mod-message handler.
        /// </remarks>
        public event Action<ApiConsumerObservedEventArgs> ConsumerObserved;
        
        /// <summary>
        /// Gets the mod providing the API.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiModIdentity Provider { get; }

        /// <summary>
        /// Gets the API identity and version exposed by this provider.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the identity of this provider instance.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public Guid ProviderInstanceId { get; }

        /// <summary>
        /// Gets whether the provider is currently listening for requests.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Gets the last unexpected lifecycle or message-processing error.
        /// </summary>
        public Exception LastError { get; private set; }
        
        /// <summary>
        /// Creates a provider with a generated provider-instance identity.
        /// </summary>
        /// <param name="messageBus">
        /// The mod-message transport used for discovery.
        /// </param>
        /// <param name="provider">
        /// The mod providing the API.
        /// </param>
        /// <param name="descriptor">
        /// The API identity and version exposed by the provider.
        /// </param>
        /// <param name="endpoints">
        /// The endpoint delegates exposed by the provider.
        /// </param>
        public ApiDiscoveryProvider(IModMessageBus messageBus, ApiModIdentity provider, ApiDescriptor descriptor, IDictionary<string, Delegate> endpoints)
            : this(messageBus, provider, descriptor, Guid.NewGuid(), endpoints) { }

        /// <summary>
        /// Creates a provider with an explicit provider-instance identity.
        /// </summary>
        /// <param name="messageBus">
        /// The mod-message transport used for discovery.
        /// </param>
        /// <param name="provider">
        /// The mod providing the API.
        /// </param>
        /// <param name="descriptor">
        /// The API identity and version exposed by the provider.
        /// </param>
        /// <param name="providerInstanceId">
        /// The non-empty identity of this provider instance.
        /// </param>
        /// <param name="endpoints">
        /// The endpoint delegates exposed by the provider.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required argument is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the provider-instance ID or an endpoint is invalid.
        /// </exception>
        public ApiDiscoveryProvider(IModMessageBus messageBus, ApiModIdentity provider, ApiDescriptor descriptor, Guid providerInstanceId, IDictionary<string, Delegate> endpoints)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (providerInstanceId == Guid.Empty)
                throw new ArgumentException("A provider requires a non-empty instance identifier.", nameof(providerInstanceId));

            var validated = new ApiAnnouncement(provider, descriptor, providerInstanceId, Guid.Empty, endpoints);

            _messageBus = messageBus;
            Provider = validated.Provider;
            Descriptor = validated.Descriptor;
            ProviderInstanceId = providerInstanceId;

            _endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal);

            foreach (var pair in validated.Endpoints)
                _endpoints.Add(pair.Key, pair.Value);
        }

        /// <summary>
        /// Registers the request handler and broadcasts provider readiness.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the provider has been disposed.
        /// </exception>
        public void Start()
        {
            ThrowIfDisposed();

            if (IsStarted)
                return;

            var subscription = new ModMessageSubscription(_messageBus, ApiProtocolChannels.Discovery, HandleMessage);

            _subscription = subscription;
            _withdrawalSent = false;
            IsStarted = true;

            try
            {
                Announce();
            }
            catch
            {
                IsStarted = false;
                _subscription = null;
                subscription.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Broadcasts an unsolicited provider announcement.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the provider has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the provider has not been started.
        /// </exception>
        public void Announce()
        {
            ThrowIfDisposed();

            if (!IsStarted)
                throw new InvalidOperationException("The API discovery provider has not been started.");

            _messageBus.Send(
                ApiProtocolChannels.Discovery,
                ApiDiscoveryWireProtocol.CreateAnnouncement(Provider, Descriptor, ProviderInstanceId, Guid.Empty, _endpoints)
            );

            LastError = null;
        }

        /// <summary>
        /// Announces provider withdrawal and unregisters the request handler.
        /// </summary>
        /// <remarks>
        /// Repeated calls while already stopped have no effect. The provider
        /// may be started again after stopping.
        /// </remarks>
        public void Stop()
        {
            if (_isDisposed || !IsStarted)
                return;

            Exception withdrawalError = null;

            if (!_withdrawalSent)
            {
                try
                {
                    _messageBus.Send(
                        ApiProtocolChannels.Discovery,
                        ApiDiscoveryWireProtocol.CreateWithdrawal(Provider, Descriptor.ApiId, ProviderInstanceId)
                    );

                    _withdrawalSent = true;
                }
                catch (Exception exception)
                {
                    withdrawalError = exception;
                }
            }

            try
            {
                _subscription.Dispose();
            }
            catch (Exception exception)
            {
                LastError = withdrawalError ?? exception;
                throw;
            }

            _subscription = null;
            IsStarted = false;
            LastError = withdrawalError;

            if (withdrawalError != null)
                throw withdrawalError;
        }

        /// <summary>
        /// Stops the provider and permanently releases its registration.
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

                if (envelope.MessageKind != ApiWireMessageKind.Request)
                    return;

                if (!string.Equals(Descriptor.ApiId, envelope.ApiId, StringComparison.Ordinal))
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

                ApiDiscoveryRequest request;

                if (!ApiDiscoveryWireProtocol.TryParseRequest(payload, out request))
                    return;

                ApiCompatibilityStatus apiCompatibility = request.Dependency.Requirement.Evaluate(Descriptor);

                var observationError = RaiseEvent(
                    ConsumerObserved,
                    new ApiConsumerObservedEventArgs(request, apiCompatibility)
                );

                if (apiCompatibility != ApiCompatibilityStatus.Compatible)
                {
                    LastError = observationError;
                    return;
                }

                _messageBus.Send(
                    ApiProtocolChannels.Discovery,
                    ApiDiscoveryWireProtocol.CreateAnnouncement(Provider, Descriptor, ProviderInstanceId, request.CorrelationId, _endpoints)
                );

                LastError = observationError;
            }
            catch (Exception exception)
            {
                LastError = exception;
            }
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

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The API discovery provider has been disposed.");
        }
    }
}