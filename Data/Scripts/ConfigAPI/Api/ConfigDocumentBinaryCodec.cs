using System;
using System.Collections.Generic;
using System.Text;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public static class ConfigDocumentBinaryCodec
    {
        private const byte CurrentVersion = 1;
        private const int MaximumDepth = 64;
        private const int MaximumCollectionItems = 65536;
        private const int MaximumStringBytes = 1048576;

        private static readonly byte[] _magic = { 0x4D, 0x5A, 0x43, 0x44 };
        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        private enum NodeKind : byte
        {
            Null = 0,
            Boolean = 1,
            Integer = 2,
            Float = 3,
            String = 4,
            Object = 5,
            Array = 6,
            OffsetDateTime = 7,
            LocalDateTime = 8,
            LocalDate = 9,
            LocalTime = 10
        }

        public static byte[] Encode(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var writer = new ByteWriter();
            writer.Write(_magic, 0, _magic.Length);
            writer.WriteByte(CurrentVersion);
            WriteNode(writer, document.Root, 0);
            return writer.ToArray();
        }
        public static ConfigDocument Decode(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var reader = new PayloadReader(payload);

            for (var index = 0; index < _magic.Length; index++)
                if (reader.ReadByte() != _magic[index])
                    throw new ArgumentException("Config document binary payload has invalid magic.", nameof(payload));

            byte version = reader.ReadByte();
            if (version != CurrentVersion)
                throw new ArgumentException("Config document binary payload uses an unsupported version: " + version, nameof(payload));

            var root = ReadNode(reader, 0) as ConfigObjectNode;
            if (root == null)
                throw new ArgumentException("Config document binary root must be an Object node.", nameof(payload));

            if (!reader.IsAtEnd)
                throw new ArgumentException("Config document binary payload contains trailing data.", nameof(payload));

            return new ConfigDocument(root);
        }

        private static void WriteNode(ByteWriter writer, ConfigNode node, int depth)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            EnsureDepth(depth);

            if (node is ConfigNullNode)
            {
                writer.WriteByte((byte)NodeKind.Null);
                return;
            }

            var scalar = node as ConfigScalarNode;
            if (scalar != null)
            {
                WriteScalar(writer, scalar);
                return;
            }

            var obj = node as ConfigObjectNode;
            if (obj != null)
            {
                writer.WriteByte((byte)NodeKind.Object);
                WriteCount(writer, obj.Entries.Count);

                for (var index = 0; index < obj.Entries.Count; index++)
                {
                    ConfigObjectEntry entry = obj.Entries[index];
                    WriteString(writer, entry.Name);
                    WriteNode(writer, entry.Value, depth + 1);
                }

                return;
            }

            var array = node as ConfigArrayNode;
            if (array != null)
            {
                writer.WriteByte((byte)NodeKind.Array);
                WriteCount(writer, array.Items.Count);

                for (var index = 0; index < array.Items.Count; index++)
                    WriteNode(writer, array.Items[index], depth + 1);

                return;
            }

            throw new ArgumentException("Unsupported config node type: " + node.GetType().FullName, nameof(node));
        }

        private static ConfigNode ReadNode(PayloadReader reader, int depth)
        {
            EnsureDepth(depth);
            NodeKind kind = (NodeKind)reader.ReadByte();

            switch (kind)
            {
                case NodeKind.Null:
                    return ConfigNullNode.Instance;
                case NodeKind.Boolean:
                    return ConfigScalarNode.Boolean(reader.ReadBoolean());
                case NodeKind.Integer:
                    return ConfigScalarNode.Integer(reader.ReadInt64());
                case NodeKind.Float:
                    return ConfigScalarNode.Float(BitConverter.Int64BitsToDouble(reader.ReadInt64()));
                case NodeKind.String:
                    return ConfigScalarNode.String(reader.ReadString());
                case NodeKind.Object:
                    return ReadObject(reader, depth);
                case NodeKind.Array:
                    return ReadArray(reader, depth);
                case NodeKind.OffsetDateTime:
                    return ConfigScalarNode.OffsetDateTime(ReadOffsetDateTime(reader));
                case NodeKind.LocalDateTime:
                    return ConfigScalarNode.LocalDateTime(new ConfigLocalDateTime(ReadDate(reader), ReadTime(reader)));
                case NodeKind.LocalDate:
                    return ConfigScalarNode.LocalDate(ReadDate(reader));
                case NodeKind.LocalTime:
                    return ConfigScalarNode.LocalTime(ReadTime(reader));
                default:
                    throw new ArgumentException("Config document binary payload contains an unknown node kind: " + (byte)kind);
            }
        }

        private static void WriteScalar(ByteWriter writer, ConfigScalarNode scalar)
        {
            switch (scalar.Kind)
            {
                case ConfigScalarKind.Boolean:
                    writer.WriteByte((byte)NodeKind.Boolean);
                    WriteBoolean(writer, (bool)scalar.Value);
                    return;
                case ConfigScalarKind.Integer:
                    writer.WriteByte((byte)NodeKind.Integer);
                    WriteInt64(writer, (long)scalar.Value);
                    return;
                case ConfigScalarKind.Float:
                    writer.WriteByte((byte)NodeKind.Float);
                    WriteInt64(writer, BitConverter.DoubleToInt64Bits((double)scalar.Value));
                    return;
                case ConfigScalarKind.String:
                    writer.WriteByte((byte)NodeKind.String);
                    WriteString(writer, (string)scalar.Value);
                    return;
                case ConfigScalarKind.OffsetDateTime:
                    writer.WriteByte((byte)NodeKind.OffsetDateTime);
                    WriteOffsetDateTime(writer, (ConfigOffsetDateTime)scalar.Value);
                    return;
                case ConfigScalarKind.LocalDateTime:
                    writer.WriteByte((byte)NodeKind.LocalDateTime);
                    var localDateTime = (ConfigLocalDateTime)scalar.Value;
                    WriteDate(writer, localDateTime.Date);
                    WriteTime(writer, localDateTime.Time);
                    return;
                case ConfigScalarKind.LocalDate:
                    writer.WriteByte((byte)NodeKind.LocalDate);
                    WriteDate(writer, (ConfigLocalDate)scalar.Value);
                    return;
                case ConfigScalarKind.LocalTime:
                    writer.WriteByte((byte)NodeKind.LocalTime);
                    WriteTime(writer, (ConfigLocalTime)scalar.Value);
                    return;
                default:
                    throw new ArgumentException("Unsupported config scalar kind: " + scalar.Kind, nameof(scalar));
            }
        }

        private static ConfigObjectNode ReadObject(PayloadReader reader, int depth)
        {
            int count = reader.ReadCount();
            var entries = new ConfigObjectEntry[count];

            for (var index = 0; index < count; index++)
                entries[index] = new ConfigObjectEntry(reader.ReadString(), ReadNode(reader, depth + 1));

            return new ConfigObjectNode(entries);
        }

        private static ConfigArrayNode ReadArray(PayloadReader reader, int depth)
        {
            int count = reader.ReadCount();
            var items = new ConfigNode[count];

            for (var index = 0; index < count; index++)
                items[index] = ReadNode(reader, depth + 1);

            return new ConfigArrayNode(items);
        }

        private static void WriteOffsetDateTime(ByteWriter writer, ConfigOffsetDateTime value)
        {
            WriteDate(writer, value.Date);
            WriteTime(writer, value.Time);
            WriteInt32(writer, value.OffsetMinutes);
            WriteBoolean(writer, value.IsUnknownLocalOffset);
        }

        private static ConfigOffsetDateTime ReadOffsetDateTime(PayloadReader reader)
            => new ConfigOffsetDateTime(ReadDate(reader), ReadTime(reader), reader.ReadInt32(), reader.ReadBoolean());

        private static void WriteDate(ByteWriter writer, ConfigLocalDate value)
        {
            WriteInt32(writer, value.Year);
            WriteInt32(writer, value.Month);
            WriteInt32(writer, value.Day);
        }

        private static ConfigLocalDate ReadDate(PayloadReader reader)
            => new ConfigLocalDate(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

        private static void WriteTime(ByteWriter writer, ConfigLocalTime value)
        {
            WriteInt32(writer, value.Hour);
            WriteInt32(writer, value.Minute);
            WriteInt32(writer, value.Second);
            WriteString(writer, value.FractionalSeconds);
        }

        private static ConfigLocalTime ReadTime(PayloadReader reader)
            => new ConfigLocalTime(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadString());

        private static void WriteBoolean(ByteWriter writer, bool value) => writer.WriteByte(value ? (byte)1 : (byte)0);

        private static void WriteCount(ByteWriter writer, int count)
        {
            if (count < 0 || count > MaximumCollectionItems)
                throw new ArgumentException("Config document binary collection exceeds the supported item count.");

            WriteInt32(writer, count);
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
                throw new ArgumentException("Config document string is not valid Unicode.", nameof(value), exception);
            }

            if (bytes.Length > MaximumStringBytes)
                throw new ArgumentException("Config document string exceeds the supported UTF-8 byte length.", nameof(value));

            WriteInt32(writer, bytes.Length);
            writer.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt32(ByteWriter writer, int value)
        {
            uint raw = unchecked((uint)value);
            writer.WriteByte((byte)raw);
            writer.WriteByte((byte)(raw >> 8));
            writer.WriteByte((byte)(raw >> 16));
            writer.WriteByte((byte)(raw >> 24));
        }

        private static void WriteInt64(ByteWriter writer, long value)
        {
            ulong raw = unchecked((ulong)value);

            for (var index = 0; index < 8; index++)
                writer.WriteByte((byte)(raw >> (index * 8)));
        }

        private static void EnsureDepth(int depth)
        {
            if (depth > MaximumDepth)
                throw new ArgumentException("Config document binary nesting exceeds the supported depth.");
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

            public bool IsAtEnd => _offset == _payload.Length;

            public byte ReadByte()
            {
                Require(1);
                return _payload[_offset++];
            }

            public bool ReadBoolean()
            {
                byte value = ReadByte();

                if (value > 1)
                    throw new ArgumentException("Config document binary Boolean must be encoded as 0 or 1.");

                return value == 1;
            }

            public int ReadInt32()
            {
                Require(4);
                uint value = (uint)(_payload[_offset] | _payload[_offset + 1] << 8 | _payload[_offset + 2] << 16 | _payload[_offset + 3] << 24);
                _offset += 4;
                return unchecked((int)value);
            }

            public long ReadInt64()
            {
                Require(8);
                ulong value = 0;

                for (var index = 0; index < 8; index++)
                    value |= (ulong)_payload[_offset + index] << (index * 8);

                _offset += 8;
                return unchecked((long)value);
            }

            public int ReadCount()
            {
                int count = ReadInt32();

                if (count < 0 || count > MaximumCollectionItems)
                    throw new ArgumentException("Config document binary collection count is outside the supported range.");

                return count;
            }

            public string ReadString()
            {
                int byteCount = ReadInt32();

                if (byteCount < 0 || byteCount > MaximumStringBytes)
                    throw new ArgumentException("Config document binary string length is outside the supported range.");

                Require(byteCount);

                try
                {
                    string value = _strictUtf8.GetString(_payload, _offset, byteCount);
                    _offset += byteCount;
                    return value;
                }
                catch (DecoderFallbackException exception)
                {
                    throw new ArgumentException("Config document binary string is not valid UTF-8.", exception);
                }
            }

            private void Require(int byteCount)
            {
                if (byteCount < 0 || _offset > _payload.Length - byteCount)
                    throw new ArgumentException("Config document binary payload is truncated.");
            }
        }
    }
}