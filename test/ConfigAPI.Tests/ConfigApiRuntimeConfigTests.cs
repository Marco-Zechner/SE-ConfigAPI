using System;
using MarcoZechner.ConfigAPI.V2;
using MarcoZechner.ConfigAPI.V2.Domain;
using Mz.Logging;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2
{
    [TestFixture]
    public sealed class ConfigApiRuntimeConfigTests
    {
        [Test]
        public void Defaults_To_Trace_And_RoundTrips_Log_Level()
        {
            ConfigApiRuntimeConfig defaults = ConfigApiRuntimeConfigDefinition.Deserialize(ConfigApiRuntimeConfigDefinition.CreateDefaults());
            Assert.That(defaults.MinimumLogLevel, Is.EqualTo(LogLevel.Trace));

            var configured = new ConfigApiRuntimeConfig { MinimumLogLevel = LogLevel.Debug };
            ConfigDocument document = ConfigApiRuntimeConfigDefinition.Serialize(configured);
            ConfigApiRuntimeConfig result = ConfigApiRuntimeConfigDefinition.Deserialize(document);

            ConfigNode serialized;
            Assert.That(document.TryGet(new ConfigValuePath("MinimumLogLevel"), out serialized), Is.True);
            var scalar = serialized as ConfigScalarNode;

            Assert.Multiple(() =>
            {
                Assert.That(scalar, Is.Not.Null);
                Assert.That(scalar.Kind, Is.EqualTo(ConfigScalarKind.String));
                Assert.That(scalar.Value, Is.EqualTo("Debug"));
                Assert.That(result.MinimumLogLevel, Is.EqualTo(LogLevel.Debug));
            });
        }

        [TestCase("Trace", LogLevel.Trace)]
        [TestCase("debug", LogLevel.Debug)]
        [TestCase("Info", LogLevel.Information)]
        [TestCase("warning", LogLevel.Warning)]
        [TestCase("Error", LogLevel.Error)]
        [TestCase("fatal", LogLevel.Critical)]
        public void Accepts_Supported_Log_Level_Names(string value, LogLevel expected)
        {
            var document = Document(ConfigScalarNode.String(value));
            ConfigApiRuntimeConfig result = ConfigApiRuntimeConfigDefinition.Deserialize(document);
            Assert.That(result.MinimumLogLevel, Is.EqualTo(expected));
        }

        [Test]
        public void Rejects_Missing_NonString_And_Unknown_Log_Level()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Deserialize(new ConfigDocument(new ConfigObjectNode())));
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Deserialize(Document(ConfigScalarNode.Integer(1))));
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Deserialize(Document(ConfigScalarNode.String("Everything"))));
            });
        }

        private static ConfigDocument Document(ConfigNode value)
            => new ConfigDocument(new ConfigObjectNode(new ConfigObjectEntry("MinimumLogLevel", value)));
    }
}