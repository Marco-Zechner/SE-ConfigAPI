using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigCallbackTextStorageTests
    {
        [Test]
        public void Read_Forwards_Integer_Location_File_And_Result()
        {
            var calls =
                new List<string>();

            var storage =
                new ConfigCallbackTextStorage(
                    (location, file) =>
                    {
                        calls.Add(
                            location +
                            "|" +
                            file);

                        return "content:" + file;
                    },
                    (location, file, content) =>
                    {
                    });

            var local =
                storage.Read(
                    ConfigLocation.Local,
                    "local.toml");

            var global =
                storage.Read(
                    ConfigLocation.Global,
                    "global.toml");

            var world =
                storage.Read(
                    ConfigLocation.World,
                    "world.toml");

            Assert.Multiple(() =>
            {
                Assert.That(
                    calls,
                    Is.EqualTo(
                        new[]
                        {
                            "0|local.toml",
                            "1|global.toml",
                            "2|world.toml"
                        }));

                Assert.That(
                    local,
                    Is.EqualTo("content:local.toml"));

                Assert.That(
                    global,
                    Is.EqualTo("content:global.toml"));

                Assert.That(
                    world,
                    Is.EqualTo("content:world.toml"));
            });
        }

        [Test]
        public void Write_Forwards_Integer_Location_File_And_Content()
        {
            var calls =
                new List<string>();

            var storage =
                new ConfigCallbackTextStorage(
                    (location, file) => null,
                    (location, file, content) =>
                    {
                        calls.Add(
                            location +
                            "|" +
                            file +
                            "|" +
                            content);
                    });

            storage.Write(
                ConfigLocation.Local,
                "local.toml",
                "local");

            storage.Write(
                ConfigLocation.Global,
                "global.toml",
                "global");

            storage.Write(
                ConfigLocation.World,
                "world.toml",
                "world");

            Assert.That(
                calls,
                Is.EqualTo(
                    new[]
                    {
                        "0|local.toml|local",
                        "1|global.toml|global",
                        "2|world.toml|world"
                    }));
        }

        [Test]
        public void Missing_Read_Result_Remains_Missing()
        {
            var storage =
                new ConfigCallbackTextStorage(
                    (location, file) => null,
                    (location, file, content) =>
                    {
                    });

            Assert.That(
                storage.Read(
                    ConfigLocation.World,
                    "missing.toml"),
                Is.Null);
        }

        [Test]
        public void Constructor_Rejects_Missing_Callbacks()
        {
            Func<int, string, string> read =
                (location, file) => null;

            Action<int, string, string> write =
                (location, file, content) =>
                {
                };

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigCallbackTextStorage(
                        null,
                        write));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigCallbackTextStorage(
                        read,
                        null));
            });
        }
    }
}
