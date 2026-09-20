using System;

namespace Mz.CommandApi
{
    /// <summary>
    /// Represents one logical command registration owned by a client.
    /// </summary>
    public sealed class CommandRegistrationHandle :
        IDisposable
    {
        private CommandApiClient _owner;

        internal Func<
            CommandRequest,
            CommandResponse
        > Handler { get; }

        internal Action ProviderUnregister { get; set; }

        public CommandRegistration Registration { get; }

        public bool IsActive =>
            ProviderUnregister != null;

        public bool IsDisposed { get; private set; }

        public Exception LastError { get; internal set; }

        internal CommandRegistrationHandle(
            CommandApiClient owner,
            CommandRegistration registration,
            Func<
                CommandRequest,
                CommandResponse
            > handler
        )
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (registration == null)
                throw new ArgumentNullException(
                    nameof(registration)
                );

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _owner = owner;
            Registration = registration;
            Handler = handler;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            CommandApiClient owner =
                _owner;

            if (owner != null)
                owner.RemoveRegistration(this);
            else
                MarkDisposed();
        }

        internal void MarkDisposed()
        {
            _owner = null;
            ProviderUnregister = null;
            IsDisposed = true;
        }
    }
}
