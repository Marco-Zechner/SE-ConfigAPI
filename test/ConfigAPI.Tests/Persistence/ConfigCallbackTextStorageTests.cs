using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigCallbackTextStorageTests
    {
        [Test]
        public void Indexed_Callbacks_Forward_Integer_Locations_And_Values()
        {
            var calls = new List<string>();
            var storage = new ConfigCallbackTextStorage(
                (location, file) => { calls.Add("exists|" + location + "|" + file); return location == 0; },
                (location, file) => { calls.Add("read|" + location + "|" + file); return "content:" + file; },
                (location, file, content) => calls.Add("write|" + location + "|" + file + "|" + content),
                location => { calls.Add("list|" + location); return new[] { "a.toml", "b.toml" }; });

            bool exists = storage.Exists(ConfigLocation.Local, "local.toml");
            string read = storage.Read(ConfigLocation.Global, "global.toml");
            storage.Write(ConfigLocation.World, "world.toml", "world");
            string[] known = storage.ListKnown(ConfigLocation.Local);

            Assert.Multiple(() =>
            {
                Assert.That(exists, Is.True);
                Assert.That(read, Is.EqualTo("content:global.toml"));
                Assert.That(known, Is.EqualTo(new[] { "a.toml", "b.toml" }));
                Assert.That(calls, Is.EqualTo(new[] {
                    "exists|0|local.toml",
                    "read|1|global.toml",
                    "write|2|world.toml|world",
                    "list|0"
                }));
            });
        }

        [Test]
        public void Missing_Read_Result_Remains_Missing()
        {
            var storage = new ConfigCallbackTextStorage((location, file) => false, (location, file) => null, (location, file, content) => { }, location => new string[0]);
            Assert.That(storage.Read(ConfigLocation.World, "missing.toml"), Is.Null);
        }

        [Test]
        public void Null_ListKnown_Result_Becomes_Empty()
        {
            var storage = new ConfigCallbackTextStorage((location, file) => false, (location, file) => null, (location, file, content) => { }, location => null);
            Assert.That(storage.ListKnown(ConfigLocation.Local), Is.Empty);
        }

        [Test]
        public void Constructor_Rejects_Missing_Callbacks()
        {
            Func<int, string, bool> exists = (location, file) => false;
            Func<int, string, string> read = (location, file) => null;
            Action<int, string, string> write = (location, file, content) => { };
            Func<int, string[]> listKnown = location => new string[0];

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new ConfigCallbackTextStorage(null, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigCallbackTextStorage(exists, null, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigCallbackTextStorage(exists, read, null, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigCallbackTextStorage(exists, read, write, null));
            });
        }
    }
}
