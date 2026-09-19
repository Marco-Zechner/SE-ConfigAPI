using System;
using System.Collections.Generic;
using System.Linq;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.Toml;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigPersistedSourcePlan
    {
        public string ActiveSource { get; }
        public bool RequiresBackup { get; }
        public bool UsedCanonicalRegeneration { get; }

        internal ConfigPersistedSourcePlan(string activeSource, bool requiresBackup, bool usedCanonicalRegeneration)
        {
            if (activeSource == null)
                throw new ArgumentNullException(nameof(activeSource));

            ActiveSource = activeSource;
            RequiresBackup = requiresBackup;
            UsedCanonicalRegeneration = usedCanonicalRegeneration;
        }
    }

    public static class ConfigPersistedSourcePlanner
    {
        public static ConfigPersistedSourcePlan Plan(ConfigPersistedLoadResult loadResult, ConfigDocument currentDefaults)
        {
            if (loadResult == null)
                throw new ArgumentNullException(nameof(loadResult));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            ConfigDocument target = loadResult.State.PlayerValues;
            string source = loadResult.ActiveSource ?? string.Empty;

            try
            {
                source = ApplyRemovedValues(source, loadResult.Changes);

                source = ApplyTargetValues(source, target.Root, new ConfigValuePath(), loadResult, currentDefaults);

                ConfigDocument decoded = ConfigTomlSourceDecoder.Decode(source, currentDefaults);

                if (decoded.Equals(target))
                    return new ConfigPersistedSourcePlan(source, loadResult.RequiresBackup, false);

                return CreateCanonicalPlan(loadResult, target, null);
            }
            catch (Exception exception)
            {
                if (!IsSourcePreservationFailure(exception))
                    throw;

                return CreateCanonicalPlan(loadResult, target, exception);
            }
        }

        private static string ApplyRemovedValues(string source, IReadOnlyList<ConfigDefaultChange> changes)
        {
            foreach (ConfigDefaultChange change in changes)
            {
                if (change.Kind != ConfigDefaultChangeKind.RemovedValue)
                    continue;

                source = ConfigTomlSourceUpdater.RemoveValue(source, change.Path);
            }

            return source;
        }

        private static string ApplyTargetValues(string source, ConfigObjectNode target, ConfigValuePath parentPath,
                                                ConfigPersistedLoadResult loadResult, ConfigDocument currentDefaults)
        {
            foreach (ConfigObjectEntry entry in target.Entries)
            {
                ConfigValuePath path = parentPath.Append(entry.Name);

                var childObject = entry.Value as ConfigObjectNode;

                if (childObject != null)
                {
                    source = ApplyTargetValues(source, childObject, path, loadResult, currentDefaults);
                    continue;
                }

                source = ApplyTargetValue(source, path, entry.Value, loadResult, currentDefaults);
            }

            return source;
        }

        private static string ApplyTargetValue(string source, ConfigValuePath path, ConfigNode value,
                                               ConfigPersistedLoadResult loadResult, ConfigDocument currentDefaults)
        {
            if (!(value is ConfigNullNode))
                return ConfigTomlSourceUpdater.SetOrInsertValue(source, path, value);

            try
            {
                return ConfigTomlSourceUpdater.SetValue(source, path, ConfigNullNode.Instance);
            }
            catch (KeyNotFoundException) { }

            ConfigNode retainedConcreteValue = FindRetainedConcreteValue(path, loadResult, currentDefaults);

            if (retainedConcreteValue == null)
                throw new NotSupportedException(
                    "A missing semantic null field cannot be persisted without a truthful retained concrete value.");

            return ConfigTomlSourceUpdater.SetOrInsertNullValue(source, path, retainedConcreteValue);
        }

        private static ConfigNode FindRetainedConcreteValue(ConfigValuePath path, ConfigPersistedLoadResult loadResult, 
                                                            ConfigDocument currentDefaults)
        {
            foreach (ConfigDefaultChange change in loadResult.Changes)
            {
                if (!change.Path.Equals(path))
                    continue;

                if (IsConcrete(change.PlayerValue))
                    return change.PlayerValue;

                if (IsConcrete(change.BaselineDefault))
                    return change.BaselineDefault;

                if (IsConcrete(change.CurrentDefault))
                    return change.CurrentDefault;
            }

            ConfigNode value;

            if (loadResult.State.BaselineDefaults.TryGet(path, out value) && IsConcrete(value))
                return value;

            if (currentDefaults.TryGet(path, out value) && IsConcrete(value))
                return value;

            return null;
        }

        private static ConfigPersistedSourcePlan CreateCanonicalPlan(ConfigPersistedLoadResult loadResult,
                                                                     ConfigDocument target, Exception sourcePreservationFailure)
        {
            if (ContainsNull(target.Root))
            {
                const string message = "Canonical TOML regeneration cannot represent semantic null values, " +
                                       "and source-preserving persistence was not sufficient.";

                if (sourcePreservationFailure != null)
                    throw new NotSupportedException(message, sourcePreservationFailure);

                throw new NotSupportedException(message);
            }

            string source = Toml.Write(ConfigTomlDocumentCodec.ToTomlDocument(target));

            bool requiresBackup = loadResult.RequiresBackup || !loadResult.WasActiveFileMissing;

            return new ConfigPersistedSourcePlan(source, requiresBackup, true);
        }

        private static bool ContainsNull(ConfigNode node)
        {
            if (node is ConfigNullNode)
                return true;

            var obj = node as ConfigObjectNode;

            if (obj != null)
                return obj.Entries.Any(t => ContainsNull(t.Value));

            var array =
                node as ConfigArrayNode;

            return array != null && array.Items.Any(ContainsNull);
        }

        private static bool IsConcrete(ConfigNode node) => node != null && !(node is ConfigNullNode);

        private static bool IsSourcePreservationFailure(Exception exception)
        {
            return exception is KeyNotFoundException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException ||
                exception is ArgumentException;
        }
    }
}