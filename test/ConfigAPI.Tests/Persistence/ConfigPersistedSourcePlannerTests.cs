using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigPersistedSourcePlannerTests
    {
        [Test]
        public void Missing_Provenance_Filled_Value_Is_Persisted_Even_Without_Reconciliation_Change()
        {
            var identity = Identity();
            var currentDefaults =
                Document(
                    Entry("Existing", Integer(5)),
                    Entry("Added", Integer(30)));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Existing = 5\n" +
                "# keep tail\n");

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults);

            var decoded =
                ConfigTomlSourceDecoder.Decode(
                    plan.ActiveSource,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    loadResult.Changes.Count,
                    Is.EqualTo(0));

                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.False);

                Assert.That(
                    plan.RequiresBackup,
                    Is.False);

                Assert.That(
                    plan.ActiveSource,
                    Does.Contain("Existing = 5\n"));

                Assert.That(
                    plan.ActiveSource,
                    Does.Contain("# keep tail\n"));

                Assert.That(
                    plan.ActiveSource,
                    Does.Contain("Added = 30"));

                Assert.That(
                    decoded.Equals(
                        loadResult.State.PlayerValues),
                    Is.True);
            });
        }

        [Test]
        public void Existing_Value_Update_Preserves_Assignment_Trivia()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry("Value", Integer(10)));

            var currentDefaults =
                Document(
                    Entry("Value", Integer(20)));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Value = 10   # keep\n");

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    loadResult.RequiresBackup,
                    Is.False);

                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.False);

                Assert.That(
                    plan.RequiresBackup,
                    Is.False);

                Assert.That(
                    plan.ActiveSource,
                    Is.EqualTo(
                        "Value = 20   # keep\n"));
            });
        }

        [Test]
        public void Existing_Disabled_Null_Assignment_Is_Preserved_Exactly()
        {
            var identity = Identity();
            var defaults =
                Document(
                    Entry(
                        "Optional",
                        ConfigNullNode.Instance));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "#!Optional = \"previous\"   # keep\n");

            SetProvenance(
                storage,
                identity,
                defaults);

            var loadResult =
                Load(
                    storage,
                    identity,
                    defaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    defaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.False);

                Assert.That(
                    plan.RequiresBackup,
                    Is.False);

                Assert.That(
                    plan.ActiveSource,
                    Is.EqualTo(
                        "#!Optional = \"previous\"   # keep\n"));
            });
        }

        [Test]
        public void Missing_Null_Assignment_Uses_Historical_Baseline_As_Retained_Source()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry("Optional", Integer(5)),
                    Entry("Other", Integer(1)));

            var currentDefaults =
                Document(
                    Entry(
                        "Optional",
                        ConfigNullNode.Instance),
                    Entry("Other", Integer(1)));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Other = 1\n");

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults);

            var decoded =
                ConfigTomlSourceDecoder.Decode(
                    plan.ActiveSource,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    loadResult.RequiresBackup,
                    Is.True);

                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.False);

                Assert.That(
                    plan.RequiresBackup,
                    Is.True);

                Assert.That(
                    plan.ActiveSource,
                    Does.Contain("#!Optional = 5"));

                Assert.That(
                    decoded.Equals(
                        loadResult.State.PlayerValues),
                    Is.True);
            });
        }

        [Test]
        public void Removed_Table_Falls_Back_To_Canonical_Target_Without_Stale_Header()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry(
                        "Legacy",
                        Object(
                            Entry(
                                "Value",
                                Integer(9)))));

            var currentDefaults =
                Document(
                    Entry("Known", Integer(1)));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "[Legacy]\n" +
                "Value = 9\n");

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults);

            var decoded =
                ConfigTomlSourceDecoder.Decode(
                    plan.ActiveSource,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.True);

                Assert.That(
                    plan.RequiresBackup,
                    Is.True);

                Assert.That(
                    plan.ActiveSource,
                    Does.Not.Contain("Legacy"));

                Assert.That(
                    plan.ActiveSource,
                    Does.Contain("Known = 1"));

                Assert.That(
                    decoded.Equals(
                        loadResult.State.PlayerValues),
                    Is.True);
            });
        }

        [Test]
        public void Unsupported_Source_Layout_Uses_Canonical_Fallback_And_Requires_Backup()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry(
                        "Section",
                        Object(
                            Entry(
                                "Value",
                                Integer(1)))));

            var currentDefaults =
                Document(
                    Entry(
                        "Section",
                        Object(
                            Entry(
                                "Value",
                                Integer(2)))));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Section = { Value = 1 }\n");

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            var plan =
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults);

            var decoded =
                ConfigTomlSourceDecoder.Decode(
                    plan.ActiveSource,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    loadResult.RequiresBackup,
                    Is.False);

                Assert.That(
                    plan.UsedCanonicalRegeneration,
                    Is.True);

                Assert.That(
                    plan.RequiresBackup,
                    Is.True);

                Assert.That(
                    decoded.Equals(
                        loadResult.State.PlayerValues),
                    Is.True);
            });
        }

        [Test]
        public void Canonical_Fallback_With_Semantic_Null_Is_Rejected_Instead_Of_Inventing_Source()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry(
                        "Optional",
                        ConfigNullNode.Instance),
                    Entry(
                        "Legacy",
                        Object(
                            Entry(
                                "Value",
                                Integer(9)))));

            var currentDefaults =
                Document(
                    Entry(
                        "Optional",
                        ConfigNullNode.Instance));

            var storage = new MemoryStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "#!Optional = 5\n" +
                "\n" +
                "[Legacy]\n" +
                "Value = 9\n");

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            Assert.Throws<NotSupportedException>(() =>
                ConfigPersistedSourcePlanner.Plan(
                    loadResult,
                    currentDefaults));
        }

        private static ConfigPersistedLoadResult Load(
            MemoryStorage storage,
            ConfigIdentity identity,
            ConfigDocument currentDefaults)
        {
            return new ConfigPersistedStateLoader(
                storage)
                .Load(
                    ConfigLocation.World,
                    "settings.toml",
                    identity,
                    currentDefaults);
        }

        private static void SetProvenance(
            MemoryStorage storage,
            ConfigIdentity identity,
            ConfigDocument baseline)
        {
            storage.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(
                    new ConfigProvenance(
                        identity,
                        baseline)));
        }

        private static ConfigIdentity Identity()
        {
            return new ConfigIdentity(
                "12345",
                "Settings");
        }

        private static ConfigDocument Document(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(
                Object(entries));
        }

        private static ConfigObjectNode Object(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigObjectNode(entries);
        }

        private static ConfigObjectEntry Entry(
            string name,
            ConfigNode value)
        {
            return new ConfigObjectEntry(
                name,
                value);
        }

        private static ConfigScalarNode Integer(
            long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private sealed class MemoryStorage : IConfigTextStorage
        {
            private readonly Dictionary<string, string> _content =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            public void Set(
                ConfigLocation location,
                string file,
                string content)
            {
                _content[
                    CreateKey(
                        location,
                        file)] = content;
            }

            public string Read(
                ConfigLocation location,
                string file)
            {
                string content;

                return _content.TryGetValue(
                    CreateKey(
                        location,
                        file),
                    out content)
                    ? content
                    : null;
            }

            public void Write(
                ConfigLocation location,
                string file,
                string content)
            {
                Set(
                    location,
                    file,
                    content);
            }

            private static string CreateKey(
                ConfigLocation location,
                string file)
            {
                return location +
                    "|" +
                    file;
            }
        }
    }
}