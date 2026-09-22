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
    public sealed class ConfigApiClientTests
    {
        [Test]
        public void Facade_Version_Matches_Current_Changelog()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    ApiVersionFile.MinimumProviderApiVersion.ToString(),
                    Is.EqualTo("2.0.0"));

                Assert.That(
                    ApiVersionFile.Changelog.CurrentVersion.ToString(),
                    Is.EqualTo(ApiVersionFile.VersionString));

                Assert.That(
                    ApiVersionFile.Changelog.Current.Version.ToString(),
                    Is.EqualTo(ApiVersionFile.VersionString));
            });
        }

        [Test]
        public void Start_Discovers_Provider_And_Registers_Consumer_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            string observedConsumerId = null;
            Guid observedRegistrationId = Guid.Empty;
            Func<int, string, string> observedRead = null;
            Action<int, string, string> observedWrite = null;
            var unregisterCount = 0;

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            observedConsumerId = consumerId;
                            observedRegistrationId = registrationId;
                            observedRead = read;
                            observedWrite = write;

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            provider.Start();

            var written = string.Empty;

            var client =
                CreateClient(
                    bus,
                    (location, file) => location + "|" + file,
                    (location, file, content) =>
                        written = location + "|" + file + "|" + content);

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderModVersion.ToString(), Is.EqualTo("0.1.0"));
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("2.0.0"));
                Assert.That(observedConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(observedRegistrationId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(observedRead, Is.Not.Null);
                Assert.That(observedWrite, Is.Not.Null);
            });

            Assert.That(
                observedRead(0, "config.toml"),
                Is.EqualTo("0|config.toml"));

            observedWrite(2, "world.toml", "content");

            Assert.That(
                written,
                Is.EqualTo("2|world.toml|content"));

            client.Dispose();

            Assert.That(unregisterCount, Is.EqualTo(1));

            provider.Dispose();
        }

        [Test]
        public void Provider_Reconnect_Uses_New_Registration_Id()
        {
            var bus = new RecordingModMessageBus();
            var registrationIds = new List<Guid>();
            var unregisterCount = 0;

            var firstProvider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            registrationIds.Add(registrationId);

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            firstProvider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.That(client.IsConnected, Is.True);
            Assert.That(registrationIds.Count, Is.EqualTo(1));

            firstProvider.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.False);
                Assert.That(unregisterCount, Is.EqualTo(1));
            });

            var secondProvider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 1, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            registrationIds.Add(registrationId);

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            secondProvider.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("2.1.0"));
                Assert.That(registrationIds.Count, Is.EqualTo(2));
                Assert.That(registrationIds[0], Is.Not.EqualTo(Guid.Empty));
                Assert.That(registrationIds[1], Is.Not.EqualTo(Guid.Empty));
                Assert.That(registrationIds[1], Is.Not.EqualTo(registrationIds[0]));
            });

            client.Dispose();

            Assert.That(unregisterCount, Is.EqualTo(2));

            secondProvider.Dispose();
        }

        [Test]
        public void Accepts_Newer_Provider_Without_Upper_Version_Ceiling()
        {
            var bus = new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(9, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            return delegate { };
                        }));

            provider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("9.0.0"));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Rejects_Provider_Missing_Exact_RegisterConsumer_Endpoint()
        {
            var bus = new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    new Dictionary<string, Delegate>(StringComparer.Ordinal));

            provider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.False);
                Assert.That(client.LastError, Is.Not.Null);
                Assert.That(
                    client.LastError.Message,
                    Does.Contain("RegisterConsumer"));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Load_Reloads_Requested_Variant_And_Keeps_Using_It()
        {
            var bus = new RecordingModMessageBus();
            var openedFiles = new List<string>();
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openedFiles.Add(file);
                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);

            handle.Load("alternate");
            handle.Reload();

            Assert.Multiple(() =>
            {
                Assert.That(handle.CurrentVariant, Is.EqualTo("alternate"));
                Assert.That(openedFiles, Is.EqualTo(new[] { "Settings.default.toml", "Settings.alternate.toml", "Settings.alternate.toml" }));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Load_Failure_Keeps_Previous_Variant_And_Value()
        {
            var bus = new RecordingModMessageBus();
            var openedFiles = new List<string>();
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openedFiles.Add(file);

                    if (file == "Settings.alternate.toml")
                        throw new InvalidOperationException("Synthetic open failure.");

                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);
            ConfigDocument previousValue = handle.Value;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => handle.Load("alternate"));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Synthetic open failure."));
                Assert.That(handle.CurrentVariant, Is.EqualTo(ConfigDefinition<ConfigDocument>.DefaultVariant));
                Assert.That(handle.Value.Equals(previousValue), Is.True);
                Assert.That(openedFiles, Is.EqualTo(new[] { "Settings.default.toml", "Settings.alternate.toml" }));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Runtime_State_Separates_Draft_Applied_Stored_And_Defaults()
        {
            var bus = new RecordingModMessageBus();
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults) { return ConfigDocumentWireCodec.Encode(Document(10)); });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            ConfigHandle<MutableConfig> handle = client.OpenHandle(CreateMutableDefinition(), ConfigLocation.Local);

            Assert.Multiple(() =>
            {
                Assert.That(handle.Defaults.Value, Is.EqualTo(1));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.Applied.Value, Is.EqualTo(10));
                Assert.That(handle.Draft.Value, Is.EqualTo(10));
                Assert.That(handle.HasDraftChanges, Is.False);
                Assert.That(handle.HasUnsavedChanges, Is.False);
                Assert.That(handle.Defaults, Is.Not.SameAs(handle.Stored));
                Assert.That(handle.Stored, Is.Not.SameAs(handle.Applied));
                Assert.That(handle.Applied, Is.Not.SameAs(handle.Draft));
            });

            handle.Draft.Value = 20;

            Assert.Multiple(() =>
            {
                Assert.That(handle.Draft.Value, Is.EqualTo(20));
                Assert.That(handle.Applied.Value, Is.EqualTo(10));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.False);
            });

            handle.Apply();
            handle.Draft.Value = 30;

            Assert.Multiple(() =>
            {
                Assert.That(handle.Applied.Value, Is.EqualTo(20));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.Draft.Value, Is.EqualTo(30));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.True);
            });

            handle.DiscardDraft();

            Assert.Multiple(() =>
            {
                Assert.That(handle.Draft.Value, Is.EqualTo(20));
                Assert.That(handle.HasDraftChanges, Is.False);
                Assert.That(handle.HasUnsavedChanges, Is.True);
            });

            handle.ResetDraftToDefaults();

            Assert.Multiple(() =>
            {
                Assert.That(handle.Draft.Value, Is.EqualTo(1));
                Assert.That(handle.Applied.Value, Is.EqualTo(20));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.True);
            });

            MutableConfig appliedSnapshot = handle.Applied;
            appliedSnapshot.Value = 99;
            Assert.That(handle.Applied.Value, Is.EqualTo(20));

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Save_Persists_Applied_Without_Saving_Unapplied_Draft()
        {
            var bus = new RecordingModMessageBus();
            ConfigDocument savedDocument = null;
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults) { return ConfigDocumentWireCodec.Encode(Document(10)); });
            endpoints["SaveConfig"] = new Func<string, Guid, string, int, string, object, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults, object playerValues)
                {
                    savedDocument = ConfigDocumentWireCodec.Decode(playerValues);
                    return playerValues;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            ConfigHandle<MutableConfig> handle = client.OpenHandle(CreateMutableDefinition(), ConfigLocation.Global);
            handle.Draft.Value = 20;
            handle.Apply();
            handle.Draft.Value = 30;
            handle.Save();

            ConfigValue savedValue;
            Assert.That(savedDocument.TryGet("Value", out savedValue), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That((long)savedValue.ScalarValue, Is.EqualTo(20L));
                Assert.That(handle.Stored.Value, Is.EqualTo(20));
                Assert.That(handle.Applied.Value, Is.EqualTo(20));
                Assert.That(handle.Draft.Value, Is.EqualTo(30));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.False);
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Apply_Deserialization_Failure_Preserves_Applied_State()
        {
            var bus = new RecordingModMessageBus();
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults) { return ConfigDocumentWireCodec.Encode(Document(10)); });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            var definition = new ConfigDefinition<MutableConfig>("Settings", () => new MutableConfig { Value = 1 }, value => Document(value.Value),
                document => { ConfigValue value; if (!document.TryGet("Value", out value)) throw new InvalidOperationException("Value missing."); int number = (int)(long)value.ScalarValue; if (number == 20) throw new InvalidOperationException("Synthetic deserialize failure."); return new MutableConfig { Value = number }; });
            ConfigHandle<MutableConfig> handle = client.OpenHandle(definition, ConfigLocation.Local);
            handle.Draft.Value = 20;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => handle.Apply());

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Synthetic deserialize failure."));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.Applied.Value, Is.EqualTo(10));
                Assert.That(handle.Draft.Value, Is.EqualTo(20));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.False);
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Save_Invalid_Returned_State_Preserves_Previous_Stored_State()
        {
            var bus = new RecordingModMessageBus();
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults) { return ConfigDocumentWireCodec.Encode(Document(10)); });
            endpoints["SaveConfig"] = new Func<string, Guid, string, int, string, object, object, object>(delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults, object playerValues) { return ConfigDocumentWireCodec.Encode(Document(99)); });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            var definition = new ConfigDefinition<MutableConfig>("Settings", () => new MutableConfig { Value = 1 }, value => Document(value.Value),
                document => { ConfigValue value; if (!document.TryGet("Value", out value)) throw new InvalidOperationException("Value missing."); int number = (int)(long)value.ScalarValue; if (number == 99) throw new InvalidOperationException("Synthetic saved-state failure."); return new MutableConfig { Value = number }; });
            ConfigHandle<MutableConfig> handle = client.OpenHandle(definition, ConfigLocation.Global);
            handle.Draft.Value = 20;
            handle.Apply();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => handle.Save());

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Synthetic saved-state failure."));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.Applied.Value, Is.EqualTo(20));
                Assert.That(handle.Draft.Value, Is.EqualTo(20));
                Assert.That(handle.HasDraftChanges, Is.False);
                Assert.That(handle.HasUnsavedChanges, Is.True);
            });

            client.Dispose();
            provider.Dispose();
        }
        [Test]
        public void Handle_Reload_Replaces_Stored_Applied_And_Draft_From_Active_Variant()
        {
            var bus = new RecordingModMessageBus();
            var openCount = 0;
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openCount++;
                    return ConfigDocumentWireCodec.Encode(Document(openCount == 1 ? 10 : 40));
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            ConfigHandle<MutableConfig> handle = client.OpenHandle(CreateMutableDefinition(), ConfigLocation.Local);
            handle.Draft.Value = 20;
            handle.Apply();
            handle.Draft.Value = 30;

            handle.Reload();

            Assert.Multiple(() =>
            {
                Assert.That(openCount, Is.EqualTo(2));
                Assert.That(handle.CurrentVariant, Is.EqualTo(ConfigDefinition<MutableConfig>.DefaultVariant));
                Assert.That(handle.Defaults.Value, Is.EqualTo(1));
                Assert.That(handle.Stored.Value, Is.EqualTo(40));
                Assert.That(handle.Applied.Value, Is.EqualTo(40));
                Assert.That(handle.Draft.Value, Is.EqualTo(40));
                Assert.That(handle.HasDraftChanges, Is.False);
                Assert.That(handle.HasUnsavedChanges, Is.False);
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_Load_Failure_Preserves_Complete_Runtime_State()
        {
            var bus = new RecordingModMessageBus();
            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    if (file == "Settings.alternate.toml")
                        throw new InvalidOperationException("Synthetic open failure.");

                    return ConfigDocumentWireCodec.Encode(Document(10));
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();
            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            ConfigHandle<MutableConfig> handle = client.OpenHandle(CreateMutableDefinition(), ConfigLocation.Local);
            handle.Draft.Value = 20;
            handle.Apply();
            handle.Draft.Value = 30;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => handle.Load("alternate"));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Synthetic open failure."));
                Assert.That(handle.CurrentVariant, Is.EqualTo(ConfigDefinition<MutableConfig>.DefaultVariant));
                Assert.That(handle.Defaults.Value, Is.EqualTo(1));
                Assert.That(handle.Stored.Value, Is.EqualTo(10));
                Assert.That(handle.Applied.Value, Is.EqualTo(20));
                Assert.That(handle.Draft.Value, Is.EqualTo(30));
                Assert.That(handle.HasDraftChanges, Is.True);
                Assert.That(handle.HasUnsavedChanges, Is.True);
            });

            client.Dispose();
            provider.Dispose();
        }
        [Test]
        public void Typed_Open_Delegate_Failures_Stop_At_Expected_Provider_Boundary()
        {
            var bus = new RecordingModMessageBus();
            var openCount = 0;
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openCount++;
                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var throwingDefaults = new ConfigDefinition<ConfigDocument>("Settings", () => { throw new InvalidOperationException("Synthetic default failure."); }, value => value, document => document);
            var nullDefaults = new ConfigDefinition<ConfigDocument>("Settings", () => null, value => value, document => document);
            var throwingSerializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => { throw new InvalidOperationException("Synthetic serializer failure."); }, document => document);
            var nullSerializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => null, document => document);
            var throwingDeserializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => { throw new InvalidOperationException("Synthetic deserializer failure."); });
            var nullDeserializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => null);

            InvalidOperationException defaultException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingDefaults, ConfigLocation.Local));
            InvalidOperationException nullDefaultException = Assert.Throws<InvalidOperationException>(() => client.Open(nullDefaults, ConfigLocation.Local));
            InvalidOperationException serializerException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingSerializer, ConfigLocation.Local));
            InvalidOperationException nullSerializerException = Assert.Throws<InvalidOperationException>(() => client.Open(nullSerializer, ConfigLocation.Local));

            Assert.Multiple(() =>
            {
                Assert.That(defaultException.Message, Is.EqualTo("Synthetic default failure."));
                Assert.That(nullDefaultException.Message, Does.Contain("default factory returned null"));
                Assert.That(serializerException.Message, Is.EqualTo("Synthetic serializer failure."));
                Assert.That(nullSerializerException.Message, Does.Contain("serializer returned null"));
                Assert.That(openCount, Is.EqualTo(0));
            });

            InvalidOperationException deserializerException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingDeserializer, ConfigLocation.Local));
            Assert.That(openCount, Is.EqualTo(1));

            InvalidOperationException nullDeserializerException = Assert.Throws<InvalidOperationException>(() => client.Open(nullDeserializer, ConfigLocation.Local));

            Assert.Multiple(() =>
            {
                Assert.That(deserializerException.Message, Is.EqualTo("Synthetic deserializer failure."));
                Assert.That(nullDeserializerException.Message, Does.Contain("deserializer returned null"));
                Assert.That(openCount, Is.EqualTo(2));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Typed_Save_Delegate_Failures_Stop_At_Expected_Provider_Boundary()
        {
            var bus = new RecordingModMessageBus();
            var saveCount = 0;
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["SaveConfig"] = new Func<string, Guid, string, int, string, object, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults, object encodedPlayerValues)
                {
                    saveCount++;
                    return encodedPlayerValues;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();
            var playerValues = new ConfigDocument();

            var throwingDefaults = new ConfigDefinition<ConfigDocument>("Settings", () => { throw new InvalidOperationException("Synthetic default failure."); }, value => value, document => document);
            var nullDefaults = new ConfigDefinition<ConfigDocument>("Settings", () => null, value => value, document => document);
            var throwingSerializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => { throw new InvalidOperationException("Synthetic serializer failure."); }, document => document);
            var nullSerializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => null, document => document);
            var playerSerializeCount = 0;
            var throwingPlayerSerializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => { playerSerializeCount++; if (playerSerializeCount == 2) throw new InvalidOperationException("Synthetic player serializer failure."); return value; }, document => document);
            var throwingDeserializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => { throw new InvalidOperationException("Synthetic deserializer failure."); });
            var nullDeserializer = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => null);

            InvalidOperationException defaultException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingDefaults, ConfigLocation.Local, playerValues));
            InvalidOperationException nullDefaultException = Assert.Throws<InvalidOperationException>(() => client.Save(nullDefaults, ConfigLocation.Local, playerValues));
            InvalidOperationException serializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingSerializer, ConfigLocation.Local, playerValues));
            InvalidOperationException nullSerializerException = Assert.Throws<InvalidOperationException>(() => client.Save(nullSerializer, ConfigLocation.Local, playerValues));
            InvalidOperationException playerSerializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingPlayerSerializer, ConfigLocation.Local, playerValues));

            Assert.Multiple(() =>
            {
                Assert.That(defaultException.Message, Is.EqualTo("Synthetic default failure."));
                Assert.That(nullDefaultException.Message, Does.Contain("default factory returned null"));
                Assert.That(serializerException.Message, Is.EqualTo("Synthetic serializer failure."));
                Assert.That(nullSerializerException.Message, Does.Contain("serializer returned null"));
                Assert.That(playerSerializerException.Message, Is.EqualTo("Synthetic player serializer failure."));
                Assert.That(playerSerializeCount, Is.EqualTo(2));
                Assert.That(saveCount, Is.EqualTo(0));
            });

            InvalidOperationException deserializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingDeserializer, ConfigLocation.Local, playerValues));
            Assert.That(saveCount, Is.EqualTo(1));

            InvalidOperationException nullDeserializerException = Assert.Throws<InvalidOperationException>(() => client.Save(nullDeserializer, ConfigLocation.Local, playerValues));

            Assert.Multiple(() =>
            {
                Assert.That(deserializerException.Message, Is.EqualTo("Synthetic deserializer failure."));
                Assert.That(nullDeserializerException.Message, Does.Contain("deserializer returned null"));
                Assert.That(saveCount, Is.EqualTo(2));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void World_Endpoints_Are_Optional_And_Surface_Asynchronous_Response()
        {
            var bus = new RecordingModMessageBus();
            Action<IDictionary<string, object>> worldCallback = null;
            Guid worldRegistrationId = Guid.Empty;
            var worldUnregisterCount = 0;
            var opened = false;

            IDictionary<string, Delegate> endpoints = ValidEndpoints(
                delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write)
                {
                    return delegate { };
                });

            endpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(
                delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
                {
                    worldRegistrationId = registrationId;
                    worldCallback = callback;
                    return delegate { worldUnregisterCount++; };
                });

            endpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(
                delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults)
                {
                    opened = true;
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    Assert.That(file, Is.EqualTo("settings.toml"));

                    worldCallback(
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "ConfigKey", "Settings" },
                            { "RequestId", 17UL },
                            { "Operation", "Open" },
                            { "TriggeredBy", 222UL },
                            { "IsApplied", false },
                            { "IsStale", false },
                            { "Error", null },
                            { "ServerIteration", 4UL },
                            { "CurrentFile", "settings.toml" },
                            { "Document", ConfigDocumentWireCodec.Encode(new ConfigDocument()) },
                        });
                });

            endpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(
                delegate(string consumerId, Guid registrationId, string configKey, object document) { });

            var provider = CreateProvider(bus, new SemanticVersion(2, 1, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            WorldConfigResponse observed = null;
            client.WorldConfigResponseReceived += delegate(WorldConfigResponse response) { observed = response; };
            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.SupportsWorldConfigs, Is.True);
                Assert.That(worldRegistrationId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(worldCallback, Is.Not.Null);
            });

            client.OpenWorld("Settings", "settings.toml", new ConfigDocument());

            Assert.Multiple(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(observed, Is.Not.Null);
                Assert.That(observed.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(observed.RequestId, Is.EqualTo(17UL));
                Assert.That(observed.Operation, Is.EqualTo(WorldConfigOperation.Open));
                Assert.That(observed.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(observed.IsApplied, Is.False);
                Assert.That(observed.IsStale, Is.False);
                Assert.That(observed.IsError, Is.False);
                Assert.That(observed.HasSnapshot, Is.True);
                Assert.That(observed.ServerIteration, Is.EqualTo(4UL));
                Assert.That(observed.CurrentFile, Is.EqualTo("settings.toml"));
                Assert.That(observed.Document, Is.Not.Null);
            });

            client.Dispose();
            Assert.That(worldUnregisterCount, Is.EqualTo(1));
            provider.Dispose();

            var legacyBus = new RecordingModMessageBus();
            var legacyProvider = CreateProvider(
                legacyBus,
                new SemanticVersion(2, 0, 0),
                ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; }));

            legacyProvider.Start();

            var legacyClient = CreateClient(legacyBus, (location, file) => null, (location, file, content) => { });
            legacyClient.Start();

            Assert.Multiple(() =>
            {
                Assert.That(legacyClient.IsConnected, Is.True);
                Assert.That(legacyClient.SupportsWorldConfigs, Is.False);
                Assert.Throws<InvalidOperationException>(() => legacyClient.OpenWorld("Settings", "settings.toml", new ConfigDocument()));
            });

            legacyClient.Dispose();
            legacyProvider.Dispose();
        }

#if CONFIGAPI_CONSUMER_2_2_TESTS
        [Test]
        public void World_File_Operation_Endpoints_Are_Optional_And_Invoke_Exact_Provider_Contract()
        {
            var bus = new RecordingModMessageBus();
            Guid worldRegistrationId = Guid.Empty;
            var operations = new List<string>();
            string loadedFile = null;
            string savedFile = null;
            string exportedFile = null;
            ConfigDocument savedDocument = null;
            ConfigDocument exportedDocument = null;
            var exportedOverwrite = false;

            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(
                delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
                {
                    worldRegistrationId = registrationId;
                    return delegate { };
                });
            endpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults) { });
            endpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(delegate(string consumerId, Guid registrationId, string configKey, object document) { });
            endpoints["ReloadWorldConfig"] = new Action<string, Guid, string>(
                delegate(string consumerId, Guid registrationId, string configKey)
                {
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    operations.Add("Reload");
                });
            endpoints["LoadAndSwitchWorldConfig"] = new Action<string, Guid, string, string>(
                delegate(string consumerId, Guid registrationId, string configKey, string file)
                {
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    loadedFile = file;
                    operations.Add("LoadAndSwitch");
                });
            endpoints["SaveAndSwitchWorldConfig"] = new Action<string, Guid, string, string, object>(
                delegate(string consumerId, Guid registrationId, string configKey, string file, object document)
                {
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    savedFile = file;
                    savedDocument = ConfigDocumentWireCodec.Decode(document);
                    operations.Add("SaveAndSwitch");
                });
            endpoints["ExportWorldConfig"] = new Action<string, Guid, string, string, object, bool>(
                delegate(string consumerId, Guid registrationId, string configKey, string file, object document, bool overwrite)
                {
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    exportedFile = file;
                    exportedDocument = ConfigDocumentWireCodec.Decode(document);
                    exportedOverwrite = overwrite;
                    operations.Add("Export");
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 2, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();
            var values = new ConfigDocument();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.SupportsWorldConfigs, Is.True);
                Assert.That(client.SupportsWorldFileOperations, Is.True);
            });

            client.ReloadWorld("Settings");
            client.LoadAndSwitchWorld("Settings", "alternate.toml");
            client.SaveAndSwitchWorld("Settings", "saved.toml", values);
            client.ExportWorld("Settings", "copy.toml", values, true);

            Assert.Multiple(() =>
            {
                Assert.That(operations, Is.EqualTo(new[] { "Reload", "LoadAndSwitch", "SaveAndSwitch", "Export" }));
                Assert.That(loadedFile, Is.EqualTo("alternate.toml"));
                Assert.That(savedFile, Is.EqualTo("saved.toml"));
                Assert.That(savedDocument.Equals(values), Is.True);
                Assert.That(exportedFile, Is.EqualTo("copy.toml"));
                Assert.That(exportedDocument.Equals(values), Is.True);
                Assert.That(exportedOverwrite, Is.True);
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void World_File_Operation_Group_Preserves_21_Compatibility_And_Rejects_Partial_22_Contract()
        {
            var legacyBus = new RecordingModMessageBus();
            IDictionary<string, Delegate> legacyEndpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            legacyEndpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback) { return delegate { }; });
            legacyEndpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults) { });
            legacyEndpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(delegate(string consumerId, Guid registrationId, string configKey, object document) { });

            var legacyProvider = CreateProvider(legacyBus, new SemanticVersion(2, 1, 0), legacyEndpoints);
            legacyProvider.Start();
            var legacyClient = CreateClient(legacyBus, (location, file) => null, (location, file, content) => { });
            legacyClient.Start();

            Assert.Multiple(() =>
            {
                Assert.That(legacyClient.IsConnected, Is.True);
                Assert.That(legacyClient.SupportsWorldConfigs, Is.True);
                Assert.That(legacyClient.SupportsWorldFileOperations, Is.False);
                Assert.Throws<InvalidOperationException>(() => legacyClient.ReloadWorld("Settings"));
            });

            legacyClient.Dispose();
            legacyProvider.Dispose();

            var partialBus = new RecordingModMessageBus();
            IDictionary<string, Delegate> partialEndpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            partialEndpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback) { return delegate { }; });
            partialEndpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults) { });
            partialEndpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(delegate(string consumerId, Guid registrationId, string configKey, object document) { });
            partialEndpoints["ReloadWorldConfig"] = new Action<string, Guid, string>(delegate(string consumerId, Guid registrationId, string configKey) { });

            var partialProvider = CreateProvider(partialBus, new SemanticVersion(2, 2, 0), partialEndpoints);
            partialProvider.Start();
            var partialClient = CreateClient(partialBus, (location, file) => null, (location, file, content) => { });
            partialClient.Start();

            Assert.Multiple(() =>
            {
                Assert.That(partialClient.IsConnected, Is.False);
                Assert.That(partialClient.LastError, Is.Not.Null);
                Assert.That(partialClient.LastError.Message, Does.Contain("incomplete World file-operation endpoint set"));
            });

            partialClient.Dispose();
            partialProvider.Dispose();
        }
#endif

#if CONFIGAPI_CONSUMER_2_3_TESTS
        [Test]
        public void Preset_Endpoints_Are_Independent_Optional_Capabilities_And_Use_Canonical_File()
        {
            var bus = new RecordingModMessageBus();
            string canonicalFile = null;
            string localPresetFile = null;
            string worldPresetFile = null;
            Action<IDictionary<string, object>> worldCallback = null;
            var presetResult = new ConfigDocument(new ConfigEntry("Value", ConfigValue.Integer(30)));

            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["ApplyPresetConfig"] = new Func<string, Guid, string, int, string, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string canonical, string preset, object defaults)
                {
                    canonicalFile = canonical;
                    localPresetFile = preset;
                    return ConfigDocumentWireCodec.Encode(presetResult);
                });
            endpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(
                delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
                {
                    worldCallback = callback;
                    return delegate { };
                });
            endpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults) { });
            endpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(delegate(string consumerId, Guid registrationId, string configKey, object document) { });
            endpoints["ApplyPresetWorldConfig"] = new Action<string, Guid, string, string>(
                delegate(string consumerId, Guid registrationId, string configKey, string preset)
                {
                    worldPresetFile = preset;
                    worldCallback(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "ConfigKey", configKey },
                        { "RequestId", 17UL },
                        { "Operation", "ApplyPreset" },
                        { "TriggeredBy", 222UL },
                        { "IsApplied", true },
                        { "IsStale", false },
                        { "Error", null },
                        { "ServerIteration", 2UL },
                        { "CurrentFile", "settings.toml" },
                        { "Document", ConfigDocumentWireCodec.Encode(presetResult) },
                    });
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 3, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            WorldConfigResponse observed = null;
            client.WorldConfigResponseReceived += delegate(WorldConfigResponse response) { observed = response; };
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", () => new ConfigDocument(), value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);
            handle.Load("legacy");
            ConfigDocument applied = handle.ApplyPreset("preset.toml");
            client.ApplyPresetWorld("Settings", "world-preset.toml");

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.SupportsPresets, Is.True);
                Assert.That(client.SupportsWorldConfigs, Is.True);
                Assert.That(client.SupportsWorldPresets, Is.True);
                Assert.That(client.SupportsWorldFileOperations, Is.False);
                Assert.That(canonicalFile, Is.EqualTo("Settings.default.toml"));
                Assert.That(localPresetFile, Is.EqualTo("preset.toml"));
                Assert.That(handle.CurrentVariant, Is.EqualTo(ConfigDefinition<ConfigDocument>.DefaultVariant));
                Assert.That(handle.Value.Equals(presetResult), Is.True);
                Assert.That(applied.Equals(presetResult), Is.True);
                Assert.That(worldPresetFile, Is.EqualTo("world-preset.toml"));
                Assert.That(observed, Is.Not.Null);
                Assert.That(observed.Operation, Is.EqualTo(WorldConfigOperation.ApplyPreset));
                Assert.That(observed.IsApplied, Is.True);
                Assert.That(observed.CurrentFile, Is.EqualTo("settings.toml"));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Preset_Saving_Is_Independent_And_Does_Not_Change_Handle_Current_File()
        {
            var bus = new RecordingModMessageBus();
            string canonicalFile = null;
            string localPresetFile = null;
            string worldPresetFile = null;
            ConfigDocument localDocument = null;
            ConfigDocument worldDocument = null;
            var localOverwrite = false;
            var worldOverwrite = true;
            Action<IDictionary<string, object>> worldCallback = null;
            var values = new ConfigDocument(new ConfigEntry("Value", ConfigValue.Integer(30)));

            IDictionary<string, Delegate> endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });
            endpoints["SavePresetConfig"] = new Func<string, Guid, string, int, string, string, object, object, bool, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string canonical, string preset, object defaults, object document, bool overwrite)
                {
                    canonicalFile = canonical;
                    localPresetFile = preset;
                    localDocument = ConfigDocumentWireCodec.Decode(document);
                    localOverwrite = overwrite;
                    return document;
                });
            endpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(
                delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
                {
                    worldCallback = callback;
                    return delegate { };
                });
            endpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults) { });
            endpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(delegate(string consumerId, Guid registrationId, string configKey, object document) { });
            endpoints["SavePresetWorldConfig"] = new Action<string, Guid, string, string, object, bool>(
                delegate(string consumerId, Guid registrationId, string configKey, string preset, object document, bool overwrite)
                {
                    worldPresetFile = preset;
                    worldDocument = ConfigDocumentWireCodec.Decode(document);
                    worldOverwrite = overwrite;
                    worldCallback(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "ConfigKey", configKey },
                        { "RequestId", 23UL },
                        { "Operation", "SavePreset" },
                        { "TriggeredBy", 222UL },
                        { "IsApplied", false },
                        { "IsStale", false },
                        { "Error", null },
                        { "ServerIteration", 4UL },
                        { "CurrentFile", "settings.toml" },
                        { "Document", ConfigDocumentWireCodec.Encode(values) },
                    });
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 3, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            WorldConfigResponse observed = null;
            client.WorldConfigResponseReceived += delegate(WorldConfigResponse response) { observed = response; };
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", () => values, value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);
            handle.Load("legacy");
            ConfigDocument saved = handle.SavePreset("local-preset.toml", true);
            client.SavePresetWorld("Settings", "world-preset.toml", values);

            Assert.Multiple(() =>
            {
                Assert.That(client.SupportsPresets, Is.False);
                Assert.That(client.SupportsPresetSaving, Is.True);
                Assert.That(client.SupportsWorldPresets, Is.False);
                Assert.That(client.SupportsWorldPresetSaving, Is.True);
                Assert.That(canonicalFile, Is.EqualTo("Settings.default.toml"));
                Assert.That(localPresetFile, Is.EqualTo("local-preset.toml"));
                Assert.That(localDocument.Equals(values), Is.True);
                Assert.That(localOverwrite, Is.True);
                Assert.That(handle.CurrentVariant, Is.EqualTo("legacy"));
                Assert.That(handle.Value.Equals(values), Is.True);
                Assert.That(saved.Equals(values), Is.True);
                Assert.That(worldPresetFile, Is.EqualTo("world-preset.toml"));
                Assert.That(worldDocument.Equals(values), Is.True);
                Assert.That(worldOverwrite, Is.False);
                Assert.That(observed.Operation, Is.EqualTo(WorldConfigOperation.SavePreset));
                Assert.That(observed.IsApplied, Is.False);
                Assert.That(observed.ServerIteration, Is.EqualTo(4UL));
            });

            client.Dispose();
            provider.Dispose();
        }
        [Test]
        public void Preset_Capabilities_Remain_Optional_For_22_Provider()
        {
            var bus = new RecordingModMessageBus();
            var provider = CreateProvider(bus, new SemanticVersion(2, 2, 0), ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; }));
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.SupportsPresets, Is.False);
                Assert.That(client.SupportsWorldPresets, Is.False);
                Assert.That(client.SupportsPresetSaving, Is.False);
                Assert.That(client.SupportsWorldPresetSaving, Is.False);
                Assert.Throws<InvalidOperationException>(() => client.ApplyPreset("Settings", ConfigLocation.Local, "settings.toml", "preset.toml", new ConfigDocument()));
                Assert.Throws<InvalidOperationException>(() => client.ApplyPresetWorld("Settings", "preset.toml"));
                Assert.Throws<InvalidOperationException>(() => client.SavePreset("Settings", ConfigLocation.Local, "settings.toml", "preset.toml", new ConfigDocument(), new ConfigDocument()));
                Assert.Throws<InvalidOperationException>(() => client.SavePresetWorld("Settings", "preset.toml", new ConfigDocument()));
            });

            client.Dispose();
            provider.Dispose();
        }
#endif
        [Test]
        public void Constructor_Rejects_Invalid_Consumer_Identity_And_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            var version = new SemanticVersion(1, 2, 3);
            Func<int, string, bool> exists = (location, file) => false;
            Func<int, string, string> read = (location, file) => null;
            Action<int, string, string> write = (location, file, content) => { };
            Func<int, string[]> listKnown = location => new string[0];

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(null, "Example.Mod", "Example Mod", version, true, "Config", exists, read, write, listKnown));
                Assert.Throws<ArgumentException>(() => new ConfigApiClient(bus, " ", "Example Mod", version, true, "Config", exists, read, write, listKnown));
                Assert.Throws<ArgumentException>(() => new ConfigApiClient(bus, "Example.Mod", " ", version, true, "Config", exists, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(bus, "Example.Mod", "Example Mod", null, true, "Config", exists, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(bus, "Example.Mod", "Example Mod", version, true, "Config", null, read, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(bus, "Example.Mod", "Example Mod", version, true, "Config", exists, null, write, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(bus, "Example.Mod", "Example Mod", version, true, "Config", exists, read, null, listKnown));
                Assert.Throws<ArgumentNullException>(() => new ConfigApiClient(bus, "Example.Mod", "Example Mod", version, true, "Config", exists, read, write, null));
            });
        }

        private static ConfigApiClient CreateClient(IModMessageBus bus, Func<int, string, string> read, Action<int, string, string> write)
        {
            return new ConfigApiClient(bus, "Example.Mod", "Example Mod", new SemanticVersion(2, 3, 4), true,
                "Uses ConfigAPI for configuration.", (location, file) => read(location, file) != null, read, write, location => new string[0]);
        }
        private static ApiDiscoveryProvider CreateProvider(
            IModMessageBus bus,
            SemanticVersion apiVersion,
            IDictionary<string, Delegate> endpoints)
        {
            return new ApiDiscoveryProvider(
                bus,
                new ApiModIdentity(
                    "MarcoZechner.ConfigAPI",
                    "ConfigAPI",
                    new SemanticVersion(0, 1, 0)),
                new ApiDescriptor(
                    "MarcoZechner.ConfigAPI",
                    apiVersion),
                endpoints);
        }

        private static IDictionary<string, Delegate> ValidEndpoints(
            Func<string, Guid, Func<int, string, string>, Action<int, string, string>, Action> registerConsumer)
        {
            return new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                {
                    "RegisterConsumer",
                    new Func<string, Guid, Func<int, string, bool>, Func<int, string, string>, Action<int, string, string>, Func<int, string[]>, Action>(
                        delegate(string consumerId, Guid registrationId, Func<int, string, bool> exists, Func<int, string, string> read, Action<int, string, string> write, Func<int, string[]> listKnown)
                        {
                            return registerConsumer(consumerId, registrationId, read, write);
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
                },
                {
                    "LoadAndSwitchConfig",
                    new Func<string, Guid, string, int, string, string, object, object>(
                        delegate(string consumerId, Guid registrationId, string configKey, int location, string currentFile, string targetFile, object defaults)
                        {
                            return defaults;
                        })
                }
            };
        }
        private static ConfigDefinition<MutableConfig> CreateMutableDefinition() =>
            new ConfigDefinition<MutableConfig>("Settings", () => new MutableConfig { Value = 1 },
                value => Document(value.Value),
                document => { ConfigValue value; if (!document.TryGet("Value", out value)) throw new InvalidOperationException("Value missing."); return new MutableConfig { Value = (int)(long)value.ScalarValue }; });

        private static ConfigDocument Document(int value) => new ConfigDocument(new ConfigEntry("Value", ConfigValue.Integer(value)));

        private sealed class MutableConfig
        {
            public int Value { get; set; }
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
