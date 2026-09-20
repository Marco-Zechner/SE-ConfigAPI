using System;
using System.Globalization;
using System.Text;

namespace Mz.Toml.Internal
{
    internal static partial class TomlWriter
    {
        private static void AppendNode(StringBuilder sb, TomlNode node)
        {
            switch (node.Kind)
            {
                case TomlNodeKind.Value:
                    AppendValue(sb, (TomlValue)node);
                    return;

                case TomlNodeKind.Array:
                    AppendArray(sb, (TomlArray)node);
                    return;

                case TomlNodeKind.Table:
                    AppendInlineTable(sb, (TomlTable)node);
                    return;

                default:
                    throw new InvalidOperationException("Unsupported TOML node kind: " + node.Kind);
            }
        }

        private static void AppendArray(StringBuilder sb, TomlArray array)
        {
            sb.Append('[');

            for (var i = 0; i < array.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                AppendNode(sb, array[i]);
            }

            sb.Append(']');
        }

        private static void AppendInlineTable(StringBuilder sb, TomlTable table)
        {
            sb.Append('{');

            var first = true;

            foreach (var pair in table)
            {
                if (!first)
                    sb.Append(", ");

                AppendKey(sb, pair.Key);
                sb.Append(" = ");
                AppendNode(sb, pair.Value);

                first = false;
            }

            sb.Append('}');
        }

        private static void AppendValue(StringBuilder sb, TomlValue value)
        {
            switch (value.ValueKind)
            {
                case TomlValueKind.String:
                    AppendBasicString(sb, value.AsString());
                    return;

                case TomlValueKind.Integer:
                    sb.Append(value.AsInteger().ToString(CultureInfo.InvariantCulture));
                    return;

                case TomlValueKind.Float:
                {
                    var number = value.AsFloat();

                    if (double.IsNaN(number))
                    {
                        sb.Append("nan");
                        return;
                    }

                    if (double.IsPositiveInfinity(number))
                    {
                        sb.Append("inf");
                        return;
                    }

                    if (double.IsNegativeInfinity(number))
                    {
                        sb.Append("-inf");
                        return;
                    }

                    if (number == 0.0 && double.IsNegativeInfinity(1.0 / number))
                    {
                        sb.Append("-0.0");
                        return;
                    }

                    var text = number.ToString("R", CultureInfo.InvariantCulture);

                    if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0 && text.IndexOf('E') < 0)
                        text += ".0";

                    sb.Append(text);
                    return;
                }

                case TomlValueKind.Boolean:
                    sb.Append(value.AsBoolean() ? "true" : "false");
                    return;

                case TomlValueKind.OffsetDateTime:
                    sb.Append(value.AsOffsetDateTime());
                    return;

                case TomlValueKind.LocalDateTime:
                    sb.Append(value.AsLocalDateTime());
                    return;

                case TomlValueKind.LocalDate:
                    sb.Append(value.AsLocalDate());
                    return;

                case TomlValueKind.LocalTime:
                    sb.Append(value.AsLocalTime());
                    return;

                default:
                    throw new InvalidOperationException("Unsupported TOML scalar kind: " + value.ValueKind);
            }
        }
    }
}
