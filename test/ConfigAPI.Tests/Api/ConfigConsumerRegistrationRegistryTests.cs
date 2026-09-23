using System;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigConsumerRegistrationRegistryTests
    {
        [Test]
        public void Register_Provides_Callback_Backed_Indexed_Storage_For_Exact_Token()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var registrationId = Guid.NewGuid();
            var writtenContent = string.Empty;

            registry.Register(
                "Example.Mod", registrationId,
                (location, file) => location == 0 && file == "config.toml",
                (location, file) => location + "|" + file,
                (location, file, content) => writtenContent = location + "|" + file + "|" + content,
                location => new[] { "config.toml", "variant.toml" });

            IIndexedConfigTextStorage storage = registry.GetIndexedStorage("Example.Mod", registrationId);
            string loaded = storage.Read(ConfigLocation.Local, "config.toml");
            storage.Write(ConfigLocation.World, "world.toml", "content");

            Assert.Multiple(() =>
            {
                Assert.That(storage.Exists(ConfigLocation.Local, "config.toml"), Is.True);
                Assert.That(storage.Exists(ConfigLocation.World, "config.toml"), Is.False);
                Assert.That(loaded, Is.EqualTo("0|config.toml"));
                Assert.That(storage.ListKnown(ConfigLocation.Global), Is.EqualTo(new[] { "config.toml", "variant.toml" }));
                Assert.That(writtenContent, Is.EqualTo("2|world.toml|content"));
            });
        }

        [Test]
        public void Register_New_Token_Replaces_Previous_Consumer_Instance()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldRegistrationId = Guid.NewGuid();
            var newRegistrationId = Guid.NewGuid();

            RegisterValue(registry, "Example.Mod", oldRegistrationId, "old");
            RegisterValue(registry, "Example.Mod", newRegistrationId, "new");

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => registry.GetStorage("Example.Mod", oldRegistrationId));
                Assert.That(registry.GetStorage("Example.Mod", newRegistrationId).Read(ConfigLocation.Local, "config.toml"), Is.EqualTo("new"));
            });
        }

        [Test]
        public void Stale_Unregister_Does_Not_Remove_Reconnected_Consumer()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldRegistrationId = Guid.NewGuid();
            var newRegistrationId = Guid.NewGuid();

            RegisterValue(registry, "Example.Mod", oldRegistrationId, "old");
            RegisterValue(registry, "Example.Mod", newRegistrationId, "new");

            bool staleRemoved = registry.Unregister("Example.Mod", oldRegistrationId);
            IConfigTextStorage currentStorage = registry.GetStorage("Example.Mod", newRegistrationId);

            Assert.Multiple(() =>
            {
                Assert.That(staleRemoved, Is.False);
                Assert.That(currentStorage.Read(ConfigLocation.Local, "config.toml"), Is.EqualTo("new"));
            });
        }

        [Test]
        public void Matching_Unregister_Removes_Consumer()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var registrationId = Guid.NewGuid();

            RegisterValue(registry, "Example.Mod", registrationId, null);
            bool removed = registry.Unregister("Example.Mod", registrationId);

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.True);
                Assert.Throws<InvalidOperationException>(() => registry.GetStorage("Example.Mod", registrationId));
            });
        }

        [Test]
        public void Consumer_Ids_Are_Case_Sensitive()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var upperRegistrationId = Guid.NewGuid();
            var lowerRegistrationId = Guid.NewGuid();

            RegisterValue(registry, "Example.Mod", upperRegistrationId, "upper");
            RegisterValue(registry, "example.mod", lowerRegistrationId, "lower");

            Assert.Multiple(() =>
            {
                Assert.That(registry.GetStorage("Example.Mod", upperRegistrationId).Read(ConfigLocation.Local, "config.toml"), Is.EqualTo("upper"));
                Assert.That(registry.GetStorage("example.mod", lowerRegistrationId).Read(ConfigLocation.Local, "config.toml"), Is.EqualTo("lower"));
            });
        }

        [Test]
        public void Registered_Consumer_Ids_Are_Sorted_And_Reflect_Live_Registrations()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var betaRegistrationId = Guid.NewGuid();
            var alphaRegistrationId = Guid.NewGuid();

            RegisterValue(registry, "Beta.Mod", betaRegistrationId, null);
            RegisterValue(registry, "Alpha.Mod", alphaRegistrationId, null);

            Assert.Multiple(() =>
            {
                Assert.That(registry.Count, Is.EqualTo(2));
                Assert.That(registry.GetConsumerIds(), Is.EqualTo(new[] { "Alpha.Mod", "Beta.Mod" }));
            });

            registry.Unregister("Alpha.Mod", alphaRegistrationId);

            Assert.Multiple(() =>
            {
                Assert.That(registry.Count, Is.EqualTo(1));
                Assert.That(registry.GetConsumerIds(), Is.EqualTo(new[] { "Beta.Mod" }));
            });
        }

        [Test]
        public void Registration_Rejects_Invalid_Identity_Token_And_Callbacks()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            Func<int, string, bool> exists = (location, file) => false;
            Func<int, string, string> read = (location, file) => null;
            Action<int, string, string> write = (location, file, content) => { };
            Func<int, string[]> listKnown = location => new string[0];

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => registry.Register(null, Guid.NewGuid(), exists, read, write, listKnown));
                Assert.Throws<ArgumentException>(() => registry.Register(" ", Guid.NewGuid(), exists, read, write, listKnown));
                Assert.Throws<ArgumentException>(() => registry.Register("Example.Mod", Guid.Empty, exists, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => registry.Register("Example.Mod", Guid.NewGuid(), null, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => registry.Register("Example.Mod", Guid.NewGuid(), exists, null, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => registry.Register("Example.Mod", Guid.NewGuid(), exists, read, null, listKnown));
                Assert.Throws<ArgumentNullException>(() => registry.Register("Example.Mod", Guid.NewGuid(), exists, read, write, null));
            });
        }

        [Test]
        public void Internal_Storage_Reserves_Consumer_Id_And_Allows_Tokenless_Access()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new InternalStorage();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var registerInternal = typeof(ConfigConsumerRegistrationRegistry).GetMethod("RegisterInternalStorage", flags);
            var getCurrent = typeof(ConfigConsumerRegistrationRegistry).GetMethod("GetCurrentStorage", flags);
            var getCurrentIndexed = typeof(ConfigConsumerRegistrationRegistry).GetMethod("GetCurrentIndexedStorage", flags);
            var unregisterInternal = typeof(ConfigConsumerRegistrationRegistry).GetMethod("UnregisterInternalStorage", flags);

            Assert.Multiple(() =>
            {
                Assert.That(registerInternal, Is.Not.Null);
                Assert.That(getCurrent, Is.Not.Null);
                Assert.That(getCurrentIndexed, Is.Not.Null);
                Assert.That(unregisterInternal, Is.Not.Null);
            });

            registerInternal.Invoke(registry, new object[] { "ConfigAPI", storage });

            Assert.Multiple(() =>
            {
                Assert.That(getCurrent.Invoke(registry, new object[] { "ConfigAPI" }), Is.SameAs(storage));
                Assert.That(getCurrentIndexed.Invoke(registry, new object[] { "ConfigAPI" }), Is.SameAs(storage));
                Assert.Throws<InvalidOperationException>(() => registry.GetStorage("ConfigAPI", Guid.NewGuid()));
                Assert.Throws<InvalidOperationException>(() => registry.Register("ConfigAPI", Guid.NewGuid(), (location, file) => false, (location, file) => null, (location, file, content) => { }, location => new string[0]));
            });

            Assert.That((bool)unregisterInternal.Invoke(registry, new object[] { "ConfigAPI" }), Is.True);
        }
        private sealed class InternalStorage : IIndexedConfigTextStorage
        {
            public bool Exists(ConfigLocation location, string file) => false;
            public string Read(ConfigLocation location, string file) => null;
            public void Write(ConfigLocation location, string file, string content) { }
            public string[] ListKnown(ConfigLocation location) => new string[0];
        }
        private static void RegisterValue(ConfigConsumerRegistrationRegistry registry, string consumerId, Guid registrationId, string value)
        {
            registry.Register(consumerId, registrationId, (location, file) => value != null, (location, file) => value, (location, file, content) => { }, location => new string[0]);
        }
    }
}
