using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigNodeTests
    {
        [Test]
        public void Scalars_Use_Explicit_Semantic_Kinds()
        {
            var boolean = ConfigScalarNode.Boolean(true);
            var integer = ConfigScalarNode.Integer(1);
            var floatingPoint = ConfigScalarNode.Float(1.0);
            var text = ConfigScalarNode.String("1");

            Assert.Multiple(() =>
            {
                Assert.That(boolean.Kind, Is.EqualTo(ConfigScalarKind.Boolean));
                Assert.That(integer.Kind, Is.EqualTo(ConfigScalarKind.Integer));
                Assert.That(floatingPoint.Kind, Is.EqualTo(ConfigScalarKind.Float));
                Assert.That(text.Kind, Is.EqualTo(ConfigScalarKind.String));

                Assert.That(integer, Is.Not.EqualTo(floatingPoint));
                Assert.That(integer, Is.Not.EqualTo(text));
                Assert.That(floatingPoint, Is.Not.EqualTo(text));
            });
        }

        [Test]
        public void Scalars_Compare_By_Kind_And_Value()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ConfigScalarNode.Boolean(true).Equals(ConfigScalarNode.Boolean(true)), Is.True);
                Assert.That(ConfigScalarNode.Boolean(true).Equals(ConfigScalarNode.Boolean(false)), Is.False);

                Assert.That(ConfigScalarNode.Integer(42).Equals(ConfigScalarNode.Integer(42)), Is.True);
                Assert.That(ConfigScalarNode.Integer(42).Equals(ConfigScalarNode.Integer(43)), Is.False);

                Assert.That(ConfigScalarNode.Float(0.75).Equals(ConfigScalarNode.Float(0.75)), Is.True);
                Assert.That(ConfigScalarNode.Float(0.75).Equals(ConfigScalarNode.Float(0.76)), Is.False);

                Assert.That(ConfigScalarNode.String("Basic").Equals(ConfigScalarNode.String("Basic")), Is.True);
                Assert.That(ConfigScalarNode.String("Basic").Equals(ConfigScalarNode.String("basic")), Is.False);
            });
        }

        [Test]
        public void String_Scalar_Rejects_Null_Because_Null_Is_Explicit()
        {
            Assert.Throws<ArgumentNullException>(() => ConfigScalarNode.String(null));
        }

        [Test]
        public void Null_Is_An_Explicit_Semantic_Node()
        {
            Assert.That(ConfigNullNode.Instance.Equals(ConfigNullNode.Instance), Is.True);
            Assert.That(ConfigNullNode.Instance.Equals(ConfigScalarNode.String("null")), Is.False);
        }

        [Test]
        public void Object_Preserves_Entry_Order_But_Equality_Uses_Ordinal_Keys()
        {
            var first = new ConfigObjectNode(
                new ConfigObjectEntry("Enabled", ConfigScalarNode.Boolean(true)),
                new ConfigObjectEntry("Mode", ConfigScalarNode.String("Basic")));

            var reordered = new ConfigObjectNode(
                new ConfigObjectEntry("Mode", ConfigScalarNode.String("Basic")),
                new ConfigObjectEntry("Enabled", ConfigScalarNode.Boolean(true)));

            Assert.Multiple(() =>
            {
                Assert.That(first.Entries.Count, Is.EqualTo(2));
                Assert.That(first.Entries[0].Name, Is.EqualTo("Enabled"));
                Assert.That(first.Entries[1].Name, Is.EqualTo("Mode"));
                Assert.That(first, Is.EqualTo(reordered));
            });
        }

        [Test]
        public void Object_Equality_Is_Recursive_And_Key_Case_Is_Ordinal()
        {
            var first = new ConfigObjectNode(
                new ConfigObjectEntry(
                    "Nested",
                    new ConfigObjectNode(
                        new ConfigObjectEntry("Threshold", ConfigScalarNode.Float(0.75)))));

            var same = new ConfigObjectNode(
                new ConfigObjectEntry(
                    "Nested",
                    new ConfigObjectNode(
                        new ConfigObjectEntry("Threshold", ConfigScalarNode.Float(0.75)))));

            var differentCase = new ConfigObjectNode(
                new ConfigObjectEntry(
                    "Nested",
                    new ConfigObjectNode(
                        new ConfigObjectEntry("threshold", ConfigScalarNode.Float(0.75)))));

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(same));
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
                Assert.That(first, Is.Not.EqualTo(differentCase));
            });
        }

        [Test]
        public void Object_Rejects_Duplicate_Ordinal_Keys()
        {
            Assert.Throws<ArgumentException>(() =>
                new ConfigObjectNode(
                    new ConfigObjectEntry("Mode", ConfigScalarNode.String("Basic")),
                    new ConfigObjectEntry("Mode", ConfigScalarNode.String("Expert"))));
        }

        [Test]
        public void Object_Lookup_Uses_Ordinal_Key_Identity()
        {
            var expected = ConfigScalarNode.Integer(10);
            var obj = new ConfigObjectNode(new ConfigObjectEntry("MaxCount", expected));

            ConfigNode actual;
            ConfigNode wrongCase;

            Assert.Multiple(() =>
            {
                Assert.That(obj.TryGet("MaxCount", out actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(obj.TryGet("maxcount", out wrongCase), Is.False);
                Assert.That(wrongCase, Is.Null);
            });
        }

        [Test]
        public void Array_Copies_Input_And_Equality_Is_Order_Sensitive()
        {
            var values = new ConfigNode[]
            {
                ConfigScalarNode.String("alpha"),
                ConfigScalarNode.String("beta")
            };

            var first = new ConfigArrayNode(values);
            values[0] = ConfigScalarNode.String("changed");

            var same = new ConfigArrayNode(
                ConfigScalarNode.String("alpha"),
                ConfigScalarNode.String("beta"));

            var reversed = new ConfigArrayNode(
                ConfigScalarNode.String("beta"),
                ConfigScalarNode.String("alpha"));

            Assert.Multiple(() =>
            {
                Assert.That(first.Items.Count, Is.EqualTo(2));
                Assert.That(first.Items[0], Is.EqualTo(ConfigScalarNode.String("alpha")));
                Assert.That(first, Is.EqualTo(same));
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
                Assert.That(first, Is.Not.EqualTo(reversed));
            });
        }

        [Test]
        public void Nested_Object_Array_Structure_Uses_Recursive_Equality()
        {
            var first = new ConfigObjectNode(
                new ConfigObjectEntry(
                    "Tags",
                    new ConfigArrayNode(
                        ConfigScalarNode.String("alpha"),
                        ConfigScalarNode.String("beta"))),
                new ConfigObjectEntry(
                    "NamedValues",
                    new ConfigObjectNode(
                        new ConfigObjectEntry("start", ConfigScalarNode.Integer(1)),
                        new ConfigObjectEntry("end", ConfigScalarNode.Integer(10)))));

            var same = new ConfigObjectNode(
                new ConfigObjectEntry(
                    "NamedValues",
                    new ConfigObjectNode(
                        new ConfigObjectEntry("end", ConfigScalarNode.Integer(10)),
                        new ConfigObjectEntry("start", ConfigScalarNode.Integer(1)))),
                new ConfigObjectEntry(
                    "Tags",
                    new ConfigArrayNode(
                        ConfigScalarNode.String("alpha"),
                        ConfigScalarNode.String("beta"))));

            Assert.That(first, Is.EqualTo(same));
        }
    }
}
