using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigTextWriteCoordinatorTests
    {
        [Test]
        public void Backup_Name_Contains_Original_Name_And_Utc_Timestamp()
        {
            var timestamp = new DateTime(
                2026,
                8,
                31,
                20,
                53,
                7,
                DateTimeKind.Utc);

            var backup = ConfigBackupName.Create(
                "server.toml",
                timestamp);

            Assert.Multiple(() =>
            {
                Assert.That(
                    backup,
                    Is.EqualTo("server.toml.20260831T205307.0000000Z.bak"));

                Assert.That(
                    ConfigBackupName.Create(
                        "server.toml",
                        timestamp,
                        2),
                    Is.EqualTo("server.toml.20260831T205307.0000000Z.2.bak"));
            });
        }

        [Test]
        public void Lossy_Write_Backs_Up_Exact_Original_Before_Overwriting()
        {
            var storage = new RecordingStorage
            {
                ReadContent = "original config text"
            };

            var clock = new FixedClock(
                new DateTime(
                    2026,
                    8,
                    31,
                    20,
                    53,
                    7,
                    DateTimeKind.Utc));

            var writer = new ConfigTextWriteCoordinator(
                storage,
                clock);

            var result = writer.Write(
                ConfigLocation.World,
                "server.toml",
                "regenerated config text",
                true);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.BackupFile,
                    Is.EqualTo("server.toml.20260831T205307.0000000Z.bak"));

                Assert.That(storage.Operations.Count, Is.EqualTo(4));
                Assert.That(
                    storage.Operations[0],
                    Is.EqualTo("READ|World|server.toml"));

                Assert.That(
                    storage.Operations[1],
                    Is.EqualTo(
                        "READ|World|server.toml.20260831T205307.0000000Z.bak"));

                Assert.That(
                    storage.Operations[2],
                    Is.EqualTo(
                        "WRITE|World|server.toml.20260831T205307.0000000Z.bak|original config text"));

                Assert.That(
                    storage.Operations[3],
                    Is.EqualTo(
                        "WRITE|World|server.toml|regenerated config text"));
            });
        }

        [Test]
        public void Lossy_Write_Uses_Numbered_Backup_When_Timestamp_Name_Already_Exists()
        {
            var storage = new RecordingStorage
            {
                ReadContent = "original config text"
            };

            storage.ReadContentByFile[
                "server.toml.20260831T205307.0000000Z.bak"] =
                "existing backup";

            var writer = new ConfigTextWriteCoordinator(
                storage,
                new FixedClock(
                    new DateTime(
                        2026,
                        8,
                        31,
                        20,
                        53,
                        7,
                        DateTimeKind.Utc)));

            var result = writer.Write(
                ConfigLocation.World,
                "server.toml",
                "regenerated config text",
                true);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.BackupFile,
                    Is.EqualTo(
                        "server.toml.20260831T205307.0000000Z.1.bak"));

                Assert.That(
                    storage.Operations,
                    Is.EqualTo(
                        new[]
                        {
                            "READ|World|server.toml",
                            "READ|World|server.toml.20260831T205307.0000000Z.bak",
                            "READ|World|server.toml.20260831T205307.0000000Z.1.bak",
                            "WRITE|World|server.toml.20260831T205307.0000000Z.1.bak|original config text",
                            "WRITE|World|server.toml|regenerated config text"
                        }));
            });
        }

        [Test]
        public void Lossless_Write_Does_Not_Read_Or_Create_Backup()
        {
            var storage = new RecordingStorage
            {
                ReadContent = "original config text"
            };

            var writer = new ConfigTextWriteCoordinator(
                storage,
                new FixedClock(
                    new DateTime(
                        2026,
                        8,
                        31,
                        20,
                        53,
                        7,
                        DateTimeKind.Utc)));

            var result = writer.Write(
                ConfigLocation.Local,
                "client.toml",
                "new config text",
                false);

            Assert.Multiple(() =>
            {
                Assert.That(result.BackupFile, Is.Null);
                Assert.That(storage.Operations.Count, Is.EqualTo(1));
                Assert.That(
                    storage.Operations[0],
                    Is.EqualTo("WRITE|Local|client.toml|new config text"));
            });
        }

        [Test]
        public void Lossy_Write_With_Missing_Source_Writes_New_File_Without_Empty_Backup()
        {
            var storage = new RecordingStorage
            {
                ReadContent = null
            };

            var writer = new ConfigTextWriteCoordinator(
                storage,
                new FixedClock(
                    new DateTime(
                        2026,
                        8,
                        31,
                        20,
                        53,
                        7,
                        DateTimeKind.Utc)));

            var result = writer.Write(
                ConfigLocation.Global,
                "global.toml",
                "regenerated config text",
                true);

            Assert.Multiple(() =>
            {
                Assert.That(result.BackupFile, Is.Null);
                Assert.That(storage.Operations.Count, Is.EqualTo(2));
                Assert.That(
                    storage.Operations[0],
                    Is.EqualTo("READ|Global|global.toml"));

                Assert.That(
                    storage.Operations[1],
                    Is.EqualTo("WRITE|Global|global.toml|regenerated config text"));
            });
        }

        [Test]
        public void Backup_Uses_The_Same_Config_Location_As_The_Source()
        {
            var storage = new RecordingStorage
            {
                ReadContent = "before"
            };

            var writer = new ConfigTextWriteCoordinator(
                storage,
                new FixedClock(
                    new DateTime(
                        2026,
                        8,
                        31,
                        20,
                        53,
                        7,
                        DateTimeKind.Utc)));

            writer.Write(
                ConfigLocation.Local,
                "settings.toml",
                "after",
                true);

            Assert.Multiple(() =>
            {
                Assert.That(
                    storage.Operations[0],
                    Is.EqualTo("READ|Local|settings.toml"));

                Assert.That(
                    storage.Operations[1],
                    Is.EqualTo(
                        "READ|Local|settings.toml.20260831T205307.0000000Z.bak"));

                Assert.That(
                    storage.Operations[2],
                    Does.StartWith("WRITE|Local|settings.toml."));
            });
        }

        [Test]
        public void Writer_Rejects_Null_Dependencies_And_Invalid_Write_Arguments()
        {
            var storage = new RecordingStorage();
            var clock = new FixedClock(DateTime.UtcNow);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigTextWriteCoordinator(null, clock));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigTextWriteCoordinator(storage, null));

                var writer = new ConfigTextWriteCoordinator(storage, clock);

                Assert.Throws<ArgumentException>(() =>
                    writer.Write(
                        ConfigLocation.Local,
                        null,
                        "content",
                        false));

                Assert.Throws<ArgumentException>(() =>
                    writer.Write(
                        ConfigLocation.Local,
                        "   ",
                        "content",
                        false));

                Assert.Throws<ArgumentNullException>(() =>
                    writer.Write(
                        ConfigLocation.Local,
                        "settings.toml",
                        null,
                        false));
            });
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow { get; }

            public FixedClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }
        }

        private sealed class RecordingStorage : IConfigTextStorage
        {
            public readonly List<string> Operations = new List<string>();

            public readonly Dictionary<string, string> ReadContentByFile =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            public string ReadContent;

            public string Read(
                ConfigLocation location,
                string file)
            {
                Operations.Add(
                    "READ|" + location + "|" + file);

                string content;
                if (ReadContentByFile.TryGetValue(
                    file,
                    out content))
                {
                    return content;
                }

                if (file.EndsWith(
                    ".bak",
                    StringComparison.Ordinal))
                {
                    return null;
                }

                return ReadContent;
            }

            public void Write(
                ConfigLocation location,
                string file,
                string content)
            {
                Operations.Add(
                    "WRITE|" + location + "|" + file + "|" + content);
            }
        }
    }
}
