using System;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigNetworkCodecTests
    {
        [Test]
        public void Request_RoundTrips_Open_With_Current_Defaults()
        {
            var defaults = Document(Entry("Value", Integer(10)));
            var request = new WorldConfigNetworkRequest(
                17UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                "settings.toml", false, defaults, null);

            byte[] encoded = WorldConfigNetworkCodec.EncodeRequest(request);
            WorldConfigNetworkRequest decoded = WorldConfigNetworkCodec.DecodeRequest(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(17UL));
                Assert.That(decoded.ConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(decoded.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(decoded.BaseIteration, Is.EqualTo(0UL));
                Assert.That(decoded.File, Is.EqualTo("settings.toml"));
                Assert.That(decoded.Overwrite, Is.False);
                Assert.That(decoded.Defaults, Is.EqualTo(defaults));
                Assert.That(decoded.Document, Is.Null);
            });
        }

        [Test]
        public void Request_RoundTrips_Save_With_Draft_And_Base_Iteration()
        {
            var draft = Document(
                Entry("Value", Integer(25)),
                Entry("Nested", new ConfigObjectNode(Entry("Enabled", ConfigScalarNode.Boolean(true)))));

            var request = new WorldConfigNetworkRequest(
                18UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 7UL,
                null, false, null, draft);

            byte[] encoded = WorldConfigNetworkCodec.EncodeRequest(request);
            WorldConfigNetworkRequest decoded = WorldConfigNetworkCodec.DecodeRequest(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(18UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Save));
                Assert.That(decoded.BaseIteration, Is.EqualTo(7UL));
                Assert.That(decoded.File, Is.Null);
                Assert.That(decoded.Overwrite, Is.False);
                Assert.That(decoded.Defaults, Is.Null);
                Assert.That(decoded.Document, Is.EqualTo(draft));
            });
        }

        [Test]
        public void Response_RoundTrips_Applied_Authoritative_Snapshot()
        {
            var snapshot = Snapshot(25, 8UL, "settings.toml");
            var response = new WorldConfigNetworkResponse(
                18UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot,
                76561198000000001UL, true, false, snapshot, null);

            byte[] encoded = WorldConfigNetworkCodec.EncodeResponse(response);
            WorldConfigNetworkResponse decoded = WorldConfigNetworkCodec.DecodeResponse(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(18UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Save));
                Assert.That(decoded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(decoded.TriggeredBy, Is.EqualTo(76561198000000001UL));
                Assert.That(decoded.IsApplied, Is.True);
                Assert.That(decoded.IsStale, Is.False);
                Assert.That(decoded.Snapshot.Identity, Is.EqualTo(snapshot.Identity));
                Assert.That(decoded.Snapshot.Document, Is.EqualTo(snapshot.Document));
                Assert.That(decoded.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(decoded.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                Assert.That(decoded.Error, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Stale_Authoritative_Snapshot()
        {
            var snapshot = Snapshot(20, 9UL, "settings.toml");
            var response = new WorldConfigNetworkResponse(
                19UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot,
                76561198000000002UL, false, true, snapshot, null);

            WorldConfigNetworkResponse decoded =
                WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.IsApplied, Is.False);
                Assert.That(decoded.IsStale, Is.True);
                Assert.That(decoded.Snapshot.ServerIteration, Is.EqualTo(9UL));
                Assert.That(decoded.Snapshot.Document, Is.EqualTo(snapshot.Document));
                Assert.That(decoded.Error, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Error_Without_Authoritative_Mutation()
        {
            var response = new WorldConfigNetworkResponse(
                20UL, WorldConfigNetworkOperation.SaveAndSwitch, WorldConfigNetworkResponseKind.Error,
                76561198000000003UL, false, false, null,
                "Permission denied: Only admins can perform this operation.");

            WorldConfigNetworkResponse decoded =
                WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(20UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.SaveAndSwitch));
                Assert.That(decoded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(decoded.TriggeredBy, Is.EqualTo(76561198000000003UL));
                Assert.That(decoded.IsApplied, Is.False);
                Assert.That(decoded.IsStale, Is.False);
                Assert.That(decoded.Snapshot, Is.Null);
                Assert.That(decoded.Error, Is.EqualTo("Permission denied: Only admins can perform this operation."));
            });
        }

        [Test]
        public void Request_And_Response_Codecs_Are_Deterministic()
        {
            var request = new WorldConfigNetworkRequest(
                21UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Export, 11UL,
                "copy.toml", true, null, Document(Entry("Value", Integer(44))));

            var response = new WorldConfigNetworkResponse(
                21UL, WorldConfigNetworkOperation.Export, WorldConfigNetworkResponseKind.Exported,
                76561198000000004UL, false, false, Snapshot(40, 11UL, "settings.toml"), null);

            byte[] firstRequest = WorldConfigNetworkCodec.EncodeRequest(request);
            byte[] secondRequest = WorldConfigNetworkCodec.EncodeRequest(request);
            byte[] firstResponse = WorldConfigNetworkCodec.EncodeResponse(response);
            byte[] secondResponse = WorldConfigNetworkCodec.EncodeResponse(response);

            Assert.Multiple(() =>
            {
                Assert.That(secondRequest, Is.EqualTo(firstRequest));
                Assert.That(secondResponse, Is.EqualTo(firstResponse));
            });
        }

        [Test]
        public void Decode_Rejects_Truncated_Trailing_And_Unsupported_Version_Data()
        {
            var request = new WorldConfigNetworkRequest(
                22UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                "settings.toml", false, Document(Entry("Value", Integer(10))), null);

            byte[] encoded = WorldConfigNetworkCodec.EncodeRequest(request);

            for (var length = 0; length < encoded.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(encoded, truncated, length);
                Assert.Throws<ArgumentException>(() => WorldConfigNetworkCodec.DecodeRequest(truncated), "Accepted truncated request length " + length);
            }

            var trailing = new byte[encoded.Length + 1];
            Array.Copy(encoded, trailing, encoded.Length);
            trailing[trailing.Length - 1] = 0x7f;
            Assert.Throws<ArgumentException>(() => WorldConfigNetworkCodec.DecodeRequest(trailing));

            byte[] unsupported = WorldConfigNetworkCodec.EncodeRequest(request);
            Assert.That(unsupported.Length, Is.GreaterThan(4));
            unsupported[4] = 0x7f;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => WorldConfigNetworkCodec.DecodeRequest(unsupported));
            Assert.That(exception.Message, Does.Contain("version").IgnoreCase);
        }

        [Test]
        public void Codec_Rejects_Null_Requests_Responses_And_Payloads()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => WorldConfigNetworkCodec.EncodeRequest(null));
                Assert.Throws<ArgumentNullException>(() => WorldConfigNetworkCodec.DecodeRequest(null));
                Assert.Throws<ArgumentNullException>(() => WorldConfigNetworkCodec.EncodeResponse(null));
                Assert.Throws<ArgumentNullException>(() => WorldConfigNetworkCodec.DecodeResponse(null));
            });
        }

        private static WorldConfigSnapshot Snapshot(long value, ulong iteration, string file)
            => new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(value))), iteration, file);

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
            => new ConfigDocument(new ConfigObjectNode(entries));

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
            => new ConfigObjectEntry(name, value);

        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);
    }
}