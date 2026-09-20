using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigDefaultStatusTests
    {
        [Test]
        public void Get_Returns_Current_Player_Baseline_And_Current_Default()
        {
            var path = new ConfigValuePath("Value");
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(20)));

            var status = ConfigDefaultStatus.Get(baseline, player, currentDefaults, path);

            Assert.Multiple(() =>
            {
                Assert.That(status.Path.Equals(path), Is.True);
                Assert.That(status.BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(status.PlayerValue.Equals(Integer(15)), Is.True);
                Assert.That(status.CurrentDefault.Equals(Integer(20)), Is.True);
            });
        }

        [Test]
        public void Unchanged_Default_Without_Override_Has_No_Derived_State()
        {
            var document = Document(Entry("Value", Integer(10)));

            var status = ConfigDefaultStatus.Get(
                document,
                document,
                document,
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.False);
                Assert.That(status.HasPendingDefaultChange, Is.False);
            });
        }

        [Test]
        public void Unchanged_Default_With_Player_Customization_Is_Override_Only()
        {
            var status = ConfigDefaultStatus.Get(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(15))),
                Document(Entry("Value", Integer(10))),
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.True);
                Assert.That(status.HasPendingDefaultChange, Is.False);
            });
        }

        [Test]
        public void Changed_Default_With_Player_Override_Is_Override_And_Pending()
        {
            var status = ConfigDefaultStatus.Get(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(15))),
                Document(Entry("Value", Integer(20))),
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.True);
                Assert.That(status.HasPendingDefaultChange, Is.True);
            });
        }

        [Test]
        public void Auto_Applied_Default_Has_No_Override_Or_Pending_State()
        {
            var reconciled = ConfigDefaultReconciler.Reconcile(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(20))));

            var status = ConfigDefaultStatus.Get(
                reconciled.BaselineDefaults,
                reconciled.PlayerValues,
                Document(Entry("Value", Integer(20))),
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.False);
                Assert.That(status.HasPendingDefaultChange, Is.False);
            });
        }

        [Test]
        public void RevertToDefault_Clears_Override_And_Pending_State()
        {
            var currentDefaults = Document(Entry("Value", Integer(20)));

            var reverted = ConfigDefaultOperations.RevertToDefault(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(15))),
                currentDefaults,
                new ConfigValuePath("Value"));

            var status = ConfigDefaultStatus.Get(
                reverted.BaselineDefaults,
                reverted.PlayerValues,
                currentDefaults,
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.False);
                Assert.That(status.HasPendingDefaultChange, Is.False);
            });
        }

        [Test]
        public void Matching_Current_Default_Without_Baseline_Advance_Remains_Pending()
        {
            var status = ConfigDefaultStatus.Get(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(20))),
                Document(Entry("Value", Integer(20))),
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                Assert.That(status.IsOverride, Is.False);
                Assert.That(status.HasPendingDefaultChange, Is.True);
            });
        }

        [Test]
        public void Nested_Status_Uses_Requested_Path()
        {
            var path = new ConfigValuePath("Display", "Width");

            var status = ConfigDefaultStatus.Get(
                Document(Entry("Display", Object(Entry("Width", Integer(100))))),
                Document(Entry("Display", Object(Entry("Width", Integer(150))))),
                Document(Entry("Display", Object(Entry("Width", Integer(200))))),
                path);

            Assert.Multiple(() =>
            {
                Assert.That(status.Path.Equals(path), Is.True);
                Assert.That(status.BaselineDefault.Equals(Integer(100)), Is.True);
                Assert.That(status.PlayerValue.Equals(Integer(150)), Is.True);
                Assert.That(status.CurrentDefault.Equals(Integer(200)), Is.True);
                Assert.That(status.IsOverride, Is.True);
                Assert.That(status.HasPendingDefaultChange, Is.True);
            });
        }

        [Test]
        public void Array_Status_Uses_Whole_Array_Value()
        {
            var baselineTags = new ConfigArrayNode(String("alpha"));
            var playerTags = new ConfigArrayNode(String("custom"));
            var currentTags = new ConfigArrayNode(String("alpha"), String("beta"));

            var status = ConfigDefaultStatus.Get(
                Document(Entry("Tags", baselineTags)),
                Document(Entry("Tags", playerTags)),
                Document(Entry("Tags", currentTags)),
                new ConfigValuePath("Tags"));

            Assert.Multiple(() =>
            {
                Assert.That(status.BaselineDefault.Equals(baselineTags), Is.True);
                Assert.That(status.PlayerValue.Equals(playerTags), Is.True);
                Assert.That(status.CurrentDefault.Equals(currentTags), Is.True);
                Assert.That(status.IsOverride, Is.True);
                Assert.That(status.HasPendingDefaultChange, Is.True);
            });
        }

        [Test]
        public void Get_Rejects_Path_Missing_From_Any_Document()
        {
            var present = Document(Entry("Value", Integer(10)));
            var missing = Document();
            var path = new ConfigValuePath("Value");

            Assert.Multiple(() =>
            {
                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigDefaultStatus.Get(missing, present, present, path));

                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigDefaultStatus.Get(present, missing, present, path));

                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigDefaultStatus.Get(present, present, missing, path));
            });
        }

        [Test]
        public void Get_Rejects_Null_Inputs()
        {
            var document = Document(Entry("Value", Integer(10)));
            var path = new ConfigValuePath("Value");

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultStatus.Get(null, document, document, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultStatus.Get(document, null, document, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultStatus.Get(document, document, null, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultStatus.Get(document, document, document, null));
            });
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }

        private static ConfigObjectNode Object(params ConfigObjectEntry[] entries)
        {
            return new ConfigObjectNode(entries);
        }

        private static ConfigScalarNode Integer(long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private static ConfigScalarNode String(string value)
        {
            return ConfigScalarNode.String(value);
        }
    }
}
