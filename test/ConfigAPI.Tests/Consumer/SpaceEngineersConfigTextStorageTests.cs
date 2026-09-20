using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.ConfigApi;
using Mz.SemanticVersioning;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Consumer
{
    [TestFixture]
    public sealed class SpaceEngineersConfigTextStorageTests
    {
        [Test]
        public void Local_Read_And_Write_Use_Consumer_Owned_Scope_Type()
        {
            var utilities = new RecordingStorageUtilities();
            var scope = typeof(SpaceEngineersConfigTextStorageTests);
            var storage = new SpaceEngineersConfigTextStorage(utilities, scope);

            utilities.LocalExists = true;
            utilities.ReadContent = "local-content";

            string content = storage.Read(0, "config.toml");
            storage.Write(0, "config.toml", "updated");

            Assert.Multiple(() =>
            {
                Assert.That(content, Is.EqualTo("local-content"));
                Assert.That(utilities.LastOperation, Is.EqualTo("WriteLocal"));
                Assert.That(utilities.LastFile, Is.EqualTo("config.toml"));
                Assert.That(utilities.LastContent, Is.EqualTo("updated"));
                Assert.That(utilities.LastScope, Is.SameAs(scope));
            });
        }

        [Test]
        public void Missing_Local_File_Returns_Null_Without_Reading()
        {
            var utilities = new RecordingStorageUtilities();
            var storage =
                new SpaceEngineersConfigTextStorage(
                    utilities,
                    typeof(SpaceEngineersConfigTextStorageTests));

            string content = storage.Read(0, "missing.toml");

            Assert.Multiple(() =>
            {
                Assert.That(content, Is.Null);
                Assert.That(utilities.LocalReadCount, Is.EqualTo(0));
                Assert.That(utilities.LastOperation, Is.EqualTo("ExistsLocal"));
            });
        }

        [Test]
        public void Global_Read_And_Write_Use_Unscoped_Storage()
        {
            var utilities = new RecordingStorageUtilities();
            var storage =
                new SpaceEngineersConfigTextStorage(
                    utilities,
                    typeof(SpaceEngineersConfigTextStorageTests));

            utilities.GlobalExists = true;
            utilities.ReadContent = "global-content";

            string content = storage.Read(1, "config.toml");
            storage.Write(1, "config.toml", "updated");

            Assert.Multiple(() =>
            {
                Assert.That(content, Is.EqualTo("global-content"));
                Assert.That(utilities.GlobalReadCount, Is.EqualTo(1));
                Assert.That(utilities.GlobalWriteCount, Is.EqualTo(1));
                Assert.That(utilities.LastScope, Is.Null);
                Assert.That(utilities.LastContent, Is.EqualTo("updated"));
            });
        }

        [Test]
        public void World_Read_And_Write_Use_Consumer_Owned_Scope_Type()
        {
            var utilities = new RecordingStorageUtilities();
            var scope = typeof(SpaceEngineersConfigTextStorageTests);
            var storage = new SpaceEngineersConfigTextStorage(utilities, scope);

            utilities.WorldExists = true;
            utilities.ReadContent = "world-content";

            string content = storage.Read(2, "world.toml");
            storage.Write(2, "world.toml", "updated");

            Assert.Multiple(() =>
            {
                Assert.That(content, Is.EqualTo("world-content"));
                Assert.That(utilities.WorldReadCount, Is.EqualTo(1));
                Assert.That(utilities.WorldWriteCount, Is.EqualTo(1));
                Assert.That(utilities.LastScope, Is.SameAs(scope));
                Assert.That(utilities.LastContent, Is.EqualTo("updated"));
            });
        }

        [Test]
        public void Unsupported_Location_Is_Rejected()
        {
            var utilities = new RecordingStorageUtilities();
            var storage =
                new SpaceEngineersConfigTextStorage(
                    utilities,
                    typeof(SpaceEngineersConfigTextStorageTests));

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(
                    () => storage.Read(3, "config.toml"));

                Assert.Throws<ArgumentException>(
                    () => storage.Write(-1, "config.toml", "content"));
            });
        }

        [Test]
        public void SpaceEngineers_Client_Factory_Registers_Storage_Adapter_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            Func<int, string, string> observedRead = null;
            Action<int, string, string> observedWrite = null;

            var endpoints =
                new Dictionary<string, Delegate>(StringComparer.Ordinal)
                {
                    {
                        "RegisterConsumer",
                        new Func<
                            string,
                            Guid,
                            Func<int, string, string>,
                            Action<int, string, string>,
                            Action>(
                            delegate(
                                string consumerId,
                                Guid registrationId,
                                Func<int, string, string> read,
                                Action<int, string, string> write)
                            {
                                observedRead = read;
                                observedWrite = write;
                                return delegate { };
                            })
                    },
                    {
                        "OpenConfig",
                        new Func<
                            string,
                            Guid,
                            string,
                            int,
                            string,
                            object,
                            object>(
                            delegate(
                                string consumerId,
                                Guid registrationId,
                                string configKey,
                                int location,
                                string file,
                                object defaults)
                            {
                                return defaults;
                            })
                    },
                    {
                        "SaveConfig",
                        new Func<
                            string,
                            Guid,
                            string,
                            int,
                            string,
                            object,
                            object,
                            object>(
                            delegate(
                                string consumerId,
                                Guid registrationId,
                                string configKey,
                                int location,
                                string file,
                                object defaults,
                                object playerValues)
                            {
                                return playerValues;
                            })
                    }
                };

            var provider =
                new ApiDiscoveryProvider(
                    bus,
                    new ApiModIdentity(
                        "MarcoZechner.ConfigAPI",
                        "ConfigAPI",
                        new SemanticVersion(0, 1, 0)),
                    new ApiDescriptor(
                        "MarcoZechner.ConfigAPI",
                        new SemanticVersion(2, 0, 0)),
                    endpoints);

            provider.Start();

            var client =
                ConfigApiClient.CreateForSpaceEngineers(
                    bus,
                    "Example.Mod",
                    "Example Mod",
                    new SemanticVersion(1, 0, 0),
                    true,
                    "Uses ConfigAPI.");

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(observedRead, Is.Not.Null);
                Assert.That(observedWrite, Is.Not.Null);
                Assert.That(
                    observedRead.Target,
                    Is.TypeOf<SpaceEngineersConfigTextStorage>());
                Assert.That(
                    observedWrite.Target,
                    Is.SameAs(observedRead.Target));
            });

            client.Dispose();
            provider.Dispose();
        }

        private sealed class RecordingStorageUtilities :
            IConfigApiStorageUtilities
        {
            public bool LocalExists { get; set; }
            public bool GlobalExists { get; set; }
            public bool WorldExists { get; set; }
            public string ReadContent { get; set; }

            public string LastOperation { get; private set; }
            public string LastFile { get; private set; }
            public string LastContent { get; private set; }
            public Type LastScope { get; private set; }

            public int LocalReadCount { get; private set; }
            public int GlobalReadCount { get; private set; }
            public int WorldReadCount { get; private set; }
            public int GlobalWriteCount { get; private set; }
            public int WorldWriteCount { get; private set; }

            public bool FileExistsInLocalStorage(string file, Type scope)
            {
                Record("ExistsLocal", file, null, scope);
                return LocalExists;
            }

            public string ReadFileInLocalStorage(string file, Type scope)
            {
                LocalReadCount++;
                Record("ReadLocal", file, null, scope);
                return ReadContent;
            }

            public void WriteFileInLocalStorage(
                string file,
                string content,
                Type scope)
            {
                Record("WriteLocal", file, content, scope);
            }

            public bool FileExistsInGlobalStorage(string file)
            {
                Record("ExistsGlobal", file, null, null);
                return GlobalExists;
            }

            public string ReadFileInGlobalStorage(string file)
            {
                GlobalReadCount++;
                Record("ReadGlobal", file, null, null);
                return ReadContent;
            }

            public void WriteFileInGlobalStorage(string file, string content)
            {
                GlobalWriteCount++;
                Record("WriteGlobal", file, content, null);
            }

            public bool FileExistsInWorldStorage(string file, Type scope)
            {
                Record("ExistsWorld", file, null, scope);
                return WorldExists;
            }

            public string ReadFileInWorldStorage(string file, Type scope)
            {
                WorldReadCount++;
                Record("ReadWorld", file, null, scope);
                return ReadContent;
            }

            public void WriteFileInWorldStorage(
                string file,
                string content,
                Type scope)
            {
                WorldWriteCount++;
                Record("WriteWorld", file, content, scope);
            }

            private void Record(
                string operation,
                string file,
                string content,
                Type scope)
            {
                LastOperation = operation;
                LastFile = file;
                LastContent = content;
                LastScope = scope;
            }
        }

        private sealed class RecordingModMessageBus : IModMessageBus
        {
            private readonly Dictionary<long, List<Action<object>>> _handlers =
                new Dictionary<long, List<Action<object>>>();

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
