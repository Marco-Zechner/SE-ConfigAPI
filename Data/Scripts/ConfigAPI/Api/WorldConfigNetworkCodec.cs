using System;
using System.Collections.Generic;
using System.Text;
using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Api
{
    public static class WorldConfigNetworkCodec
    {
        private const byte CurrentVersion = 3;
        private const byte RequestFrame = 1;
        private const byte ResponseFrame = 2;
        private const int MaximumStringBytes = 1048576;
        private const int MaximumDocumentBytes = 16777216;
        private const int MaximumVariantCount = 65536;

        private static readonly byte[] _magic = { 0x4D, 0x5A, 0x57, 0x43 };
        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        public static byte[] EncodeRequest(WorldConfigNetworkRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var writer = new ByteWriter();
            WriteHeader(writer, RequestFrame);
            WriteUInt64(writer, request.RequestId);
            WriteString(writer, request.ConsumerId);
            WriteString(writer, request.ConfigKey);
            writer.WriteByte((byte)request.Operation);
            WriteUInt64(writer, request.ExpectedRevision);
            WriteOptionalString(writer, request.Variant);
            WriteOptionalDocument(writer, request.Defaults);
            WriteOptionalDocument(writer, request.Document);
            return writer.ToArray();
        }

        public static WorldConfigNetworkRequest DecodeRequest(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var reader = new PayloadReader(payload);
            ReadHeader(reader, RequestFrame);

            ulong requestId = reader.ReadUInt64();
            string consumerId = reader.ReadRequiredString("consumer ID");
            string configKey = reader.ReadRequiredString("config key");
            WorldConfigNetworkOperation operation = ReadOperation(reader);
            ulong expectedRevision = reader.ReadUInt64();
            string variant = reader.ReadOptionalString();
            ConfigDocument defaults = reader.ReadOptionalDocument();
            ConfigDocument document = reader.ReadOptionalDocument();

            reader.EnsureAtEnd();
            return new WorldConfigNetworkRequest(requestId, consumerId, configKey, operation, expectedRevision, variant, defaults, document);
        }

        public static byte[] EncodeResponse(WorldConfigNetworkResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            var writer = new ByteWriter();
            WriteHeader(writer, ResponseFrame);
            WriteUInt64(writer, response.RequestId);
            writer.WriteByte((byte)response.Operation);
            writer.WriteByte((byte)response.Kind);
            WriteUInt64(writer, response.TriggeredBy);
            WriteBoolean(writer, response.IsChanged);
            WriteBoolean(writer, response.IsStale);
            WriteOptionalSnapshot(writer, response.Snapshot);
            WriteOptionalStringArray(writer, response.Variants);
            WriteOptionalString(writer, response.Error);
            return writer.ToArray();
        }

        public static WorldConfigNetworkResponse DecodeResponse(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var reader = new PayloadReader(payload);
            ReadHeader(reader, ResponseFrame);

            ulong requestId = reader.ReadUInt64();
            WorldConfigNetworkOperation operation = ReadOperation(reader);
            WorldConfigNetworkResponseKind kind = ReadResponseKind(reader);
            ulong triggeredBy = reader.ReadUInt64();
            bool isChanged = reader.ReadBoolean();
            bool isStale = reader.ReadBoolean();
            WorldConfigSnapshot snapshot = reader.ReadOptionalSnapshot();
            string[] variants = reader.ReadOptionalStringArray();
            string error = reader.ReadOptionalString();

            reader.EnsureAtEnd();
            return new WorldConfigNetworkResponse(requestId, operation, kind, triggeredBy, isChanged, isStale, snapshot, variants, error);
        }

        private static void WriteHeader(ByteWriter writer, byte frame)
        {
            writer.Write(_magic, 0, _magic.Length);
            writer.WriteByte(CurrentVersion);
            writer.WriteByte(frame);
        }

        private static void ReadHeader(PayloadReader reader, byte expectedFrame)
        {
            for (var index = 0; index < _magic.Length; index++)
                if (reader.ReadByte() != _magic[index])
                    throw new ArgumentException("World config network payload has invalid magic.");

            byte version = reader.ReadByte();
            if (version != CurrentVersion)
                throw new ArgumentException("World config network payload uses an unsupported version: " + version);

            byte frame = reader.ReadByte();
            if (frame != expectedFrame)
                throw new ArgumentException("World config network payload contains the wrong frame kind.");
        }

        private static WorldConfigNetworkOperation ReadOperation(PayloadReader reader)
        {
            var operation = (WorldConfigNetworkOperation)reader.ReadByte();
            WorldConfigNetworkRequest.EnsureOperation(operation);
            return operation;
        }

        private static WorldConfigNetworkResponseKind ReadResponseKind(PayloadReader reader)
        {
            var kind = (WorldConfigNetworkResponseKind)reader.ReadByte();

            if (kind < WorldConfigNetworkResponseKind.Snapshot || kind > WorldConfigNetworkResponseKind.Error)
                throw new ArgumentException("World config network payload contains an unsupported response kind: " + (byte)kind);

            return kind;
        }

        private static void WriteOptionalSnapshot(ByteWriter writer, WorldConfigSnapshot snapshot)
        {
            WriteBoolean(writer, snapshot != null);
            if (snapshot == null)
                return;

            WriteString(writer, snapshot.Identity.OwnerId);
            WriteString(writer, snapshot.Identity.ConfigKey);
            WriteDocument(writer, snapshot.Stored);
            WriteDocument(writer, snapshot.Applied);
            WriteUInt64(writer, snapshot.Revision);
            WriteString(writer, snapshot.CurrentVariant);
        }

        private static WorldConfigSnapshot ReadSnapshot(PayloadReader reader)
        {
            string ownerId = reader.ReadRequiredString("snapshot owner ID");
            string configKey = reader.ReadRequiredString("snapshot config key");
            ConfigDocument stored = reader.ReadDocument();
            ConfigDocument applied = reader.ReadDocument();
            ulong revision = reader.ReadUInt64();
            string currentVariant = reader.ReadRequiredString("snapshot current variant");
            return new WorldConfigSnapshot(new ConfigIdentity(ownerId, configKey), stored, applied, revision, currentVariant);
        }

        private static void WriteOptionalDocument(ByteWriter writer, ConfigDocument document)
        {
            WriteBoolean(writer, document != null);
            if (document != null)
                WriteDocument(writer, document);
        }

        private static void WriteDocument(ByteWriter writer, ConfigDocument document)
        {
            byte[] encoded = ConfigDocumentBinaryCodec.Encode(document);

            if (encoded.Length > MaximumDocumentBytes)
                throw new ArgumentException("World config network document exceeds the supported byte length.", nameof(document));

            WriteInt32(writer, encoded.Length);
            writer.Write(encoded, 0, encoded.Length);
        }

        private static void WriteOptionalString(ByteWriter writer, string value)
        {
            WriteBoolean(writer, value != null);
            if (value != null)
                WriteString(writer, value);
        }

        private static void WriteOptionalStringArray(ByteWriter writer, string[] values)
        {
            WriteBoolean(writer, values != null);
            if (values == null)
                return;
            if (values.Length > MaximumVariantCount)
                throw new ArgumentException("World config network variant count exceeds the supported range.", nameof(values));

            WriteInt32(writer, values.Length);
            for (var index = 0; index < values.Length; index++)
                WriteString(writer, values[index]);
        }

        private static void WriteString(ByteWriter writer, string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            byte[] bytes;

            try
            {
                bytes = _strictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw new ArgumentException("World config network string is not valid Unicode.", nameof(value), exception);
            }

            if (bytes.Length > MaximumStringBytes)
                throw new ArgumentException("World config network string exceeds the supported UTF-8 byte length.", nameof(value));

            WriteInt32(writer, bytes.Length);
            writer.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBoolean(ByteWriter writer, bool value) => writer.WriteByte(value ? (byte)1 : (byte)0);

        private static void WriteInt32(ByteWriter writer, int value)
        {
            uint raw = unchecked((uint)value);
            writer.WriteByte((byte)raw);
            writer.WriteByte((byte)(raw >> 8));
            writer.WriteByte((byte)(raw >> 16));
            writer.WriteByte((byte)(raw >> 24));
        }

        private static void WriteUInt64(ByteWriter writer, ulong value)
        {
            for (var index = 0; index < 8; index++)
                writer.WriteByte((byte)(value >> (index * 8)));
        }

        private sealed class ByteWriter
        {
            private readonly List<byte> _bytes = new List<byte>();

            public void WriteByte(byte value) => _bytes.Add(value);

            public void Write(byte[] values, int offset, int count)
            {
                if (values == null)
                    throw new ArgumentNullException(nameof(values));
                if (offset < 0 || count < 0 || offset > values.Length - count)
                    throw new ArgumentException("Byte writer range is outside the supplied array.");

                for (var index = 0; index < count; index++)
                    _bytes.Add(values[offset + index]);
            }

            public byte[] ToArray()
            {
                var result = new byte[_bytes.Count];
                _bytes.CopyTo(result);
                return result;
            }
        }

        private sealed class PayloadReader
        {
            private readonly byte[] _payload;
            private int _offset;

            public PayloadReader(byte[] payload)
            {
                _payload = payload;
            }

            public byte ReadByte()
            {
                Require(1);
                return _payload[_offset++];
            }

            public bool ReadBoolean()
            {
                byte value = ReadByte();

                if (value > 1)
                    throw new ArgumentException("World config network Boolean must be encoded as 0 or 1.");

                return value == 1;
            }

            public int ReadInt32()
            {
                Require(4);
                uint value = (uint)(_payload[_offset] | _payload[_offset + 1] << 8 | _payload[_offset + 2] << 16 | _payload[_offset + 3] << 24);
                _offset += 4;
                return unchecked((int)value);
            }

            public ulong ReadUInt64()
            {
                Require(8);
                ulong value = 0;

                for (var index = 0; index < 8; index++)
                    value |= (ulong)_payload[_offset + index] << (index * 8);

                _offset += 8;
                return value;
            }

            public string ReadRequiredString(string description)
            {
                string value = ReadString();

                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("World config network " + description + " must not be empty.");

                return value;
            }

            public string ReadOptionalString() => ReadBoolean() ? ReadString() : null;

            public string[] ReadOptionalStringArray()
            {
                if (!ReadBoolean())
                    return null;

                int count = ReadLength(MaximumVariantCount, "variant count");
                var values = new string[count];
                for (var index = 0; index < count; index++)
                    values[index] = ReadRequiredString("variant");
                return values;
            }

            public string ReadString()
            {
                int byteCount = ReadLength(MaximumStringBytes, "string");
                Require(byteCount);

                try
                {
                    string value = _strictUtf8.GetString(_payload, _offset, byteCount);
                    _offset += byteCount;
                    return value;
                }
                catch (DecoderFallbackException exception)
                {
                    throw new ArgumentException("World config network string is not valid UTF-8.", exception);
                }
            }

            public ConfigDocument ReadOptionalDocument() => ReadBoolean() ? ReadDocument() : null;

            public ConfigDocument ReadDocument()
            {
                int byteCount = ReadLength(MaximumDocumentBytes, "document");
                byte[] bytes = ReadBytes(byteCount);
                return ConfigDocumentBinaryCodec.Decode(bytes);
            }

            public WorldConfigSnapshot ReadOptionalSnapshot() => ReadBoolean() ? ReadSnapshot(this) : null;

            public void EnsureAtEnd()
            {
                if (_offset != _payload.Length)
                    throw new ArgumentException("World config network payload contains trailing data.");
            }

            private int ReadLength(int maximum, string description)
            {
                int length = ReadInt32();

                if (length < 0 || length > maximum)
                    throw new ArgumentException("World config network " + description + " length is outside the supported range.");

                return length;
            }

            private byte[] ReadBytes(int byteCount)
            {
                Require(byteCount);
                var result = new byte[byteCount];
                Array.Copy(_payload, _offset, result, 0, byteCount);
                _offset += byteCount;
                return result;
            }

            private void Require(int byteCount)
            {
                if (byteCount < 0 || _offset > _payload.Length - byteCount)
                    throw new ArgumentException("World config network payload is truncated.");
            }
        }
    }
}