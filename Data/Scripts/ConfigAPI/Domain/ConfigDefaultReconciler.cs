using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public static class ConfigDefaultReconciler
    {
        public static ConfigDefaultReconciliationResult Reconcile(
            ConfigDocument baselineDefaults, ConfigDocument playerValues, ConfigDocument currentDefaults)
        {
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            var changes = new List<ConfigDefaultChange>();
            var path = new List<string>();
            var requiresBackup = false;

            ObjectReconciliation reconciled = ReconcileObject(
                baselineDefaults.Root, playerValues.Root, currentDefaults.Root, path, changes, ref requiresBackup);

            return new ConfigDefaultReconciliationResult(
                new ConfigDocument(reconciled.Baseline),
                new ConfigDocument(reconciled.Player),
                changes, requiresBackup);
        }

        private static ObjectReconciliation ReconcileObject(
            ConfigObjectNode baseline, ConfigObjectNode player, ConfigObjectNode currentDefaults,
            List<string> path, IList<ConfigDefaultChange> changes, ref bool requiresBackup)
        {
            var baselineEntries = new List<ConfigObjectEntry>(currentDefaults.Entries.Count);
            var playerEntries = new List<ConfigObjectEntry>(currentDefaults.Entries.Count);

            foreach (ConfigObjectEntry entry in currentDefaults.Entries)
            {
                ConfigNode baselineValue;
                ConfigNode playerValue;

                bool hasBaseline = baseline.TryGet(entry.Name, out baselineValue);
                bool hasPlayer = player.TryGet(entry.Name, out playerValue);

                path.Add(entry.Name);

                if (!hasBaseline && !hasPlayer)
                {
                    baselineEntries.Add(new ConfigObjectEntry(entry.Name, entry.Value));
                    playerEntries.Add(new ConfigObjectEntry(entry.Name, entry.Value));
                    AddNewDefaultChanges(entry.Value, path, changes);
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                if (hasBaseline != hasPlayer)
                {
                    requiresBackup = true;

                    changes.Add(
                        new ConfigDefaultChange(ConfigDefaultChangeKind.ResetIncompatibleStructure, CreatePath(path), 
                                                baselineValue, playerValue, entry.Value));

                    baselineEntries.Add(new ConfigObjectEntry(entry.Name, entry.Value));
                    playerEntries.Add(new ConfigObjectEntry(entry.Name, entry.Value));
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                ValueReconciliation valueResult = ReconcileValue(
                    baselineValue, playerValue, entry.Value, path, changes, ref requiresBackup);

                baselineEntries.Add(new ConfigObjectEntry(entry.Name, valueResult.Baseline));
                playerEntries.Add(new ConfigObjectEntry(entry.Name, valueResult.Player));

                path.RemoveAt(path.Count - 1);
            }

            ReportRemovedEntries(baseline, player, currentDefaults, path, changes, ref requiresBackup);

            return new ObjectReconciliation(new ConfigObjectNode(baselineEntries.ToArray()),
                                            new ConfigObjectNode(playerEntries.ToArray()));
        }

        private static ValueReconciliation ReconcileValue(
            ConfigNode baseline, ConfigNode player, ConfigNode currentDefault,
            List<string> path, IList<ConfigDefaultChange> changes, ref bool requiresBackup)
        {
            if (HasIncompatibleStructure(baseline, player, currentDefault))
            {
                requiresBackup = true;

                changes.Add(
                    new ConfigDefaultChange(ConfigDefaultChangeKind.ResetIncompatibleStructure, CreatePath(path),
                                            baseline, player, currentDefault));

                return new ValueReconciliation(currentDefault, currentDefault);
            }

            var baselineObject = baseline as ConfigObjectNode;
            var playerObject = player as ConfigObjectNode;
            var currentObject = currentDefault as ConfigObjectNode;

            if (baselineObject != null && playerObject != null && currentObject != null)
            {
                ObjectReconciliation reconciledObject = ReconcileObject(
                    baselineObject, playerObject, currentObject, path, changes, ref requiresBackup);

                return new ValueReconciliation(reconciledObject.Baseline, reconciledObject.Player);
            }

            if (baseline.Equals(currentDefault))
                return new ValueReconciliation(baseline, player);

            ConfigValuePath changePath = CreatePath(path);

            if (player.Equals(baseline))
            {
                changes.Add(
                    new ConfigDefaultChange(ConfigDefaultChangeKind.AppliedChangedDefault, changePath, baseline, player, currentDefault));

                return new ValueReconciliation(currentDefault, currentDefault);
            }

            changes.Add(
                new ConfigDefaultChange(ConfigDefaultChangeKind.PendingChangedDefault, changePath, baseline, player, currentDefault));

            return new ValueReconciliation(baseline, player);
        }

        private static void ReportRemovedEntries(
            ConfigObjectNode baseline, ConfigObjectNode player, ConfigObjectNode currentDefaults,
            List<string> path, IList<ConfigDefaultChange> changes, ref bool requiresBackup)
        {
            foreach (ConfigObjectEntry entry in baseline.Entries)
            {
                ConfigNode ignored;
                if (currentDefaults.TryGet(entry.Name, out ignored))
                    continue;

                ConfigNode playerValue;
                player.TryGet(entry.Name, out playerValue);

                path.Add(entry.Name);

                changes.Add(
                    new ConfigDefaultChange(ConfigDefaultChangeKind.RemovedValue, CreatePath(path),
                                            entry.Value, playerValue, null));

                path.RemoveAt(path.Count - 1);
                requiresBackup = true;
            }

            foreach (ConfigObjectEntry entry in player.Entries)
            {
                ConfigNode ignored;
                if (currentDefaults.TryGet(entry.Name, out ignored))
                    continue;

                if (baseline.TryGet(entry.Name, out ignored))
                    continue;

                path.Add(entry.Name);

                changes.Add(
                    new ConfigDefaultChange(ConfigDefaultChangeKind.RemovedValue, CreatePath(path),
                                            null, entry.Value, null));

                path.RemoveAt(path.Count - 1);
                requiresBackup = true;
            }
        }

        private static bool HasIncompatibleStructure(ConfigNode baseline, ConfigNode player, ConfigNode currentDefault)
        {
            var shape = ConfigNodeShape.None;

            if (!AcceptShape(baseline, ref shape))
                return true;

            if (!AcceptShape(player, ref shape))
                return true;

            if (!AcceptShape(currentDefault, ref shape))
                return true;

            ConfigScalarKind? scalarKind = null;

            if (!AcceptScalarKind(baseline, ref scalarKind))
                return true;

            if (!AcceptScalarKind(player, ref scalarKind))
                return true;

            return !AcceptScalarKind(currentDefault, ref scalarKind);
        }

        private static bool AcceptShape(ConfigNode node, ref ConfigNodeShape shape)
        {
            if (node is ConfigNullNode)
                return true;

            ConfigNodeShape nodeShape;

            if (node is ConfigObjectNode)
                nodeShape = ConfigNodeShape.Object;
            else if (node is ConfigArrayNode)
                nodeShape = ConfigNodeShape.Array;
            else
                nodeShape = ConfigNodeShape.Scalar;

            if (shape != ConfigNodeShape.None)
                return shape == nodeShape;
            
            shape = nodeShape;
            return true;

        }

        private static bool AcceptScalarKind(ConfigNode node, ref ConfigScalarKind? kind)
        {
            var scalar = node as ConfigScalarNode;
            if (scalar == null)
                return true;

            if (kind.HasValue)
                return kind.Value == scalar.Kind;
            
            kind = scalar.Kind;
            return true;

        }

        private static void AddNewDefaultChanges(ConfigNode currentDefault, List<string> path, IList<ConfigDefaultChange> changes)
        {
            var obj = currentDefault as ConfigObjectNode;
            if (obj == null)
            {
                changes.Add(
                    new ConfigDefaultChange(
                        ConfigDefaultChangeKind.AddedDefault, CreatePath(path),
                        null, null, currentDefault));

                return;
            }

            foreach (ConfigObjectEntry entry in obj.Entries)
            {
                path.Add(entry.Name);
                AddNewDefaultChanges(entry.Value, path, changes);
                path.RemoveAt(path.Count - 1);
            }
        }

        private static ConfigValuePath CreatePath(List<string> path) => new ConfigValuePath(path.ToArray());

        private enum ConfigNodeShape
        {
            None = 0,
            Scalar = 1,
            Object = 2,
            Array = 3
        }

        private struct ObjectReconciliation
        {
            public readonly ConfigObjectNode Baseline;
            public readonly ConfigObjectNode Player;

            public ObjectReconciliation(ConfigObjectNode baseline, ConfigObjectNode player)
            {
                Baseline = baseline;
                Player = player;
            }
        }

        private struct ValueReconciliation
        {
            public readonly ConfigNode Baseline;
            public readonly ConfigNode Player;

            public ValueReconciliation(ConfigNode baseline, ConfigNode player)
            {
                Baseline = baseline;
                Player = player;
            }
        }
    }
}
