using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using Mz.Toml;

namespace MarcoZechner.ConfigAPI.Serialization
{
    public static class ConfigTomlDocumentCodec
    {
        public static TomlDocument ToTomlDocument(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return new TomlDocument(ToTomlTable(document.Root));
        }

        public static ConfigDocument FromTomlDocument(TomlDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return new ConfigDocument(ToConfigObject(document.Root));
        }

        private static TomlNode ToTomlNode(ConfigNode node)
        {
            if (node is ConfigNullNode)
                throw new NotSupportedException(
                    "TOML 1.0 has no null value. ConfigAPI null persistence requires the syntax-layer disabled-assignment policy.");

            var scalar = node as ConfigScalarNode;
            if (scalar != null)
                return ToTomlValue(scalar);

            var obj = node as ConfigObjectNode;
            if (obj != null)
                return ToTomlTable(obj);

            var array = node as ConfigArrayNode;
            if (array != null)
                return ToTomlArray(array);

            throw new NotSupportedException("Unsupported ConfigAPI semantic node type: " + node.GetType().FullName);
        }

        private static TomlValue ToTomlValue(ConfigScalarNode scalar)
        {
            switch (scalar.Kind)
            {
                case ConfigScalarKind.Boolean:
                    return TomlValue.FromBoolean((bool)scalar.Value);
                case ConfigScalarKind.Integer:
                    return TomlValue.FromInteger((long)scalar.Value);
                case ConfigScalarKind.Float:
                    return TomlValue.FromFloat((double)scalar.Value);
                case ConfigScalarKind.String:
                    return TomlValue.FromString((string)scalar.Value);
                case ConfigScalarKind.OffsetDateTime:
                    return TomlValue.FromOffsetDateTime(ToTomlOffsetDateTime((ConfigOffsetDateTime)scalar.Value));
                case ConfigScalarKind.LocalDateTime:
                    return TomlValue.FromLocalDateTime(ToTomlLocalDateTime((ConfigLocalDateTime)scalar.Value));
                case ConfigScalarKind.LocalDate:
                    return TomlValue.FromLocalDate(ToTomlLocalDate((ConfigLocalDate)scalar.Value));
                case ConfigScalarKind.LocalTime:
                    return TomlValue.FromLocalTime(ToTomlLocalTime((ConfigLocalTime)scalar.Value));
                default:
                    throw new NotSupportedException("Unsupported ConfigAPI scalar kind: " + scalar.Kind);
            }
        }

        private static TomlTable ToTomlTable(ConfigObjectNode obj)
        {
            var table = new TomlTable();

            foreach (ConfigObjectEntry entry in obj.Entries)
                table.Set(entry.Name, ToTomlNode(entry.Value));

            return table;
        }

        private static TomlArray ToTomlArray(ConfigArrayNode array)
        {
            var result = new TomlArray();

            foreach (ConfigNode item in array.Items)
                result.Add(ToTomlNode(item));

            return result;
        }

        private static ConfigNode ToConfigNode(TomlNode node)
        {
            switch (node.Kind)
            {
                case TomlNodeKind.Value:
                    return ToConfigScalar((TomlValue)node);
                case TomlNodeKind.Table:
                    return ToConfigObject((TomlTable)node);
                case TomlNodeKind.Array:
                    return ToConfigArray((TomlArray)node);
                default:
                    throw new NotSupportedException("Unsupported TOML node kind: " + node.Kind);
            }
        }

        private static ConfigScalarNode ToConfigScalar(TomlValue value)
        {
            switch (value.ValueKind)
            {
                case TomlValueKind.Boolean:
                    return ConfigScalarNode.Boolean(value.AsBoolean());
                case TomlValueKind.Integer:
                    return ConfigScalarNode.Integer(value.AsInteger());
                case TomlValueKind.Float:
                    return ConfigScalarNode.Float(value.AsFloat());
                case TomlValueKind.String:
                    return ConfigScalarNode.String(value.AsString());
                case TomlValueKind.OffsetDateTime:
                    return ConfigScalarNode.OffsetDateTime(ToConfigOffsetDateTime(value.AsOffsetDateTime()));
                case TomlValueKind.LocalDateTime:
                    return ConfigScalarNode.LocalDateTime(ToConfigLocalDateTime(value.AsLocalDateTime()));
                case TomlValueKind.LocalDate:
                    return ConfigScalarNode.LocalDate(ToConfigLocalDate(value.AsLocalDate()));
                case TomlValueKind.LocalTime:
                    return ConfigScalarNode.LocalTime(ToConfigLocalTime(value.AsLocalTime()));
                default:
                    throw new NotSupportedException("Unsupported TOML scalar kind: " + value.ValueKind);
            }
        }

        private static ConfigObjectNode ToConfigObject(TomlTable table)
        {
            var entries = new List<ConfigObjectEntry>();

            foreach (var pair in table)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new NotSupportedException("TOML key cannot be represented as a ConfigAPI value-path segment.");

                entries.Add(new ConfigObjectEntry(pair.Key, ToConfigNode(pair.Value)));
            }

            return new ConfigObjectNode(entries.ToArray());
        }

        private static ConfigArrayNode ToConfigArray(TomlArray array)
        {
            var items = new ConfigNode[array.Count];

            for (var i = 0; i < array.Count; i++)
                items[i] = ToConfigNode(array[i]);

            return new ConfigArrayNode(items);
        }

        private static TomlOffsetDateTime ToTomlOffsetDateTime(ConfigOffsetDateTime value) 
            => new TomlOffsetDateTime(ToTomlLocalDate(value.Date), ToTomlLocalTime(value.Time),
                                      value.OffsetMinutes, value.IsUnknownLocalOffset);

        private static TomlLocalDateTime ToTomlLocalDateTime(ConfigLocalDateTime value) 
            => new TomlLocalDateTime(ToTomlLocalDate(value.Date), ToTomlLocalTime(value.Time));

        private static TomlLocalDate ToTomlLocalDate(ConfigLocalDate value) 
            => new TomlLocalDate(value.Year, value.Month, value.Day);

        private static TomlLocalTime ToTomlLocalTime(ConfigLocalTime value) 
            => new TomlLocalTime(value.Hour, value.Minute, value.Second, value.FractionalSeconds);

        private static ConfigOffsetDateTime ToConfigOffsetDateTime(TomlOffsetDateTime value) 
            => new ConfigOffsetDateTime(ToConfigLocalDate(value.Date), ToConfigLocalTime(value.Time),
                                        value.OffsetMinutes, value.IsUnknownLocalOffset);

        private static ConfigLocalDateTime ToConfigLocalDateTime(TomlLocalDateTime value) 
            => new ConfigLocalDateTime(ToConfigLocalDate(value.Date), ToConfigLocalTime(value.Time));

        private static ConfigLocalDate ToConfigLocalDate(TomlLocalDate value) 
            => new ConfigLocalDate(value.Year, value.Month, value.Day);

        private static ConfigLocalTime ToConfigLocalTime(TomlLocalTime value) 
            => new ConfigLocalTime(value.Hour, value.Minute, value.Second, value.FractionalSeconds);
    }
}