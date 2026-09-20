using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public static class ConfigDocumentWireCodec
    {
        public static object Encode(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return EncodeNode(document.Root);
        }

        public static ConfigDocument Decode(object payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var root = DecodeNode(payload) as ConfigObjectNode;

            if (root == null)
                throw new ArgumentException("Config document root must be an Object node.", nameof(payload));

            return new ConfigDocument(root);
        }

        private static object EncodeNode(ConfigNode node)
        {
            var nullNode = node as ConfigNullNode;

            if (nullNode != null)
                return CreateKindOnly("Null");

            var scalar = node as ConfigScalarNode;

            if (scalar != null)
                return EncodeScalar(scalar);

            var obj = node as ConfigObjectNode;

            if (obj != null)
                return EncodeObject(obj);

            var array = node as ConfigArrayNode;

            if (array != null)
                return EncodeArray(array);

            throw new ArgumentException($"Unsupported config node type: {node.GetType().FullName}", nameof(node));
        }

        private static object EncodeScalar(ConfigScalarNode scalar)
        {
            switch (scalar.Kind)
            { 
                case ConfigScalarKind.Boolean:        return CreateValueNode("Boolean", scalar.Value);
                case ConfigScalarKind.Integer:        return CreateValueNode("Integer", scalar.Value);
                case ConfigScalarKind.Float:          return CreateValueNode("Float", scalar.Value);
                case ConfigScalarKind.String:         return CreateValueNode("String", scalar.Value);
                case ConfigScalarKind.OffsetDateTime: return EncodeOffsetDateTime((ConfigOffsetDateTime)scalar.Value);
                case ConfigScalarKind.LocalDateTime:  return EncodeLocalDateTime((ConfigLocalDateTime)scalar.Value);
                case ConfigScalarKind.LocalDate:      return EncodeLocalDate((ConfigLocalDate)scalar.Value);
                case ConfigScalarKind.LocalTime:      return EncodeLocalTime((ConfigLocalTime)scalar.Value);

                default:
                    throw new ArgumentException($"Unsupported config scalar kind: {scalar.Kind}", nameof(scalar));
            }
        }

        private static object EncodeObject(ConfigObjectNode obj)
        {
            var entries = new object[obj.Entries.Count];

            for (var i = 0; i < obj.Entries.Count; i++)
            {
                ConfigObjectEntry entry = obj.Entries[i];

                entries[i] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "Name", entry.Name },
                    { "Value", EncodeNode(entry.Value) },
                };
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "Object" },
                { "Entries", entries },
            };
        }

        private static object EncodeArray(ConfigArrayNode array)
        {
            var items = new object[array.Items.Count];

            for (var i = 0; i < array.Items.Count; i++)
                items[i] = EncodeNode(array.Items[i]);

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "Array" },
                { "Items", items },
            };
        }

        private static object EncodeOffsetDateTime(ConfigOffsetDateTime value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "OffsetDateTime" },
                { "Year", value.Date.Year },
                { "Month", value.Date.Month },
                { "Day", value.Date.Day },
                { "Hour", value.Time.Hour },
                { "Minute", value.Time.Minute },
                { "Second", value.Time.Second },
                { "FractionalSeconds", value.Time.FractionalSeconds }, 
                { "OffsetMinutes", value.OffsetMinutes },
                { "IsUnknownLocalOffset", value.IsUnknownLocalOffset },
            };

        private static object EncodeLocalDateTime(ConfigLocalDateTime value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "LocalDateTime" },
                { "Year", value.Date.Year },
                { "Month", value.Date.Month },
                { "Day", value.Date.Day },
                { "Hour", value.Time.Hour },
                { "Minute", value.Time.Minute },
                { "Second", value.Time.Second },
                { "FractionalSeconds", value.Time.FractionalSeconds },
            };

        private static object EncodeLocalDate(ConfigLocalDate value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "LocalDate" },
                { "Year", value.Year },
                { "Month", value.Month },
                { "Day", value.Day },
            };

        private static object EncodeLocalTime(ConfigLocalTime value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "LocalTime" },
                { "Hour", value.Hour },
                { "Minute", value.Minute },
                { "Second", value.Second },
                { "FractionalSeconds", value.FractionalSeconds },
            };

        private static object CreateKindOnly(string kind) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", kind },
            };

        private static object CreateValueNode(string kind, object value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", kind },
                { "Value", value },
            };

        private static ConfigNode DecodeNode(object payload)
        {
            var values = RequireDictionary(payload);

            string kind = ReadRequiredString(values, "Kind");

            switch (kind)
            {
                case "Null":
                    return ConfigNullNode.Instance;
                case "Boolean":
                    return ConfigScalarNode.Boolean(ReadRequiredValue<bool>(values, "Value"));
                case "Integer":
                    return ConfigScalarNode.Integer(ReadRequiredValue<long>(values, "Value"));
                case "Float":
                    return ConfigScalarNode.Float(ReadRequiredValue<double>(values, "Value"));
                case "String":
                    return ConfigScalarNode.String(ReadRequiredString(values, "Value"));
                case "Object":
                    return DecodeObject(values);
                case "Array":
                    return DecodeArray(values);
                case "OffsetDateTime":
                    return ConfigScalarNode.OffsetDateTime(DecodeOffsetDateTime(values));
                case "LocalDateTime":
                    return ConfigScalarNode.LocalDateTime(DecodeLocalDateTime(values));
                case "LocalDate":
                    return ConfigScalarNode.LocalDate(DecodeLocalDate(values));
                case "LocalTime":
                    return ConfigScalarNode.LocalTime(DecodeLocalTime(values));
                default:
                    throw new ArgumentException($"Unknown config node kind: {kind}", nameof(payload));
            }
        }

        private static ConfigObjectNode DecodeObject(IDictionary<string, object> values)
        {
            object[] encodedEntries = ReadRequiredArray(values, "Entries");

            var entries = new ConfigObjectEntry[encodedEntries.Length];

            for (var i = 0; i < encodedEntries.Length; i++)
            {
                var encodedEntry = RequireDictionary(encodedEntries[i]);

                string name = ReadRequiredString(encodedEntry, "Name");

                object encodedValue;

                if (!encodedEntry.TryGetValue("Value", out encodedValue))
                    throw new ArgumentException("Object entry is missing required field 'Value'.", nameof(values));

                entries[i] = new ConfigObjectEntry(name, DecodeNode(encodedValue));
            }

            return new ConfigObjectNode(entries);
        }

        private static ConfigArrayNode DecodeArray(IDictionary<string, object> values)
        {
            object[] encodedItems = ReadRequiredArray(values, "Items");

            var items = new ConfigNode[encodedItems.Length];

            for (var i = 0; i < encodedItems.Length; i++)
                items[i] = DecodeNode(encodedItems[i]);

            return new ConfigArrayNode(items);
        }

        private static ConfigOffsetDateTime DecodeOffsetDateTime(IDictionary<string, object> values) 
            => new ConfigOffsetDateTime(DecodeDate(values), DecodeTime(values),
                                        ReadRequiredValue<int>(values, "OffsetMinutes"),
                                        ReadRequiredValue<bool>(values, "IsUnknownLocalOffset")
                                        );

        private static ConfigLocalDateTime DecodeLocalDateTime(IDictionary<string, object> values) 
            => new ConfigLocalDateTime(DecodeDate(values), DecodeTime(values));

        private static ConfigLocalDate DecodeLocalDate(IDictionary<string, object> values) 
            => DecodeDate(values);

        private static ConfigLocalTime DecodeLocalTime(IDictionary<string, object> values) 
            => DecodeTime(values);

        private static ConfigLocalDate DecodeDate(IDictionary<string, object> values) 
            => new ConfigLocalDate(ReadRequiredValue<int>(values, "Year"),
                                   ReadRequiredValue<int>(values, "Month"),
                                   ReadRequiredValue<int>(values, "Day")
                                   );

        private static ConfigLocalTime DecodeTime(IDictionary<string, object> values) 
            => new ConfigLocalTime(ReadRequiredValue<int>(values, "Hour"),
                                   ReadRequiredValue<int>(values, "Minute"), 
                                   ReadRequiredValue<int>(values, "Second"),
                                   ReadRequiredString(values, "FractionalSeconds")
                                   );

        private static IDictionary<string, object> RequireDictionary(object payload)
        {
            var values = payload as IDictionary<string, object>;

            if (values == null)
                throw new ArgumentException("Config wire node must be an IDictionary<string, object>.", nameof(payload));

            return values;
        }

        private static object[] ReadRequiredArray(IDictionary<string, object> values, string key)
        {
            object value;

            if (!values.TryGetValue(key, out value))
                throw new ArgumentException($"Config wire node is missing required field '{key}'.", nameof(values));

            var array = value as object[];

            if (array == null)
                throw new ArgumentException($"Config wire field '{key}' must be an object array.", nameof(values));

            return array;
        }

        private static string ReadRequiredString(IDictionary<string, object> values, string key)
        {
            object value;

            if (!values.TryGetValue(key, out value)) 
                throw new ArgumentException($"Config wire node is missing required field '{key}'.", nameof(values));

            var text = value as string;

            if (text == null)
                throw new ArgumentException($"Config wire field '{key}' must be a string.", nameof(values));

            return text;
        }

        private static T ReadRequiredValue<T>(IDictionary<string, object> values, string key)
        {
            object value;

            if (!values.TryGetValue(key, out value))
                throw new ArgumentException($"Config wire node is missing required field '{key}'.", nameof(values));

            if (!( value is T ))
                throw new ArgumentException($"Config wire field '{key}' has the wrong type.", nameof(values));

            return (T)value;
        }
    }
}
