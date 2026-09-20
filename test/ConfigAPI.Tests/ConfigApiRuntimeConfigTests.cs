using System;
using MarcoZechner.ConfigAPI.V2;
using Mz.ConfigApi;
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
            ConfigApiRuntimeConfig defaults = ConfigApiRuntimeConfigDefinition.Definition.CreateDefaults();
            Assert.That(defaults.MinimumLogLevel, Is.EqualTo(LogLevel.Trace));

            var configured = new ConfigApiRuntimeConfig { MinimumLogLevel = LogLevel.Debug };
            ConfigDocument document = ConfigApiRuntimeConfigDefinition.Definition.Serialize(configured);
            ConfigApiRuntimeConfig result = ConfigApiRuntimeConfigDefinition.Definition.Deserialize(document);

            ConfigValue serialized;
            Assert.That(document.TryGet("MinimumLogLevel", out serialized), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(serialized.Kind, Is.EqualTo(ConfigValueKind.String));
                Assert.That(serialized.ScalarValue, Is.EqualTo("Debug"));
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
            var document = new ConfigDocument(new ConfigEntry("MinimumLogLevel", ConfigValue.String(value)));
            ConfigApiRuntimeConfig result = ConfigApiRuntimeConfigDefinition.Definition.Deserialize(document);

            Assert.That(result.MinimumLogLevel, Is.EqualTo(expected));
        }

        [Test]
        public void Rejects_Missing_NonString_And_Unknown_Log_Level()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Definition.Deserialize(new ConfigDocument()));
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Definition.Deserialize(new ConfigDocument(new ConfigEntry("MinimumLogLevel", ConfigValue.Integer(1)))));
                Assert.Throws<FormatException>(() => ConfigApiRuntimeConfigDefinition.Definition.Deserialize(new ConfigDocument(new ConfigEntry("MinimumLogLevel", ConfigValue.String("Everything")))));
            });
        }
    }
}