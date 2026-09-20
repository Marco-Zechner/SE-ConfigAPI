using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.Toml;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigTomlSyntaxIndexTests
    {
        [Test]
        public void Create_Rejects_Null_Or_Unsuccessful_Parse()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSyntaxIndex.Create(null));

                Assert.Throws<ArgumentException>(() =>
                    ConfigTomlSyntaxIndex.Create(Toml.TryParse("broken =")));
            });
        }

        [Test]
        public void Index_Maps_Root_Dotted_Quoted_And_Table_Assignments_In_Source_Order()
        {
            var parsed = Toml.TryParse(
                "root = 1\n" +
                "\"quoted.key\" = 2\n" +
                "nested.value = 3\n" +
                "[section.\"deep.name\"]\n" +
                "flag = true\n" +
                "child.value = 4\n");

            var index = ConfigTomlSyntaxIndex.Create(parsed);

            Assert.Multiple(() =>
            {
                Assert.That(index.Assignments.Count, Is.EqualTo(5));
                AssertPath(index.Assignments[0].Path, "root");
                AssertPath(index.Assignments[1].Path, "quoted.key");
                AssertPath(index.Assignments[2].Path, "nested", "value");
                AssertPath(index.Assignments[3].Path, "section", "deep.name", "flag");
                AssertPath(index.Assignments[4].Path, "section", "deep.name", "child", "value");

                Assert.That(index.Assignments[0].Node.Kind, Is.EqualTo(TomlSyntaxNodeKind.Assignment));
                Assert.That(index.Assignments[4].Node.Kind, Is.EqualTo(TomlSyntaxNodeKind.Assignment));
                Assert.That(index.UnaddressableAssignments.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void Index_Maps_Disabled_Assignments_Without_Giving_Them_Toml_Semantics()
        {
            var parsed = Toml.TryParse(
                "#!optional = \"old\"\n" +
                "[section]\n" +
                "#! child = 7\n");

            var index = ConfigTomlSyntaxIndex.Create(parsed);

            Assert.Multiple(() =>
            {
                Assert.That(index.Assignments.Count, Is.EqualTo(2));

                AssertPath(index.Assignments[0].Path, "optional");
                Assert.That(index.Assignments[0].IsDisabled, Is.True);
                Assert.That(index.Assignments[0].Node.Kind, Is.EqualTo(TomlSyntaxNodeKind.DisabledAssignment));

                AssertPath(index.Assignments[1].Path, "section", "child");
                Assert.That(index.Assignments[1].IsDisabled, Is.True);

                Assert.That(parsed.Document.Root.ContainsKey("optional"), Is.False);
            });
        }

        [Test]
        public void Index_Reports_Assignments_Inside_Array_Of_Tables_As_Unaddressable()
        {
            var parsed = Toml.TryParse(
                "[[items]]\n" +
                "name = \"first\"\n" +
                "[items.details]\n" +
                "value = 10\n" +
                "[normal]\n" +
                "value = 20\n");

            var index = ConfigTomlSyntaxIndex.Create(parsed);

            Assert.Multiple(() =>
            {
                Assert.That(index.Assignments.Count, Is.EqualTo(1));
                AssertPath(index.Assignments[0].Path, "normal", "value");

                Assert.That(index.UnaddressableAssignments.Count, Is.EqualTo(2));
                Assert.That(index.UnaddressableAssignments[0].Kind, Is.EqualTo(TomlSyntaxNodeKind.Assignment));
                Assert.That(index.UnaddressableAssignments[1].Kind, Is.EqualTo(TomlSyntaxNodeKind.Assignment));
            });
        }

        [Test]
        public void Index_Returns_To_Addressable_Context_After_Array_Table_Context()
        {
            var parsed = Toml.TryParse(
                "[[items]]\n" +
                "name = \"first\"\n" +
                "[other]\n" +
                "enabled = true\n" +
                "count = 2\n");

            var index = ConfigTomlSyntaxIndex.Create(parsed);

            Assert.Multiple(() =>
            {
                Assert.That(index.UnaddressableAssignments.Count, Is.EqualTo(1));
                Assert.That(index.Assignments.Count, Is.EqualTo(2));
                AssertPath(index.Assignments[0].Path, "other", "enabled");
                AssertPath(index.Assignments[1].Path, "other", "count");
            });
        }

        [Test]
        public void Index_Rejects_Empty_Toml_Key_Because_ConfigValuePath_Cannot_Represent_It()
        {
            var parsed = Toml.TryParse("\"\" = 1\n");

            Assert.Throws<NotSupportedException>(() =>
                ConfigTomlSyntaxIndex.Create(parsed));
        }

        private static void AssertPath(ConfigValuePath actual, params string[] expected)
        {
            Assert.That(actual.Equals(new ConfigValuePath(expected)), Is.True);
        }
    }
}
