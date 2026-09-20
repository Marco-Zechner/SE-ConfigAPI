using System;
using Mz.ConfigApi;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Consumer
{
    [TestFixture]
    public sealed class ConfigDefinitionTests
    {
        [Test]
        public void Definition_Stores_Identity_And_Delegates_Serialization()
        {
            var createCount = 0;

            var definition =
                new ConfigDefinition<ExampleConfig>(
                    " Settings ",
                    "settings.toml",
                    delegate
                    {
                        createCount++;

                        return new ExampleConfig
                        {
                            Value = createCount
                        };
                    },
                    value =>
                        new ConfigDocument(
                            new ConfigEntry(
                                "Value",
                                ConfigValue.Integer(value.Value))),
                    document =>
                    {
                        ConfigValue value;

                        if (!document.TryGet("Value", out value))
                            throw new InvalidOperationException();

                        return new ExampleConfig
                        {
                            Value = (int)(long)value.ScalarValue
                        };
                    });

            ExampleConfig defaults =
                definition.CreateDefaults();

            ConfigDocument serializedDocument =
                definition.Serialize(defaults);

            ExampleConfig restored =
                definition.Deserialize(serializedDocument);

            Assert.Multiple(() =>
            {
                Assert.That(definition.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(definition.DefaultFile, Is.EqualTo("settings.toml"));
                Assert.That(defaults.Value, Is.EqualTo(1));
                Assert.That(restored.Value, Is.EqualTo(1));
                Assert.That(createCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void Definition_Rejects_Invalid_Constructor_Arguments()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(
                    () => new ConfigDefinition<ExampleConfig>(
                        " ",
                        "settings.toml",
                        () => new ExampleConfig(),
                        value => new ConfigDocument(),
                        document => new ExampleConfig()));

                Assert.Throws<ArgumentException>(
                    () => new ConfigDefinition<ExampleConfig>(
                        "Settings",
                        " ",
                        () => new ExampleConfig(),
                        value => new ConfigDocument(),
                        document => new ExampleConfig()));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigDefinition<ExampleConfig>(
                        "Settings",
                        "settings.toml",
                        null,
                        value => new ConfigDocument(),
                        document => new ExampleConfig()));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigDefinition<ExampleConfig>(
                        "Settings",
                        "settings.toml",
                        () => new ExampleConfig(),
                        null,
                        document => new ExampleConfig()));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigDefinition<ExampleConfig>(
                        "Settings",
                        "settings.toml",
                        () => new ExampleConfig(),
                        value => new ConfigDocument(),
                        null));
            });
        }

        [Test]
        public void Definition_Rejects_Null_Delegates_Results()
        {
            var nullDefaults =
                new ConfigDefinition<ExampleConfig>(
                    "Settings",
                    "settings.toml",
                    () => null,
                    value => new ConfigDocument(),
                    document => new ExampleConfig());

            var nullSerializer =
                new ConfigDefinition<ExampleConfig>(
                    "Settings",
                    "settings.toml",
                    () => new ExampleConfig(),
                    value => null,
                    document => new ExampleConfig());

            var nullDeserializer =
                new ConfigDefinition<ExampleConfig>(
                    "Settings",
                    "settings.toml",
                    () => new ExampleConfig(),
                    value => new ConfigDocument(),
                    document => null);

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(
                    () => nullDefaults.CreateDefaults());

                Assert.Throws<InvalidOperationException>(
                    () => nullSerializer.Serialize(new ExampleConfig()));

                Assert.Throws<InvalidOperationException>(
                    () => nullDeserializer.Deserialize(new ConfigDocument()));
            });
        }

        [Test]
        public void Definition_Rejects_Null_Serialization_Values()
        {
            var definition =
                new ConfigDefinition<ExampleConfig>(
                    "Settings",
                    "settings.toml",
                    () => new ExampleConfig(),
                    value => new ConfigDocument(),
                    document => new ExampleConfig());

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(
                    () => definition.Serialize(null));

                Assert.Throws<ArgumentNullException>(
                    () => definition.Deserialize(null));
            });
        }

        private sealed class ExampleConfig
        {
            public int Value { get; set; }
        }
    }
}
