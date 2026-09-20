using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public static class ConfigDocumentWireCodec
    {
        public static object Encode(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return EncodeValue(document.Root);
        }

        public static ConfigDocument Decode(object payload)
        {
            ConfigValue root = DecodeValue(payload);

            if (root.Kind != ConfigValueKind.Object)
                throw new ArgumentException("Config document root must be an Object node.", nameof(payload));

            var entries = new ConfigEntry[root.Entries.Count];

            for (var i = 0; i < entries.Length; i++)
                entries[i] = root.Entries[i];

            return new ConfigDocument(entries);
        }

        private static object EncodeValue(ConfigValue value)
        {
            switch (value.Kind)
            {
                case ConfigValueKind.Null:
                    return KindOnly("Null");

                case ConfigValueKind.Boolean:
                    return Scalar("Boolean", value.ScalarValue);

                case ConfigValueKind.Integer:
                    return Scalar("Integer", value.ScalarValue);

                case ConfigValueKind.Float:
                    return Scalar("Float", value.ScalarValue);

                case ConfigValueKind.String:
                    return Scalar("String", value.ScalarValue);

                case ConfigValueKind.Object:
                    return EncodeObject(value);

                case ConfigValueKind.Array:
                    return EncodeArray(value);

                case ConfigValueKind.OffsetDateTime:
                    return EncodeOffsetDateTime((ConfigOffsetDateTime)value.ScalarValue);

                case ConfigValueKind.LocalDateTime:
                    return EncodeLocalDateTime((ConfigLocalDateTime)value.ScalarValue);

                case ConfigValueKind.LocalDate:
                    return EncodeLocalDate((ConfigDate)value.ScalarValue);

                case ConfigValueKind.LocalTime:
                    return EncodeLocalTime((ConfigTime)value.ScalarValue);

                default:
                    throw new ArgumentException($"Unsupported consumer config value kind: {value.Kind}", nameof(value));
            }
        }

        private static object EncodeObject(ConfigValue value)
        {
            var entries = new object[value.Entries.Count];

            for (var i = 0; i < entries.Length; i++)
                entries[i] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "Name", value.Entries[i].Name },
                    { "Value", EncodeValue(value.Entries[i].Value) },
                };

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "Object" },
                { "Entries", entries },
            };
        }

        private static object EncodeArray(ConfigValue value)
        {
            var items = new object[value.Items.Count];

            for (var i = 0; i < items.Length; i++)
                items[i] = EncodeValue(value.Items[i]);

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "Array" },
                { "Items", items },
            };
        }

        private static object EncodeOffsetDateTime(ConfigOffsetDateTime value)
        {
            var payload = EncodeDateTime("OffsetDateTime", value.Date, value.Time);

            payload.Add("OffsetMinutes", value.OffsetMinutes);

            payload.Add("IsUnknownLocalOffset", value.IsUnknownLocalOffset);

            return payload;
        }

        private static object EncodeLocalDateTime(ConfigLocalDateTime value) 
            => EncodeDateTime("LocalDateTime", value.Date, value.Time);

        private static object EncodeLocalDate(ConfigDate value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "LocalDate" },
                { "Year", value.Year },
                { "Month", value.Month },
                { "Day", value.Day },
            };

        private static object EncodeLocalTime(ConfigTime value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", "LocalTime" },
                { "Hour", value.Hour },
                { "Minute", value.Minute },
                { "Second", value.Second },
                { "FractionalSeconds", value.FractionalSeconds },
            };

        private static Dictionary<string, object> EncodeDateTime(string kind, ConfigDate date, ConfigTime time) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", kind },
                { "Year", date.Year },
                { "Month", date.Month },
                { "Day", date.Day },
                { "Hour", time.Hour },
                { "Minute", time.Minute },
                { "Second", time.Second },
                { "FractionalSeconds", time.FractionalSeconds },
            };

        private static Dictionary<string, object> KindOnly(string kind) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", kind },
            };

        private static Dictionary<string, object> Scalar(string kind, object value) 
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "Kind", kind },
                { "Value", value },
            };

        private static ConfigValue DecodeValue(object payload)
        {
            var values = RequireDictionary(payload);

            string kind = RequiredString(values, "Kind");

            switch (kind)
            {
                case "Null":
                    return ConfigValue.Null;

                case "Boolean":
                    return ConfigValue.Boolean(Required<bool>(values, "Value"));

                case "Integer":
                    return ConfigValue.Integer(Required<long>(values, "Value"));

                case "Float":
                    return ConfigValue.Float(Required<double>(values, "Value"));

                case "String":
                    return ConfigValue.String(RequiredString(values, "Value"));

                case "Object":
                    return DecodeObject(values);

                case "Array":
                    return DecodeArray(values);

                case "OffsetDateTime":
                    return ConfigValue.OffsetDateTime(
                        new ConfigOffsetDateTime(
                            DecodeDate(values), DecodeTime(values),
                            Required<int>(values, "OffsetMinutes"),
                            Required<bool>(values, "IsUnknownLocalOffset")
                        )
                    );

                case "LocalDateTime":
                    return ConfigValue.LocalDateTime(new ConfigLocalDateTime(DecodeDate(values), DecodeTime(values)));

                case "LocalDate":
                    return ConfigValue.LocalDate(DecodeDate(values));

                case "LocalTime":
                    return ConfigValue.LocalTime(DecodeTime(values));

                default:
                    throw new ArgumentException($"Unknown config node kind: {kind}", nameof(payload));
            }
        }

        private static ConfigValue DecodeObject(IDictionary<string, object> values)
        {
            object[] encodedEntries = RequiredArray(values, "Entries");

            var entries = new ConfigEntry[encodedEntries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = RequireDictionary(encodedEntries[i]);

                object encodedValue;

                if (!entry.TryGetValue("Value", out encodedValue))
                    throw new ArgumentException("Object entry is missing required field 'Value'.", nameof(values));

                entries[i] = new ConfigEntry(RequiredString(entry, "Name"), DecodeValue(encodedValue));
            }

            return ConfigValue.Object(entries);
        }

        private static ConfigValue DecodeArray(IDictionary<string, object> values)
        {
            object[] encodedItems = RequiredArray(values, "Items");

            var items = new ConfigValue[encodedItems.Length];

            for (var i = 0; i < items.Length; i++)
                items[i] = DecodeValue(encodedItems[i]);

            return ConfigValue.Array(items);
        }

        private static ConfigDate DecodeDate(IDictionary<string, object> values) 
            => new ConfigDate(Required<int>(values, "Year"),
                              Required<int>(values, "Month"),
                              Required<int>(values, "Day"));

        private static ConfigTime DecodeTime(IDictionary<string, object> values) 
            => new ConfigTime(Required<int>(values, "Hour"),
                              Required<int>(values, "Minute"),
                              Required<int>(values, "Second"),
                              RequiredString(values, "FractionalSeconds"));

        private static IDictionary<string, object> RequireDictionary(object payload)
        {
            var values = payload as IDictionary<string, object>;

            if (values == null)
                throw new ArgumentException("Config wire node must be an IDictionary<string, object>.", nameof(payload));

            return values;
        }

        private static object[] RequiredArray(IDictionary<string, object> values, string key)
        {
            object value;

            if (!values.TryGetValue(key, out value))
                throw new ArgumentException($"Config wire node is missing required field '{key}'.", nameof(values));

            var array = value as object[];

            if (array == null)
                throw new ArgumentException($"Config wire field '{key}' must be an object array.", nameof(values));

            return array;
        }

        private static string RequiredString(IDictionary<string, object> values, string key)
        {
            object value;

            if (!values.TryGetValue(key, out value))
                throw new ArgumentException($"Config wire node is missing required field '{key}'.", nameof(values));

            var text = value as string;

            if (text == null)
                throw new ArgumentException($"Config wire field '{key}' must be a string.", nameof(values));

            return text;
        }

        private static T Required<T>(IDictionary<string, object> values, string key)
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
