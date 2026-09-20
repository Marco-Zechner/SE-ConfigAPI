using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.Toml;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigTomlSourceUpdaterTests
    {
        [Test]
        public void SetValue_Replaces_Only_Value_Source()
        {
            var source =
                "# user comment\r\n" +
                "answer   = 1   # keep this\r\n" +
                "other = \"unchanged\"\r\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("answer"),
                ConfigScalarNode.Integer(42));

            Assert.That(
                edited,
                Is.EqualTo(
                    "# user comment\r\n" +
                    "answer   = 42   # keep this\r\n" +
                    "other = \"unchanged\"\r\n"));
        }

        [Test]
        public void SetValue_Null_Disables_Assignment_Without_Losing_Previous_Value()
        {
            var source = "optional = \"custom\" # retained\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("optional"),
                ConfigNullNode.Instance);

            Assert.That(
                edited,
                Is.EqualTo("#!optional = \"custom\" # retained\n"));
        }

        [Test]
        public void SetValue_Null_Leaves_Already_Disabled_Assignment_Unchanged()
        {
            var source = "#!optional = \"old\" # retained\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("optional"),
                ConfigNullNode.Instance);

            Assert.That(edited, Is.EqualTo(source));
        }

        [Test]
        public void SetValue_Concrete_Value_Enables_Disabled_Assignment_And_Replaces_Value()
        {
            var source = "#!optional = \"old\" # retained\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("optional"),
                ConfigScalarNode.String("new"));

            Assert.That(
                edited,
                Is.EqualTo("optional = \"new\" # retained\n"));
        }

        [Test]
        public void SetValue_Resolves_Dotted_And_Quoted_Config_Path()
        {
            var source = "\"section.name\".child = 1\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("section.name", "child"),
                ConfigScalarNode.Integer(5));

            Assert.That(
                edited,
                Is.EqualTo("\"section.name\".child = 5\n"));
        }

        [Test]
        public void SetValue_Renders_Object_And_Array_As_One_Canonical_Toml_Value()
        {
            var source = "settings = { old = 1 } # retained\n";

            var edited = ConfigTomlSourceUpdater.SetValue(
                source,
                new ConfigValuePath("settings"),
                new ConfigObjectNode(
                    new ConfigObjectEntry(
                        "Name",
                        ConfigScalarNode.String("Flight")),
                    new ConfigObjectEntry(
                        "Flags",
                        new ConfigArrayNode(
                            ConfigScalarNode.Boolean(true),
                            ConfigScalarNode.Boolean(false)))));

            var parsed = Toml.Parse(edited);
            var config = ConfigTomlDocumentCodec.FromTomlDocument(parsed);

            ConfigNode actual;

            Assert.Multiple(() =>
            {
                Assert.That(
                    edited,
                    Is.EqualTo(
                        "settings = {Name = \"Flight\", Flags = [true, false]} # retained\n"));

                Assert.That(
                    config.TryGet(new ConfigValuePath("settings"), out actual),
                    Is.True);

                Assert.That(
                    actual,
                    Is.EqualTo(
                        new ConfigObjectNode(
                            new ConfigObjectEntry(
                                "Name",
                                ConfigScalarNode.String("Flight")),
                            new ConfigObjectEntry(
                                "Flags",
                                new ConfigArrayNode(
                                    ConfigScalarNode.Boolean(true),
                                    ConfigScalarNode.Boolean(false))))));
            });
        }

        [Test]
        public void SetOrInsertValue_Inserts_Missing_Root_Field_Before_First_Table()
        {
            var source =
                "existing = 1\r\n" +
                "# keep section comment\r\n" +
                "[section]\r\n" +
                "value = 2\r\n";

            var edited = ConfigTomlSourceUpdater.SetOrInsertValue(
                source,
                new ConfigValuePath("new.key"),
                ConfigScalarNode.String("added"));

            Assert.That(
                edited,
                Is.EqualTo(
                    "existing = 1\r\n" +
                    "\"new.key\" = \"added\"\r\n" +
                    "# keep section comment\r\n" +
                    "[section]\r\n" +
                    "value = 2\r\n"));
        }

        [Test]
        public void SetOrInsertValue_Inserts_Inside_Existing_Table_After_Complete_Assignment_Line()
        {
            var source =
                "[section]\n" +
                "existing = 1 # keep inline\n" +
                "# keep before next section\n" +
                "\n" +
                "[next]\n" +
                "value = 2\n";

            var edited = ConfigTomlSourceUpdater.SetOrInsertValue(
                source,
                new ConfigValuePath("section", "added"),
                ConfigScalarNode.Boolean(true));

            Assert.That(
                edited,
                Is.EqualTo(
                    "[section]\n" +
                    "existing = 1 # keep inline\n" +
                    "added = true\n" +
                    "# keep before next section\n" +
                    "\n" +
                    "[next]\n" +
                    "value = 2\n"));
        }

        [Test]
        public void SetOrInsertValue_Appends_New_Quoted_Nested_Table()
        {
            var source = "root = 1\n";

            var edited = ConfigTomlSourceUpdater.SetOrInsertValue(
                source,
                new ConfigValuePath(
                    "section.name",
                    "child table",
                    "new-value"),
                new ConfigArrayNode(
                    ConfigScalarNode.Integer(1),
                    ConfigScalarNode.Integer(2)));

            Assert.That(
                edited,
                Is.EqualTo(
                    "root = 1\n" +
                    "\n" +
                    "[\"section.name\".\"child table\"]\n" +
                    "new-value = [1, 2]\n"));

            var parsed = Toml.Parse(edited);
            var config =
                ConfigTomlDocumentCodec.FromTomlDocument(
                    parsed);

            ConfigNode actual;

            Assert.That(
                config.TryGet(
                    new ConfigValuePath(
                        "section.name",
                        "child table",
                        "new-value"),
                    out actual),
                Is.True);

            Assert.That(
                actual,
                Is.EqualTo(
                    new ConfigArrayNode(
                        ConfigScalarNode.Integer(1),
                        ConfigScalarNode.Integer(2))));
        }

        [Test]
        public void SetOrInsertValue_Uses_Existing_Update_When_Field_Already_Exists()
        {
            var source =
                "value   = 1   # keep\n";

            var edited = ConfigTomlSourceUpdater.SetOrInsertValue(
                source,
                new ConfigValuePath("value"),
                ConfigScalarNode.Integer(5));

            Assert.That(
                edited,
                Is.EqualTo(
                    "value   = 5   # keep\n"));
        }

        [Test]
        public void SetOrInsertValue_Rejects_New_Null_And_Array_Table_Context()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<NotSupportedException>(() =>
                    ConfigTomlSourceUpdater.SetOrInsertValue(
                        "value = 1\n",
                        new ConfigValuePath("optional"),
                        ConfigNullNode.Instance));

                Assert.Throws<NotSupportedException>(() =>
                    ConfigTomlSourceUpdater.SetOrInsertValue(
                        "[[items]]\n" +
                        "name = \"first\"\n",
                        new ConfigValuePath(
                            "items",
                            "weight"),
                        ConfigScalarNode.Float(2.5)));
            });
        }

        [Test]
        public void SetOrInsertValue_Rejects_Dotted_Table_Conflict_Instead_Of_Rewriting_Source()
        {
            Assert.Throws<NotSupportedException>(() =>
                ConfigTomlSourceUpdater.SetOrInsertValue(
                    "section.old = 1\n",
                    new ConfigValuePath(
                        "section",
                        "added"),
                    ConfigScalarNode.Integer(2)));
        }

        [Test]
        public void RemoveValue_Removes_Complete_Assignment_Line_And_Preserves_Surrounding_Source()
        {
            var source =
                "# before\r\n" +
                "  obsolete = 5   # remove with field\r\n" +
                "# after\r\n" +
                "keep = 1\r\n";

            var edited = ConfigTomlSourceUpdater.RemoveValue(
                source,
                new ConfigValuePath("obsolete"));

            Assert.That(
                edited,
                Is.EqualTo(
                    "# before\r\n" +
                    "# after\r\n" +
                    "keep = 1\r\n"));
        }

        [Test]
        public void RemoveValue_Removes_Disabled_Assignment_And_Rejects_Missing_Or_Ambiguous_Path()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    ConfigTomlSourceUpdater.RemoveValue(
                        "#!optional = \"old\"\nkeep = 1\n",
                        new ConfigValuePath("optional")),
                    Is.EqualTo("keep = 1\n"));

                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigTomlSourceUpdater.RemoveValue(
                        "value = 1\n",
                        new ConfigValuePath("missing")));

                Assert.Throws<InvalidOperationException>(() =>
                    ConfigTomlSourceUpdater.RemoveValue(
                        "#!value = 1\n#!value = 2\n",
                        new ConfigValuePath("value")));
            });
        }

        [Test]
        public void SetOrInsertNullValue_Inserts_Disabled_Assignment_With_Truthful_Retained_Value()
        {
            var edited =
                ConfigTomlSourceUpdater.SetOrInsertNullValue(
                    "value = 1\n",
                    new ConfigValuePath("optional"),
                    ConfigScalarNode.String("previous"));

            Assert.That(
                edited,
                Is.EqualTo(
                    "value = 1\n" +
                    "#!optional = \"previous\"\n"));
        }

        [Test]
        public void SetOrInsertNullValue_Preserves_Existing_Assignment_Value()
        {
            var edited =
                ConfigTomlSourceUpdater.SetOrInsertNullValue(
                    "optional   = \"custom\"   # keep\n",
                    new ConfigValuePath("optional"),
                    ConfigScalarNode.String("fallback"));

            Assert.That(
                edited,
                Is.EqualTo(
                    "#!optional   = \"custom\"   # keep\n"));
        }

        [Test]
        public void SetOrInsertNullValue_Rejects_Missing_Or_Null_Retained_Concrete_Value()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceUpdater.SetOrInsertNullValue(
                        "value = 1\n",
                        new ConfigValuePath("optional"),
                        null));

                Assert.Throws<ArgumentException>(() =>
                    ConfigTomlSourceUpdater.SetOrInsertNullValue(
                        "value = 1\n",
                        new ConfigValuePath("optional"),
                        ConfigNullNode.Instance));
            });
        }

        [Test]
        public void SetValue_Rejects_Missing_Or_Ambiguous_Assignment()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        "value = 1\n",
                        new ConfigValuePath("missing"),
                        ConfigScalarNode.Integer(2)));

                Assert.Throws<InvalidOperationException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        "#!value = 1\n#!value = 2\n",
                        new ConfigValuePath("value"),
                        ConfigScalarNode.Integer(3)));
            });
        }

        [Test]
        public void SetValue_Rejects_Invalid_Source_And_Null_Arguments()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        null,
                        new ConfigValuePath("value"),
                        ConfigScalarNode.Integer(1)));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        "value = 1\n",
                        null,
                        ConfigScalarNode.Integer(1)));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        "value = 1\n",
                        new ConfigValuePath("value"),
                        null));

                Assert.Throws<ArgumentException>(() =>
                    ConfigTomlSourceUpdater.SetValue(
                        "value =",
                        new ConfigValuePath("value"),
                        ConfigScalarNode.Integer(1)));
            });
        }
    }
}
