using System;
using System.Collections.Generic;
using System.Text;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;

namespace MarcoZechner.ConfigAPI.Serialization
{
    public static class ConfigDefaultsStoreCodec
    {
        private const byte CurrentVersion = 1;
        private const int MaximumEntries = 65536;
        private const int MaximumStringBytes = 1048576;
        private const int MaximumDocumentBytes = 16777216;

        private static readonly byte[] _magic = { 0x4D, 0x5A, 0x44, 0x46 };
        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        public static string Encode(ConfigDefaultsStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            ConfigDefaultsEntry[] entries = store.GetEntries();
            if (entries.Length > MaximumEntries)
                throw new ArgumentException("Config defaults store exceeds the supported entry count.", nameof(store));

            var writer = new ByteWriter();
            writer.Write(_magic);
            writer.WriteByte(CurrentVersion);
            WriteInt32(writer, entries.Length);

            for (var index = 0; index < entries.Length; index++)
            {
                ConfigDefaultsEntry entry = entries[index];
                WriteString(writer, entry.File);
                WriteString(writer, entry.Identity.OwnerId);
                WriteString(writer, entry.Identity.ConfigKey);

                byte[] document = ConfigDocumentBinaryCodec.Encode(entry.BaselineDefaults);
                if (document.Length > MaximumDocumentBytes)
                    throw new ArgumentException("Config defaults document exceeds the supported byte length.", nameof(store));

                WriteInt32(writer, document.Length);
                writer.Write(document);
            }

            return Convert.ToBase64String(writer.ToArray());
        }

        public static ConfigDefaultsStore Decode(string source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            byte[] payload;

            try
            {
                payload = Convert.FromBase64String(source);
            }
            catch (FormatException exception)
            {
                throw new FormatException("ConfigAPI defaults source is not valid base64.", exception);
            }

            try
            {
                var reader = new PayloadReader(payload);

                for (var index = 0; index < _magic.Length; index++)
                    if (reader.ReadByte() != _magic[index])
                        throw new FormatException("ConfigAPI defaults payload has invalid magic.");

                byte version = reader.ReadByte();
                if (version != CurrentVersion)
                    throw new NotSupportedException("ConfigAPI defaults payload uses an unsupported version: " + version);

                int count = reader.ReadInt32();
                if (count < 0 || count > MaximumEntries)
                    throw new FormatException("ConfigAPI defaults entry count is outside the supported range.");

                var entries = new ConfigDefaultsEntry[count];

                for (var index = 0; index < count; index++)
                {
                    string file = reader.ReadString();
                    string ownerId = reader.ReadString();
                    string configKey = reader.ReadString();
                    int documentLength = reader.ReadInt32();

                    if (documentLength < 0 || documentLength > MaximumDocumentBytes)
                        throw new FormatException("ConfigAPI defaults document length is outside the supported range.");

                    ConfigDocument document = ConfigDocumentBinaryCodec.Decode(reader.ReadBytes(documentLength));
                    entries[index] = new ConfigDefaultsEntry(file, new ConfigIdentity(ownerId, configKey), document);
                }

                if (!reader.IsAtEnd)
                    throw new FormatException("ConfigAPI defaults payload contains trailing data.");

                return new ConfigDefaultsStore(entries);
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (FormatException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("ConfigAPI defaults payload is malformed.", exception);
            }
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
                throw new ArgumentException("Config defaults string is not valid Unicode.", nameof(value), exception);
            }

            if (bytes.Length > MaximumStringBytes)
                throw new ArgumentException("Config defaults string exceeds the supported UTF-8 byte length.", nameof(value));

            WriteInt32(writer, bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteInt32(ByteWriter writer, int value)
        {
            uint raw = unchecked((uint)value);
            writer.WriteByte((byte)raw);
            writer.WriteByte((byte)(raw >> 8));
            writer.WriteByte((byte)(raw >> 16));
            writer.WriteByte((byte)(raw >> 24));
        }

        private sealed class ByteWriter
        {
            private readonly List<byte> _bytes = new List<byte>();

            public void WriteByte(byte value) => _bytes.Add(value);

            public void Write(byte[] values)
            {
                if (values == null)
                    throw new ArgumentNullException(nameof(values));

                for (var index = 0; index < values.Length; index++)
                    _bytes.Add(values[index]);
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
                if (payload == null)
                    throw new ArgumentNullException(nameof(payload));

                _payload = payload;
            }

            public bool IsAtEnd => _offset == _payload.Length;

            public byte ReadByte()
            {
                Require(1);
                return _payload[_offset++];
            }

            public int ReadInt32()
            {
                Require(4);
                uint value = (uint)(_payload[_offset] | _payload[_offset + 1] << 8 | _payload[_offset + 2] << 16 | _payload[_offset + 3] << 24);
                _offset += 4;
                return unchecked((int)value);
            }

            public byte[] ReadBytes(int count)
            {
                Require(count);
                var result = new byte[count];
                Buffer.BlockCopy(_payload, _offset, result, 0, count);
                _offset += count;
                return result;
            }

            public string ReadString()
            {
                int byteCount = ReadInt32();
                if (byteCount < 0 || byteCount > MaximumStringBytes)
                    throw new FormatException("ConfigAPI defaults string length is outside the supported range.");

                Require(byteCount);

                try
                {
                    string value = _strictUtf8.GetString(_payload, _offset, byteCount);
                    _offset += byteCount;
                    return value;
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FormatException("ConfigAPI defaults string is not valid UTF-8.", exception);
                }
            }

            private void Require(int count)
            {
                if (count < 0 || _offset > _payload.Length - count)
                    throw new FormatException("ConfigAPI defaults payload is truncated.");
            }
        }
    }
}