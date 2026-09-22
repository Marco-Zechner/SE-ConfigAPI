using System;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
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
            var request = new WorldConfigNetworkRequest(17UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, defaults, null);

            WorldConfigNetworkRequest decoded = WorldConfigNetworkCodec.DecodeRequest(WorldConfigNetworkCodec.EncodeRequest(request));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(17UL));
                Assert.That(decoded.ConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(decoded.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(decoded.ExpectedRevision, Is.EqualTo(0UL));
                Assert.That(decoded.Variant, Is.Null);
                Assert.That(decoded.Defaults, Is.EqualTo(defaults));
                Assert.That(decoded.Document, Is.Null);
            });
        }

        [Test]
        public void Request_RoundTrips_Apply_With_Draft_And_Expected_Revision()
        {
            var draft = Document(Entry("Value", Integer(25)), Entry("Nested", new ConfigObjectNode(Entry("Enabled", ConfigScalarNode.Boolean(true)))));
            var request = new WorldConfigNetworkRequest(18UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 7UL, null, null, draft);

            WorldConfigNetworkRequest decoded = WorldConfigNetworkCodec.DecodeRequest(WorldConfigNetworkCodec.EncodeRequest(request));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(18UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Apply));
                Assert.That(decoded.ExpectedRevision, Is.EqualTo(7UL));
                Assert.That(decoded.Variant, Is.Null);
                Assert.That(decoded.Defaults, Is.Null);
                Assert.That(decoded.Document, Is.EqualTo(draft));
            });
        }

        [Test]
        public void Request_RoundTrips_Variant_Operation()
        {
            var request = new WorldConfigNetworkRequest(19UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SaveAs, 8UL, "combat", null, null);

            WorldConfigNetworkRequest decoded = WorldConfigNetworkCodec.DecodeRequest(WorldConfigNetworkCodec.EncodeRequest(request));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.SaveAs));
                Assert.That(decoded.ExpectedRevision, Is.EqualTo(8UL));
                Assert.That(decoded.Variant, Is.EqualTo("combat"));
                Assert.That(decoded.Defaults, Is.Null);
                Assert.That(decoded.Document, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Changed_Authoritative_Snapshot()
        {
            var stored = Document(Entry("Value", Integer(20)));
            var applied = Document(Entry("Value", Integer(25)));
            var snapshot = new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), stored, applied, 8UL, "combat");
            var response = new WorldConfigNetworkResponse(18UL, WorldConfigNetworkOperation.Apply, WorldConfigNetworkResponseKind.Snapshot, 76561198000000001UL, true, false, snapshot, null, null);

            WorldConfigNetworkResponse decoded = WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(18UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.Apply));
                Assert.That(decoded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(decoded.TriggeredBy, Is.EqualTo(76561198000000001UL));
                Assert.That(decoded.IsChanged, Is.True);
                Assert.That(decoded.IsStale, Is.False);
                Assert.That(decoded.Snapshot.Identity, Is.EqualTo(snapshot.Identity));
                Assert.That(decoded.Snapshot.Stored, Is.EqualTo(stored));
                Assert.That(decoded.Snapshot.Applied, Is.EqualTo(applied));
                Assert.That(decoded.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(decoded.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                Assert.That(decoded.Snapshot.HasUnsavedChanges, Is.True);
                Assert.That(decoded.Variants, Is.Null);
                Assert.That(decoded.Error, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Stale_Authoritative_Snapshot()
        {
            var snapshot = Snapshot(20, 20, 9UL, "default");
            var response = new WorldConfigNetworkResponse(19UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot, 76561198000000002UL, false, true, snapshot, null, null);

            WorldConfigNetworkResponse decoded = WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.IsChanged, Is.False);
                Assert.That(decoded.IsStale, Is.True);
                Assert.That(decoded.Snapshot.Revision, Is.EqualTo(9UL));
                Assert.That(decoded.Snapshot.CurrentVariant, Is.EqualTo("default"));
                Assert.That(decoded.Snapshot.Applied, Is.EqualTo(snapshot.Applied));
                Assert.That(decoded.Variants, Is.Null);
                Assert.That(decoded.Error, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Variant_List()
        {
            var response = new WorldConfigNetworkResponse(20UL, WorldConfigNetworkOperation.ListVariants, WorldConfigNetworkResponseKind.Variants, 76561198000000003UL, false, false, null, new[] { "combat", "default", "dev" }, null);

            WorldConfigNetworkResponse decoded = WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(20UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.ListVariants));
                Assert.That(decoded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Variants));
                Assert.That(decoded.TriggeredBy, Is.EqualTo(76561198000000003UL));
                Assert.That(decoded.IsChanged, Is.False);
                Assert.That(decoded.IsStale, Is.False);
                Assert.That(decoded.Snapshot, Is.Null);
                Assert.That(decoded.Variants, Is.EqualTo(new[] { "combat", "default", "dev" }));
                Assert.That(decoded.Error, Is.Null);
            });
        }

        [Test]
        public void Response_RoundTrips_Error_Without_Authoritative_State()
        {
            var response = new WorldConfigNetworkResponse(21UL, WorldConfigNetworkOperation.SaveAs, WorldConfigNetworkResponseKind.Error, 76561198000000004UL, false, false, null, null, "Permission denied: Only admins can perform this operation.");

            WorldConfigNetworkResponse decoded = WorldConfigNetworkCodec.DecodeResponse(WorldConfigNetworkCodec.EncodeResponse(response));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.RequestId, Is.EqualTo(21UL));
                Assert.That(decoded.Operation, Is.EqualTo(WorldConfigNetworkOperation.SaveAs));
                Assert.That(decoded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(decoded.TriggeredBy, Is.EqualTo(76561198000000004UL));
                Assert.That(decoded.IsChanged, Is.False);
                Assert.That(decoded.IsStale, Is.False);
                Assert.That(decoded.Snapshot, Is.Null);
                Assert.That(decoded.Variants, Is.Null);
                Assert.That(decoded.Error, Is.EqualTo("Permission denied: Only admins can perform this operation."));
            });
        }

        [Test]
        public void Request_And_Response_Codecs_Are_Deterministic()
        {
            var request = new WorldConfigNetworkRequest(22UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Load, 11UL, "combat", null, null);
            var response = new WorldConfigNetworkResponse(22UL, WorldConfigNetworkOperation.Load, WorldConfigNetworkResponseKind.Snapshot, 76561198000000005UL, true, false, Snapshot(40, 40, 12UL, "combat"), null, null);

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
            var request = new WorldConfigNetworkRequest(23UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null);
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

        private static WorldConfigSnapshot Snapshot(long stored, long applied, ulong revision, string variant)
            => new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(stored))), Document(Entry("Value", Integer(applied))), revision, variant);

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);
    }
}
