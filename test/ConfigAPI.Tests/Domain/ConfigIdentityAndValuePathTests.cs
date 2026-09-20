using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigIdentityTests
    {
        [TestCase(null, "settings")]
        [TestCase("", "settings")]
        [TestCase(" ", "settings")]
        [TestCase("example.mod", null)]
        [TestCase("example.mod", "")]
        [TestCase("example.mod", " ")]
        public void Constructor_Rejects_Missing_Identity_Parts(string ownerId, string configKey)
        {
            Assert.Throws<ArgumentException>(() => new ConfigIdentity(ownerId, configKey));
        }

        [Test]
        public void Equality_Uses_Owner_And_Config_Key()
        {
            var first = new ConfigIdentity("example.mod", "flight");
            var same = new ConfigIdentity("example.mod", "flight");
            var differentOwner = new ConfigIdentity("another.mod", "flight");
            var differentConfig = new ConfigIdentity("example.mod", "weapons");

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(same));
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
                Assert.That(first, Is.Not.EqualTo(differentOwner));
                Assert.That(first, Is.Not.EqualTo(differentConfig));
            });
        }

        [Test]
        public void Equality_Is_Ordinal()
        {
            var lower = new ConfigIdentity("example.mod", "flight");
            var upperOwner = new ConfigIdentity("Example.Mod", "flight");
            var upperConfig = new ConfigIdentity("example.mod", "Flight");

            Assert.Multiple(() =>
            {
                Assert.That(lower, Is.Not.EqualTo(upperOwner));
                Assert.That(lower, Is.Not.EqualTo(upperConfig));
            });
        }
    }

    [TestFixture]
    public sealed class ConfigValuePathTests
    {
        [Test]
        public void Constructor_Copies_Input_Segments()
        {
            var segments = new[] { "flight", "maxSpeed" };

            var path = new ConfigValuePath(segments);
            segments[1] = "changed";

            Assert.Multiple(() =>
            {
                Assert.That(path.Segments.Count, Is.EqualTo(2));
                Assert.That(path.Segments[0], Is.EqualTo("flight"));
                Assert.That(path.Segments[1], Is.EqualTo("maxSpeed"));
            });
        }

        [Test]
        public void Append_Creates_New_Path_Without_Mutating_Original()
        {
            var parent = new ConfigValuePath("flight");
            var child = parent.Append("maxSpeed");

            Assert.Multiple(() =>
            {
                Assert.That(parent.Segments.Count, Is.EqualTo(1));
                Assert.That(parent.Segments[0], Is.EqualTo("flight"));
                Assert.That(child.Segments.Count, Is.EqualTo(2));
                Assert.That(child.Segments[0], Is.EqualTo("flight"));
                Assert.That(child.Segments[1], Is.EqualTo("maxSpeed"));
            });
        }

        [Test]
        public void Equality_Uses_All_Segments_Ordinally()
        {
            var first = new ConfigValuePath("flight", "maxSpeed");
            var same = new ConfigValuePath("flight", "maxSpeed");
            var differentCase = new ConfigValuePath("flight", "MaxSpeed");
            var differentParent = new ConfigValuePath("thrusters", "maxSpeed");

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(same));
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
                Assert.That(first, Is.Not.EqualTo(differentCase));
                Assert.That(first, Is.Not.EqualTo(differentParent));
            });
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_Rejects_Invalid_Segments(string segment)
        {
            Assert.Throws<ArgumentException>(() => new ConfigValuePath(segment));
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Append_Rejects_Invalid_Segment(string segment)
        {
            var path = new ConfigValuePath("flight");

            Assert.Throws<ArgumentException>(() => path.Append(segment));
        }
    }
}
