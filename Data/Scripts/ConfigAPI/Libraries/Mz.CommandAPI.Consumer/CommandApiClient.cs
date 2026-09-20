using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace Mz.CommandApi
{
    /// <summary>
    /// Discovers CommandAPI and exposes its dictionary endpoint as a typed
    /// consumer API.
    /// </summary>
    public sealed class CommandApiClient :
        IDisposable
    {
        public const string ProviderApiId =
            "MarcoZechner.CommandAPI";

        public const string RegisterCommandEndpoint =
            "RegisterCommand";

        private readonly string _consumerId;

        private readonly ApiDiscoveryConsumer
            _consumer;

        private readonly List<
            CommandRegistrationHandle
        > _registrations =
            new List<
                CommandRegistrationHandle
            >();

        private Func<
            IDictionary<string, object>,
            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            >,
            Action
        > _registerCommand;

        private bool _isDisposed;
        private Exception _lastError;

        public event Action Connected;

        public event Action Disconnected;

        public event Action<
            CommandRegistration,
            Exception
        > RegistrationFailed;

        public bool IsStarted =>
            _consumer.IsStarted;

        public bool IsConnected =>
            _registerCommand != null;

        public SemanticVersion ProviderModVersion
        {
            get;
            private set;
        }

        public SemanticVersion ProviderApiVersion
        {
            get;
            private set;
        }

        public Exception LastError =>
            _lastError ?? _consumer.LastError;

        public CommandApiClient(
            IModMessageBus messageBus,
            string consumerId,
            string consumerDisplayName,
            SemanticVersion consumerModVersion,
            bool isRequired,
            string featureDescription
        )
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException(
                    "A stable consumer mod identifier is required.",
                    nameof(consumerId)
                );

            if (string.IsNullOrWhiteSpace(consumerDisplayName))
                throw new ArgumentException(
                    "A consumer display name is required.",
                    nameof(consumerDisplayName)
                );

            if (consumerModVersion == null)
                throw new ArgumentNullException(
                    nameof(consumerModVersion)
                );

            _consumerId =
                consumerId.Trim();

            var dependency =
                new ApiDependencyDescriptor(
                    new ApiModIdentity(
                        _consumerId,
                        consumerDisplayName.Trim(),
                        consumerModVersion
                    ),
                    new ApiRequirement(
                        ProviderApiId,
                        new ApiVersionRange(
                            ApiVersionFile
                                .MinimumProviderApiVersion,
                            null
                        )
                    ),
                    isRequired
                        ? ApiDependencyKind.Required
                        : ApiDependencyKind.Optional,
                    featureDescription
                );

            _consumer =
                new ApiDiscoveryConsumer(
                    messageBus,
                    dependency
                );

            _consumer.Connected +=
                OnConnected;

            _consumer.Disconnected +=
                OnDisconnected;
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (IsStarted)
                return;

            _lastError = null;
            _consumer.Start();

            if (!_consumer.IsConnected)
                _consumer.RequestDiscovery();
        }

        public Guid RequestDiscovery()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.RequestDiscovery();
        }

        public Guid Rediscover()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.Rediscover();
        }

        public void Stop()
        {
            ThrowIfDisposed();

            ReleaseProviderRegistrations();
            ClearConnection();
            _consumer.Stop();
        }

        public CommandRegistrationHandle Register(
            CommandRegistration registration,
            Func<
                CommandRequest,
                CommandResponse
            > handler
        )
        {
            ThrowIfDisposed();

            if (registration == null)
                throw new ArgumentNullException(
                    nameof(registration)
                );

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var handle =
                new CommandRegistrationHandle(
                    this,
                    registration,
                    handler
                );

            _registrations.Add(handle);

            if (IsConnected)
            {
                try
                {
                    Activate(handle);
                }
                catch
                {
                    _registrations.Remove(handle);
                    handle.MarkDisposed();
                    throw;
                }
            }

            return handle;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            ReleaseProviderRegistrations();

            _consumer.Connected -=
                OnConnected;

            _consumer.Disconnected -=
                OnDisconnected;

            _consumer.Dispose();

            for (
                int index = 0;
                index < _registrations.Count;
                index++
            )
            {
                _registrations[index].MarkDisposed();
            }

            _registrations.Clear();
            ClearConnection();
        }

        internal void RemoveRegistration(
            CommandRegistrationHandle handle
        )
        {
            if (handle == null || handle.IsDisposed)
                return;

            _registrations.Remove(handle);

            Action unregister =
                handle.ProviderUnregister;

            handle.ProviderUnregister = null;

            if (unregister != null)
            {
                try
                {
                    unregister();
                }
                catch (Exception exception)
                {
                    _lastError = exception;
                }
            }

            handle.MarkDisposed();
        }

        private void OnConnected(
            ApiConnectedEventArgs eventArgs
        )
        {
            try
            {
                Func<
                    IDictionary<string, object>,
                    Func<
                        IDictionary<string, object>,
                        IDictionary<string, object>
                    >,
                    Action
                > registerCommand;

                if (
                    !eventArgs.Connection.TryGetEndpoint(
                        RegisterCommandEndpoint,
                        out registerCommand
                    )
                )
                {
                    _lastError =
                        new InvalidOperationException(
                            "The CommandAPI provider is missing the exact RegisterCommand endpoint."
                        );

                    _consumer.Disconnect();
                    return;
                }

                _registerCommand =
                    registerCommand;

                ProviderModVersion =
                    eventArgs.Connection.Provider.Version;

                ProviderApiVersion =
                    eventArgs.Connection.Descriptor.Version;

                _lastError = null;

                ActivatePendingRegistrations();
                RaiseConnected();
            }
            catch (Exception exception)
            {
                _lastError = exception;
                ClearConnection();
                _consumer.Disconnect();
            }
        }

        private void OnDisconnected(
            ApiDisconnectedEventArgs eventArgs
        )
        {
            ReleaseProviderRegistrations();
            ClearConnection();
            RaiseDisconnected();
        }

        private void ActivatePendingRegistrations()
        {
            CommandRegistrationHandle[] snapshot =
                _registrations.ToArray();

            for (
                int index = 0;
                index < snapshot.Length;
                index++
            )
            {
                CommandRegistrationHandle handle =
                    snapshot[index];

                if (
                    handle.IsDisposed
                    || handle.IsActive
                )
                {
                    continue;
                }

                try
                {
                    Activate(handle);
                }
                catch (Exception exception)
                {
                    handle.LastError =
                        exception;

                    _lastError =
                        exception;

                    RaiseRegistrationFailed(
                        handle.Registration,
                        exception
                    );
                }
            }
        }

        private void Activate(
            CommandRegistrationHandle handle
        )
        {
            if (!IsConnected)
                return;

            Action unregister =
                _registerCommand(
                    CreateMetadata(handle.Registration),
                    delegate(
                        IDictionary<string, object> payload
                    )
                    {
                        CommandRequest request =
                            ReadRequest(payload);

                        CommandResponse response =
                            handle.Handler(request);

                        if (response == null)
                        {
                            throw new ArgumentException(
                                "The typed command handler returned no response.",
                                nameof(handle)
                            );
                        }

                        return WriteResponse(response);
                    }
                );

            if (unregister == null)
            {
                throw new InvalidOperationException(
                    "The CommandAPI provider returned no unregister action."
                );
            }

            handle.ProviderUnregister =
                unregister;

            handle.LastError =
                null;
        }

        private IDictionary<string, object>
            CreateMetadata(
                CommandRegistration registration
            )
        {
            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                { "OwnerId", _consumerId },
                { "Prefix", registration.Prefix },
                {
                    "ExecutionLocation",
                    registration.ExecutionLocation.ToString()
                },
                {
                    "CanonicalName",
                    registration.CanonicalName
                },
                { "Aliases", registration.Aliases },
                {
                    "ShortDescription",
                    registration.ShortDescription
                },
                { "HelpText", registration.HelpText },
                { "Usage", registration.Usage },
                { "Category", registration.Category },
                {
                    "PermissionRequirement",
                    registration.PermissionRequirement
                }
            };
        }

        private static CommandRequest ReadRequest(
            IDictionary<string, object> values
        )
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return new CommandRequest(
                ReadRequiredString(values, "RequestId"),
                ReadRequiredValue<ulong>(
                    values,
                    "RequesterSteamId"
                ),
                ReadRequiredValue<long>(
                    values,
                    "RequesterIdentityId"
                ),
                ReadRequiredString(
                    values,
                    "RequesterDisplayName"
                ),
                ReadRequiredValue<int>(
                    values,
                    "PermissionLevel"
                ),
                ReadRequiredValue<bool>(
                    values,
                    "IsServer"
                ),
                ReadRequiredString(values, "Prefix"),
                ReadRequiredString(values, "CommandName"),
                ReadStringArray(values, "Arguments")
            );
        }

        private static IDictionary<string, object>
            WriteResponse(
                CommandResponse response
            )
        {
            return new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                { "IsSuccess", response.IsSuccess },
                { "Title", response.Title },
                { "Summary", response.Summary },
                {
                    "DetailLines",
                    response.DetailLines
                },
                {
                    "Severity",
                    response.Severity.ToString()
                },
                { "UsageHint", response.UsageHint }
            };
        }

        private void ReleaseProviderRegistrations()
        {
            Exception firstError =
                null;

            CommandRegistrationHandle[] snapshot =
                _registrations.ToArray();

            for (
                int index = 0;
                index < snapshot.Length;
                index++
            )
            {
                Action unregister =
                    snapshot[index].ProviderUnregister;

                snapshot[index].ProviderUnregister =
                    null;

                if (unregister == null)
                    continue;

                try
                {
                    unregister();
                }
                catch (Exception exception)
                {
                    if (firstError == null)
                        firstError = exception;
                }
            }

            if (firstError != null)
                _lastError = firstError;
        }

        private void ClearConnection()
        {
            _registerCommand = null;
            ProviderModVersion = null;
            ProviderApiVersion = null;
        }

        private void RaiseConnected()
        {
            Action handler =
                Connected;

            if (handler == null)
                return;

            foreach (
                Action subscriber in
                handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
            }
        }

        private void RaiseDisconnected()
        {
            Action handler =
                Disconnected;

            if (handler == null)
                return;

            foreach (
                Action subscriber in
                handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
            }
        }

        private void RaiseRegistrationFailed(
            CommandRegistration registration,
            Exception exception
        )
        {
            Action<
                CommandRegistration,
                Exception
            > handler =
                RegistrationFailed;

            if (handler == null)
                return;

            foreach (
                Action<
                    CommandRegistration,
                    Exception
                > subscriber in
                handler.GetInvocationList()
            )
            {
                try
                {
                    subscriber(
                        registration,
                        exception
                    );
                }
                catch (Exception subscriberException)
                {
                    if (_lastError == null)
                        _lastError = subscriberException;
                }
            }
        }

        private static string ReadRequiredString(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
            {
                throw new ArgumentException(
                    "Required request field '"
                    + key
                    + "' is missing.",
                    nameof(values)
                );
            }

            string text =
                value as string;

            if (text == null)
            {
                throw new ArgumentException(
                    "Request field '"
                    + key
                    + "' must be a string.",
                    nameof(values)
                );
            }

            return text;
        }

        private static T ReadRequiredValue<T>(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
            {
                throw new ArgumentException(
                    "Required request field '"
                    + key
                    + "' is missing.",
                    nameof(values)
                );
            }

            if (!(value is T))
            {
                throw new ArgumentException(
                    "Request field '"
                    + key
                    + "' has the wrong type.",
                    nameof(values)
                );
            }

            return (T)value;
        }

        private static string[] ReadStringArray(
            IDictionary<string, object> values,
            string key
        )
        {
            object value;

            if (!values.TryGetValue(key, out value))
            {
                throw new ArgumentException(
                    "Required request field '"
                    + key
                    + "' is missing.",
                    nameof(values)
                );
            }

            string[] array =
                value as string[];

            if (array != null)
                return (string[])array.Clone();

            IList<string> list =
                value as IList<string>;

            if (list == null)
            {
                throw new ArgumentException(
                    "Request field '"
                    + key
                    + "' must be a string array.",
                    nameof(values)
                );
            }

            var copy =
                new string[list.Count];

            for (
                int index = 0;
                index < list.Count;
                index++
            )
            {
                if (list[index] == null)
                {
                    throw new ArgumentException(
                        "Request arguments cannot contain null values.",
                        nameof(values)
                    );
                }

                copy[index] =
                    list[index];
            }

            return copy;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new InvalidOperationException(
                    "The CommandAPI client has been disposed."
                );
            }
        }
    }
}
