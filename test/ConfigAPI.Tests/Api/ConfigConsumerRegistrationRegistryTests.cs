using System;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigConsumerRegistrationRegistryTests
    {
        [Test]
        public void Register_Provides_Callback_Backed_Storage_For_Exact_Token()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var registrationId = Guid.NewGuid();
            var writtenContent = string.Empty;

            registry.Register(
                "Example.Mod",
                registrationId,
                (location, file) => location + "|" + file,
                (location, file, content) =>
                    writtenContent = location + "|" + file + "|" + content);

            IConfigTextStorage storage = registry.GetStorage("Example.Mod", registrationId);

            var loaded = storage.Read(ConfigLocation.Local, "config.toml");
            storage.Write(ConfigLocation.World, "world.toml", "content");

            Assert.Multiple(() =>
            {
                Assert.That(loaded, Is.EqualTo("0|config.toml"));
                Assert.That(writtenContent, Is.EqualTo("2|world.toml|content"));
            });
        }

        [Test]
        public void Register_New_Token_Replaces_Previous_Consumer_Instance()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldRegistrationId = Guid.NewGuid();
            var newRegistrationId = Guid.NewGuid();

            registry.Register(
                "Example.Mod",
                oldRegistrationId,
                (location, file) => "old",
                (location, file, content) => { });

            registry.Register(
                "Example.Mod",
                newRegistrationId,
                (location, file) => "new",
                (location, file, content) => { });

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(
                    () => registry.GetStorage("Example.Mod", oldRegistrationId));

                Assert.That(
                    registry.GetStorage("Example.Mod", newRegistrationId)
                        .Read(ConfigLocation.Local, "config.toml"),
                    Is.EqualTo("new"));
            });
        }

        [Test]
        public void Stale_Unregister_Does_Not_Remove_Reconnected_Consumer()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldRegistrationId = Guid.NewGuid();
            var newRegistrationId = Guid.NewGuid();

            registry.Register(
                "Example.Mod",
                oldRegistrationId,
                (location, file) => "old",
                (location, file, content) => { });

            registry.Register(
                "Example.Mod",
                newRegistrationId,
                (location, file) => "new",
                (location, file, content) => { });

            var staleRemoved = registry.Unregister("Example.Mod", oldRegistrationId);
            var currentStorage = registry.GetStorage("Example.Mod", newRegistrationId);

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

            registry.Register(
                "Example.Mod",
                registrationId,
                (location, file) => null,
                (location, file, content) => { });

            var removed = registry.Unregister("Example.Mod", registrationId);

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.True);
                Assert.Throws<InvalidOperationException>(
                    () => registry.GetStorage("Example.Mod", registrationId));
            });
        }

        [Test]
        public void Consumer_Ids_Are_Case_Sensitive()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var upperRegistrationId = Guid.NewGuid();
            var lowerRegistrationId = Guid.NewGuid();

            registry.Register(
                "Example.Mod",
                upperRegistrationId,
                (location, file) => "upper",
                (location, file, content) => { });

            registry.Register(
                "example.mod",
                lowerRegistrationId,
                (location, file) => "lower",
                (location, file, content) => { });

            Assert.Multiple(() =>
            {
                Assert.That(
                    registry.GetStorage("Example.Mod", upperRegistrationId)
                        .Read(ConfigLocation.Local, "config.toml"),
                    Is.EqualTo("upper"));

                Assert.That(
                    registry.GetStorage("example.mod", lowerRegistrationId)
                        .Read(ConfigLocation.Local, "config.toml"),
                    Is.EqualTo("lower"));
            });
        }

        [Test]
        public void Registration_Rejects_Invalid_Identity_Token_And_Callbacks()
        {
            var registry = new ConfigConsumerRegistrationRegistry();

            Func<int, string, string> read = (location, file) => null;
            Action<int, string, string> write = (location, file, content) => { };

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(
                    () => registry.Register(null, Guid.NewGuid(), read, write));

                Assert.Throws<ArgumentException>(
                    () => registry.Register(" ", Guid.NewGuid(), read, write));

                Assert.Throws<ArgumentException>(
                    () => registry.Register("Example.Mod", Guid.Empty, read, write));

                Assert.Throws<ArgumentNullException>(
                    () => registry.Register("Example.Mod", Guid.NewGuid(), null, write));

                Assert.Throws<ArgumentNullException>(
                    () => registry.Register("Example.Mod", Guid.NewGuid(), read, null));
            });
        }
    }
}
