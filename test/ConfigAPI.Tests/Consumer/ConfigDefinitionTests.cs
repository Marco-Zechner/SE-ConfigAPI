using System;
using Mz.ConfigApi;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Consumer
{
    [TestFixture]
    public sealed class ConfigDefinitionTests
    {
        [Test]
        public void Definition_Derives_Default_And_Variant_Files_From_Config_Key()
        {
            var createCount = 0;
            var definition = new ConfigDefinition<ExampleConfig>(" Settings ", delegate { createCount++; return new ExampleConfig { Value = createCount }; },
                value => new ConfigDocument(new ConfigEntry("Value", ConfigValue.Integer(value.Value))),
                document => { ConfigValue value; if (!document.TryGet("Value", out value)) throw new InvalidOperationException(); return new ExampleConfig { Value = (int)(long)value.ScalarValue }; });

            ExampleConfig defaults = definition.CreateDefaults();
            ConfigDocument serializedDocument = definition.Serialize(defaults);
            ExampleConfig restored = definition.Deserialize(serializedDocument);

            Assert.Multiple(() =>
            {
                Assert.That(definition.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(definition.GetVariantFile(ConfigDefinition<ExampleConfig>.DefaultVariant), Is.EqualTo("Settings.default.toml"));
                Assert.That(definition.GetVariantFile(" combat "), Is.EqualTo("Settings.combat.toml"));
                Assert.That(definition.GetVariantFile("cargo_2"), Is.EqualTo("Settings.cargo_2.toml"));
                Assert.That(defaults.Value, Is.EqualTo(1));
                Assert.That(restored.Value, Is.EqualTo(1));
                Assert.That(createCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void Definition_Rejects_Invalid_Config_Keys_And_Variants()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => CreateDefinition(" "));
                Assert.Throws<ArgumentException>(() => CreateDefinition("Settings.Debug"));

                var definition = CreateDefinition("World-Biome_2");
                Assert.That(definition.GetVariantFile(ConfigDefinition<ExampleConfig>.DefaultVariant), Is.EqualTo("World-Biome_2.default.toml"));
                Assert.Throws<ArgumentException>(() => definition.GetVariantFile(" "));
                Assert.Throws<ArgumentException>(() => definition.GetVariantFile("combat.v2"));
            });
        }

        [Test]
        public void Definition_Rejects_Invalid_Constructor_Delegates()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new ConfigDefinition<ExampleConfig>("Settings", null, value => new ConfigDocument(), document => new ExampleConfig()));
                Assert.Throws<ArgumentNullException>(() => new ConfigDefinition<ExampleConfig>("Settings", () => new ExampleConfig(), null, document => new ExampleConfig()));
                Assert.Throws<ArgumentNullException>(() => new ConfigDefinition<ExampleConfig>("Settings", () => new ExampleConfig(), value => new ConfigDocument(), null));
            });
        }

        [Test]
        public void Definition_Rejects_Null_Delegate_Results()
        {
            var nullDefaults = new ConfigDefinition<ExampleConfig>("Settings", () => null, value => new ConfigDocument(), document => new ExampleConfig());
            var nullSerializer = new ConfigDefinition<ExampleConfig>("Settings", () => new ExampleConfig(), value => null, document => new ExampleConfig());
            var nullDeserializer = new ConfigDefinition<ExampleConfig>("Settings", () => new ExampleConfig(), value => new ConfigDocument(), document => null);

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => nullDefaults.CreateDefaults());
                Assert.Throws<InvalidOperationException>(() => nullSerializer.Serialize(new ExampleConfig()));
                Assert.Throws<InvalidOperationException>(() => nullDeserializer.Deserialize(new ConfigDocument()));
            });
        }

        [Test]
        public void Definition_Rejects_Null_Serialization_Values()
        {
            ConfigDefinition<ExampleConfig> definition = CreateDefinition("Settings");

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => definition.Serialize(null));
                Assert.Throws<ArgumentNullException>(() => definition.Deserialize(null));
            });
        }

        private static ConfigDefinition<ExampleConfig> CreateDefinition(string configKey) =>
            new ConfigDefinition<ExampleConfig>(configKey, () => new ExampleConfig(), value => new ConfigDocument(), document => new ExampleConfig());

        private sealed class ExampleConfig
        {
            public int Value { get; set; }
        }
    }
}
