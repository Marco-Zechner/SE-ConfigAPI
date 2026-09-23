using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.ConfigApi;
using Mz.SemanticVersioning;
using Mz.Storage;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Consumer
{
    [TestFixture]
    public sealed class SpaceEngineersConfigTextStorageTests
    {
        [Test]
        public void Indexed_Storage_Operations_Remain_Location_Scoped()
        {
            var localBackend = new MemoryBackend();
            var globalBackend = new MemoryBackend();
            var worldBackend = new MemoryBackend();
            var storage = new SpaceEngineersConfigTextStorage(
                new IndexedStorage(localBackend),
                new IndexedStorage(globalBackend, "Example.Mod.", ".Example.Mod.index"),
                new IndexedStorage(worldBackend));

            storage.Write(0, "local.toml", "local");
            storage.Write(1, "global.toml", "global");
            storage.Write(2, "world.toml", "world");

            Assert.Multiple(() =>
            {
                Assert.That(storage.Exists(0, "local.toml"), Is.True);
                Assert.That(storage.Exists(1, "global.toml"), Is.True);
                Assert.That(storage.Exists(2, "world.toml"), Is.True);
                Assert.That(storage.Read(0, "local.toml"), Is.EqualTo("local"));
                Assert.That(storage.Read(1, "global.toml"), Is.EqualTo("global"));
                Assert.That(storage.Read(2, "world.toml"), Is.EqualTo("world"));
                Assert.That(storage.ListKnown(0), Is.EqualTo(new[] { "local.toml" }));
                Assert.That(storage.ListKnown(1), Is.EqualTo(new[] { "global.toml" }));
                Assert.That(storage.ListKnown(2), Is.EqualTo(new[] { "world.toml" }));
                Assert.That(globalBackend.Contains("Example.Mod.global.toml"), Is.True);
                Assert.That(globalBackend.Contains(".Example.Mod.index"), Is.True);
            });
        }

        [Test]
        public void Missing_File_Returns_Null_Without_Loading()
        {
            var localBackend = new MemoryBackend();
            var storage = new SpaceEngineersConfigTextStorage(
                new IndexedStorage(localBackend),
                new IndexedStorage(new MemoryBackend(), "Example.Mod.", ".Example.Mod.index"),
                new IndexedStorage(new MemoryBackend()));

            string content = storage.Read(0, "missing.toml");

            Assert.Multiple(() =>
            {
                Assert.That(content, Is.Null);
                Assert.That(localBackend.ReadCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void Unsupported_Location_Is_Rejected()
        {
            var storage = new SpaceEngineersConfigTextStorage(
                new IndexedStorage(new MemoryBackend()),
                new IndexedStorage(new MemoryBackend(), "Example.Mod.", ".Example.Mod.index"),
                new IndexedStorage(new MemoryBackend()));

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => storage.Exists(3, "config.toml"));
                Assert.Throws<ArgumentException>(() => storage.Read(3, "config.toml"));
                Assert.Throws<ArgumentException>(() => storage.Write(-1, "config.toml", "content"));
                Assert.Throws<ArgumentException>(() => storage.ListKnown(4));
            });
        }

        [Test]
        public void Constructor_Rejects_Missing_Indexed_Storages()
        {
            var storage = new IndexedStorage(new MemoryBackend());

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new SpaceEngineersConfigTextStorage(null, storage, storage));
                Assert.Throws<ArgumentNullException>(() => new SpaceEngineersConfigTextStorage(storage, null, storage));
                Assert.Throws<ArgumentNullException>(() => new SpaceEngineersConfigTextStorage(storage, storage, null));
            });
        }

        [Test]
        public void ListKnown_Returns_Ordinal_Sorted_Logical_Names_Per_Location()
        {
            var storage = new SpaceEngineersConfigTextStorage(
                new IndexedStorage(new MemoryBackend()),
                new IndexedStorage(new MemoryBackend(), "Example.Mod.", ".Example.Mod.index"),
                new IndexedStorage(new MemoryBackend()));

            storage.Write(0, "zeta.toml", "z");
            storage.Write(0, "alpha.toml", "a");
            storage.Write(2, "world.toml", "w");

            Assert.Multiple(() =>
            {
                Assert.That(storage.ListKnown(0), Is.EqualTo(new[] { "alpha.toml", "zeta.toml" }));
                Assert.That(storage.ListKnown(1), Is.Empty);
                Assert.That(storage.ListKnown(2), Is.EqualTo(new[] { "world.toml" }));
            });
        }

        [Test]
        public void SpaceEngineers_Client_Factory_Registers_Indexed_Storage_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            Func<int, string, bool> observedExists = null;
            Func<int, string, string> observedRead = null;
            Action<int, string, string> observedWrite = null;
            Func<int, string[]> observedListKnown = null;

            var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                {
                    "RegisterConsumer",
                    new Func<string, Guid, Func<int, string, bool>, Func<int, string, string>, Action<int, string, string>, Func<int, string[]>, Action>(
                        delegate(string consumerId, Guid registrationId, Func<int, string, bool> exists, Func<int, string, string> read, Action<int, string, string> write, Func<int, string[]> listKnown)
                        {
                            observedExists = exists;
                            observedRead = read;
                            observedWrite = write;
                            observedListKnown = listKnown;
                            return delegate { };
                        })
                },
                {
                    "OpenConfig",
                    new Func<string, Guid, string, int, string, object, object>(
                        delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                        {
                            return defaults;
                        })
                },
                {
                    "SaveConfig",
                    new Func<string, Guid, string, int, string, object, object, object>(
                        delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults, object playerValues)
                        {
                            return playerValues;
                        })
                }
            };

            var provider = new ApiDiscoveryProvider(
                bus,
                new ApiModIdentity("MarcoZechner.ConfigAPI", "ConfigAPI", new SemanticVersion(0, 1, 0)),
                new ApiDescriptor("MarcoZechner.ConfigAPI", new SemanticVersion(2, 3, 0)),
                endpoints);

            provider.Start();

            var client = ConfigApiClient.CreateForSpaceEngineers(
                bus, "Example.Mod", "Example Mod", new SemanticVersion(1, 0, 0), true, "Uses ConfigAPI.");

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(observedExists, Is.Not.Null);
                Assert.That(observedRead, Is.Not.Null);
                Assert.That(observedWrite, Is.Not.Null);
                Assert.That(observedListKnown, Is.Not.Null);
                Assert.That(observedExists.Target, Is.TypeOf<SpaceEngineersConfigTextStorage>());
                Assert.That(observedRead.Target, Is.SameAs(observedExists.Target));
                Assert.That(observedWrite.Target, Is.SameAs(observedExists.Target));
                Assert.That(observedListKnown.Target, Is.SameAs(observedExists.Target));
            });

            client.Dispose();
            provider.Dispose();
        }

        private sealed class MemoryBackend : IStorageBackend
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);

            public int ReadCount { get; private set; }

            public bool Contains(string fileName) => _content.ContainsKey(fileName);

            public bool Exists(string fileName) => _content.ContainsKey(fileName);

            public string Read(string fileName)
            {
                ReadCount++;
                return _content[fileName];
            }

            public void Write(string fileName, string content) => _content[fileName] = content;
        }

        private sealed class RecordingModMessageBus : IModMessageBus
        {
            private readonly Dictionary<long, List<Action<object>>> _handlers = new Dictionary<long, List<Action<object>>>();

            public void RegisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;
                if (!_handlers.TryGetValue(channelId, out handlers))
                {
                    handlers = new List<Action<object>>();
                    _handlers.Add(channelId, handlers);
                }

                handlers.Add(handler);
            }

            public void UnregisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;
                if (_handlers.TryGetValue(channelId, out handlers))
                    handlers.Remove(handler);
            }

            public void Send(long channelId, object payload)
            {
                List<Action<object>> handlers;
                if (!_handlers.TryGetValue(channelId, out handlers))
                    return;

                Action<object>[] snapshot = handlers.ToArray();
                for (var index = 0; index < snapshot.Length; index++)
                    snapshot[index](payload);
            }
        }
    }
}
