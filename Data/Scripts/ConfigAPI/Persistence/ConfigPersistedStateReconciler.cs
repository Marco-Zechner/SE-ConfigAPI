using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigPersistedStateReconciliationResult
    {
        public ConfigPersistedState State { get; }
        public IReadOnlyList<ConfigDefaultChange> Changes { get; }
        public bool RequiresBackup { get; }

        internal ConfigPersistedStateReconciliationResult(ConfigPersistedState state, IReadOnlyList<ConfigDefaultChange> changes,
                                                          bool requiresBackup)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (changes == null)
                throw new ArgumentNullException(nameof(changes));

            State = state;
            Changes = changes;
            RequiresBackup = requiresBackup;
        }
    }

    public static class ConfigPersistedStateReconciler
    {
        public static ConfigPersistedStateReconciliationResult Reconcile(ConfigPersistedState state, ConfigDocument currentDefaults)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            var reconciliationResult = ConfigDefaultReconciler.Reconcile(state.BaselineDefaults, state.PlayerValues, currentDefaults);

            var reconciledState = new ConfigPersistedState(state.Identity, reconciliationResult.PlayerValues,
                                                           reconciliationResult.BaselineDefaults, state.CurrentFile);

            return new ConfigPersistedStateReconciliationResult(reconciledState, reconciliationResult.Changes, 
                                                                reconciliationResult.RequiresBackup);
        }
    }
}
