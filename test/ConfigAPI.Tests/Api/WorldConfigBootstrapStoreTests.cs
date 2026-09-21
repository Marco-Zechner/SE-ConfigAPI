using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigBootstrapStoreTests
    {
        [Test]
        public void Write_And_TryRead_RoundTrip_Whole_Snapshot_And_Replace_Previous_Value()
        {
            var variables = new MemoryVariables();
            var store = new WorldConfigBootstrapStore(variables);
            var identity = new ConfigIdentity("Example.Mod", "Settings");

            store.Write(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(10))), 3UL, "settings.toml"));
            store.Write(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(20))), 4UL, "alternate.toml"));

            WorldConfigSnapshot snapshot;
            Assert.That(store.TryRead(identity, out snapshot), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(variables.Count, Is.EqualTo(1));
                Assert.That(snapshot.Identity, Is.EqualTo(identity));
                Assert.That(snapshot.ServerIteration, Is.EqualTo(4UL));
                Assert.That(snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(snapshot.Document, 20);
            });
        }

        [Test]
        public void TryRead_Missing_Malformed_Or_Throwing_Variable_Is_A_Cache_Miss()
        {
            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var variables = new MemoryVariables();
            var store = new WorldConfigBootstrapStore(variables);

            WorldConfigSnapshot snapshot;
            Assert.That(store.TryRead(identity, out snapshot), Is.False);
            Assert.That(snapshot, Is.Null);

            variables.SetOnlyValue("not-base64");
            Assert.That(store.TryRead(identity, out snapshot), Is.False);
            Assert.That(snapshot, Is.Null);

            variables.ThrowOnRead = true;
            Assert.That(store.TryRead(identity, out snapshot), Is.False);
            Assert.That(snapshot, Is.Null);
        }

        [Test]
        public void TryRead_Rejects_Snapshot_For_Different_Identity()
        {
            var variables = new MemoryVariables();
            var store = new WorldConfigBootstrapStore(variables);
            var storedIdentity = new ConfigIdentity("Example.Mod", "Settings");
            var requestedIdentity = new ConfigIdentity("Example.Mod", "Other");

            store.Write(new WorldConfigSnapshot(storedIdentity, Document(Entry("Value", Integer(10))), 1UL, "settings.toml"));
            variables.CopyOnlyValueTo(StoreVariableName(requestedIdentity));

            WorldConfigSnapshot snapshot;
            Assert.That(store.TryRead(requestedIdentity, out snapshot), Is.False);
            Assert.That(snapshot, Is.Null);
        }

        [Test]
        public void Write_Failure_Is_FailSoft()
        {
            var variables = new MemoryVariables { ThrowOnWrite = true };
            var store = new WorldConfigBootstrapStore(variables);
            var snapshot = new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(10))), 0UL, "settings.toml");

            Assert.DoesNotThrow(() => store.Write(snapshot));
        }

        private static string StoreVariableName(ConfigIdentity identity) => "ConfigAPI.World.Bootstrap|" + identity.OwnerId.Length + ":" + identity.OwnerId + identity.ConfigKey.Length + ":" + identity.ConfigKey;
        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected)
        {
            ConfigNode value;
            Assert.That(document.TryGet(new ConfigValuePath("Value"), out value), Is.True);
            Assert.That(value, Is.EqualTo(Integer(expected)));
        }

        private sealed class MemoryVariables : IWorldConfigBootstrapVariables
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

            public bool ThrowOnRead { get; set; }
            public bool ThrowOnWrite { get; set; }
            public int Count => _values.Count;

            public bool TryRead(string name, out string value)
            {
                if (ThrowOnRead)
                    throw new InvalidOperationException("Synthetic read failure.");

                return _values.TryGetValue(name, out value);
            }

            public void Write(string name, string value)
            {
                if (ThrowOnWrite)
                    throw new InvalidOperationException("Synthetic write failure.");

                _values[name] = value;
            }

            public void SetOnlyValue(string value)
            {
                if (_values.Count == 0)
                    _values["ConfigAPI.World.Bootstrap|11:Example.Mod8:Settings"] = value;
                else
                {
                    string key = null;
                    foreach (string existing in _values.Keys) { key = existing; break; }
                    _values[key] = value;
                }
            }

            public void CopyOnlyValueTo(string name)
            {
                string value = null;
                foreach (string existing in _values.Values) { value = existing; break; }
                _values[name] = value;
            }
        }
    }
}