using System;
using System.Globalization;
using System.Text;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;

namespace MarcoZechner.ConfigAPI.Serialization
{
    public static class ConfigProvenanceCodec
    {
        private const string FormatPrefix = "CONFIGAPI-PROVENANCE:";
        private const string FormatHeader = "CONFIGAPI-PROVENANCE:1;";

        public static string Encode(ConfigProvenance provenance)
        {
            if (provenance == null)
                throw new ArgumentNullException(nameof(provenance));

            var result = new StringBuilder();

            result.Append(FormatHeader);
            WriteString(result, provenance.Identity.OwnerId);
            WriteString(result, provenance.Identity.ConfigKey);
            WriteNode(result, provenance.BaselineDefaults.Root);

            return result.ToString();
        }

        public static ConfigProvenance Decode(string source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!source.StartsWith(FormatHeader, StringComparison.Ordinal))
            {
                if (source.StartsWith(FormatPrefix, StringComparison.Ordinal))
                    throw new NotSupportedException("Unsupported ConfigAPI provenance format version.");

                throw new FormatException("ConfigAPI provenance header is missing or invalid.");
            }

            try
            {
                var reader = new Reader(source, FormatHeader.Length);
                var ownerId = reader.ReadString();
                var configKey = reader.ReadString();
                var root = reader.ReadNode() as ConfigObjectNode;

                if (root == null)
                    throw new FormatException("ConfigAPI provenance baseline root must be an object.");

                if (!reader.IsEnd)
                    throw new FormatException("ConfigAPI provenance contains trailing data.");

                return new ConfigProvenance(new ConfigIdentity(ownerId, configKey), new ConfigDocument(root));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("ConfigAPI provenance contains an invalid semantic value.", exception);
            }
        }

        private static void WriteNode(StringBuilder result, ConfigNode node)
        {
            if (node is ConfigNullNode)
            {
                result.Append("N;");
                return;
            }

            var scalar = node as ConfigScalarNode;
            if (scalar != null)
            {
                WriteScalar(result, scalar);
                return;
            }

            var array = node as ConfigArrayNode;
            if (array != null)
            {
                result.Append('A');
                WriteInteger(result, array.Items.Count);
                result.Append(';');

                foreach (ConfigNode item in array.Items)
                    WriteNode(result, item);

                return;
            }

            var obj = node as ConfigObjectNode;
            if (obj == null)
                throw new NotSupportedException($"Unsupported ConfigAPI provenance node type: {node.GetType().FullName}");
            
            result.Append('O');
            WriteInteger(result, obj.Entries.Count);
            result.Append(';');

            foreach (ConfigObjectEntry entry in obj.Entries)
            {
                WriteString(result, entry.Name);
                WriteNode(result, entry.Value);
            }
        }

        private static void WriteScalar(StringBuilder result, ConfigScalarNode scalar)
        {
            switch (scalar.Kind)
            {
                case ConfigScalarKind.Boolean:
                    result.Append((bool)scalar.Value ? "B1;" : "B0;");
                    return;

                case ConfigScalarKind.Integer:
                    result.Append('I');
                    WriteInteger(result, (long)scalar.Value);
                    result.Append(';');
                    return;

                case ConfigScalarKind.Float:
                {
                    var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits((double)scalar.Value));

                    result.Append('F');
                    result.Append(bits.ToString("X16", CultureInfo.InvariantCulture)); 
                    result.Append(';');
                    return;
                }

                case ConfigScalarKind.String:
                    WriteString(result, (string)scalar.Value); 
                    return;

                case ConfigScalarKind.LocalDate:
                    WriteLocalDate(result, (ConfigLocalDate)scalar.Value);
                    return;

                case ConfigScalarKind.LocalTime:
                    WriteLocalTime(result, (ConfigLocalTime)scalar.Value);
                    return;

                case ConfigScalarKind.LocalDateTime:
                {
                    var value = (ConfigLocalDateTime)scalar.Value;

                    result.Append('L');
                    WriteLocalDate(result, value.Date);
                    WriteLocalTime(result, value.Time);
                    return;
                }

                case ConfigScalarKind.OffsetDateTime:
                {
                    var value = (ConfigOffsetDateTime)scalar.Value;

                    result.Append('Z');
                    WriteLocalDate(result, value.Date);
                    WriteLocalTime(result, value.Time);

                    result.Append('M');
                    WriteInteger(result, value.OffsetMinutes);
                    result.Append(';');

                    result.Append(value.IsUnknownLocalOffset ? "U1;" : "U0;");
                    return;
                }

                default:
                    throw new NotSupportedException($"Unsupported ConfigAPI scalar kind: {scalar.Kind}");
            }
        }

        private static void WriteLocalDate(StringBuilder result, ConfigLocalDate value)
        {
            result.Append('D');

            WriteInteger(result, value.Year); 
            result.Append(',');

            WriteInteger(result, value.Month);
            result.Append(',');

            WriteInteger(result, value.Day);
            result.Append(';');
        }

        private static void WriteLocalTime(StringBuilder result, ConfigLocalTime value)
        {
            result.Append('T');

            WriteInteger(result, value.Hour);
            result.Append(',');

            WriteInteger(result, value.Minute);
            result.Append(',');

            WriteInteger(result, value.Second);
            result.Append(',');

            WriteRawString(result, value.FractionalSeconds);
        }

        private static void WriteString(StringBuilder result, string value)
        {
            result.Append('S');
            WriteRawString(result, value);
        }

        private static void WriteRawString(StringBuilder result, string value) 
        {
            WriteInteger(result, value.Length);
            result.Append(':');
            result.Append(value);
        }

        private static void WriteInteger(StringBuilder result, long value) 
            => result.Append(value.ToString(CultureInfo.InvariantCulture));

        private sealed class Reader
        {
            private readonly string _source;
            private int _index;

            public bool IsEnd => _index == _source.Length;

            public Reader(string source, int startIndex)
            {
                _source = source;
                _index = startIndex;
            }

            public string ReadString()
            {
                Expect('S');
                return ReadRawString();
            }

            public ConfigNode ReadNode()
            {
                char kind = ReadCharacter();

                switch (kind)
                {
                    case 'N':
                        Expect(';');
                        return ConfigNullNode.Instance;

                    case 'B':
                        return ConfigScalarNode.Boolean(
                            ReadBoolean());

                    case 'I':
                        return ConfigScalarNode.Integer(
                            ReadInt64(';'));

                    case 'F':
                        return ConfigScalarNode.Float(
                            ReadFloat());

                    case 'S':
                        return ConfigScalarNode.String(
                            ReadRawString());

                    case 'D':
                        return ConfigScalarNode.LocalDate(
                            ReadLocalDate());

                    case 'T':
                        return ConfigScalarNode.LocalTime(
                            ReadLocalTime());

                    case 'L':
                        return ConfigScalarNode.LocalDateTime(
                            new ConfigLocalDateTime(ReadTaggedLocalDate(), ReadTaggedLocalTime()));

                    case 'Z':
                        return ReadOffsetDateTime();

                    case 'A':
                        return ReadArray();

                    case 'O':
                        return ReadObject();

                    default:
                        throw new FormatException("Unknown ConfigAPI provenance node tag.");
                }
            }

            private bool ReadBoolean()
            {
                char value = ReadCharacter();

                if (value != '0' && value != '1')
                    throw new FormatException("Boolean provenance value must be 0 or 1.");

                Expect(';');
                return value == '1';
            }

            private double ReadFloat()
            {
                string text = ReadUntil(';');

                if (text.Length != 16)
                    throw new FormatException("Float provenance value must contain exactly 16 hexadecimal digits.");

                ulong bits;

                if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bits))
                    throw new FormatException("Float provenance value contains invalid hexadecimal digits.");

                return BitConverter.Int64BitsToDouble(unchecked((long)bits));
            }

            private ConfigLocalDate ReadTaggedLocalDate()
            {
                Expect('D');
                return ReadLocalDate();
            }

            private ConfigLocalDate ReadLocalDate()
            {
                var year = ReadInt32(',');
                var month = ReadInt32(',');
                var day = ReadInt32(';');

                return new ConfigLocalDate(year, month, day);
            }

            private ConfigLocalTime ReadTaggedLocalTime()
            {
                Expect('T');
                return ReadLocalTime();
            }

            private ConfigLocalTime ReadLocalTime()
            {
                var hour = ReadInt32(',');
                var minute = ReadInt32(',');
                var second = ReadInt32(',');
                var fractionalSeconds = ReadRawString();

                return new ConfigLocalTime(hour, minute, second, fractionalSeconds);
            }

            private ConfigNode ReadOffsetDateTime()
            {
                ConfigLocalDate date = ReadTaggedLocalDate();
                ConfigLocalTime time = ReadTaggedLocalTime();

                Expect('M');
                var offsetMinutes = ReadInt32(';');

                Expect('U');
                var unknown = ReadBoolean();

                return ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(date, time, offsetMinutes, unknown));
            }

            private ConfigArrayNode ReadArray()
            {
                var count = ReadCount();
                var items = new ConfigNode[count];

                for (var i = 0; i < count; i++)
                    items[i] = ReadNode();

                return new ConfigArrayNode(items);
            }

            private ConfigObjectNode ReadObject()
            {
                var count = ReadCount();
                var entries = new ConfigObjectEntry[count];

                for (var i = 0; i < count; i++)
                    entries[i] = new ConfigObjectEntry(ReadString(), ReadNode());

                return new ConfigObjectNode(entries);
            }

            private int ReadCount()
            {
                var count = ReadInt32(';');

                if (count < 0)
                    throw new FormatException("Collection provenance count must not be negative.");

                if (count > _source.Length - _index)
                    throw new FormatException("Collection provenance count exceeds the remaining source.");

                return count;
            }

            private string ReadRawString()
            {
                var length = ReadInt32(':');

                if (length < 0)
                    throw new FormatException("String provenance length must not be negative.");

                if (length > _source.Length - _index)
                    throw new FormatException("String provenance length exceeds the remaining source.");

                var result = _source.Substring(_index, length);

                _index += length;
                return result;
            }

            private int ReadInt32(char terminator)
            {
                string text = ReadUntil(terminator);

                int value;

                if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    throw new FormatException("Invalid 32-bit integer provenance value.");

                return value;
            }

            private long ReadInt64(char terminator)
            {
                string text = ReadUntil(terminator);

                long value;

                if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    throw new FormatException("Invalid 64-bit integer provenance value.");

                return value;
            }

            private string ReadUntil(char terminator)
            {
                var start = _index;

                while (_index < _source.Length && _source[_index] != terminator)
                    _index++;

                if (_index >= _source.Length)
                    throw new FormatException("Expected provenance delimiter was not found.");

                if (_index == start)
                    throw new FormatException("Provenance numeric token must not be empty.");

                var result = _source.Substring(start, _index - start);

                _index++;
                return result;
            }

            private char ReadCharacter()
            {
                if (_index >= _source.Length)
                    throw new FormatException("Unexpected end of ConfigAPI provenance.");

                return _source[_index++];
            }

            private void Expect(char expected)
            {
                var actual = ReadCharacter();

                if (actual != expected)
                    throw new FormatException("Unexpected ConfigAPI provenance token.");
            }
        }
    }
}
