using System;
using System.Globalization;
using System.Text;

namespace Mz.Toml.Internal
{
    internal static partial class TomlWriter
    {
        private static void AppendBasicString(StringBuilder sb, string value)
        {
            sb.Append('"');

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c >= 0xD800 && c <= 0xDBFF)
                {
                    if (i + 1 >= value.Length || value[i + 1] < 0xDC00 || value[i + 1] > 0xDFFF)
                        throw new InvalidOperationException("Cannot write a TOML string containing an unpaired UTF-16 surrogate.");

                    sb.Append(c);
                    sb.Append(value[i + 1]);
                    i++;
                    continue;
                }

                if (c >= 0xDC00 && c <= 0xDFFF)
                    throw new InvalidOperationException("Cannot write a TOML string containing an unpaired UTF-16 surrogate.");

                switch (c)
                {
                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    case '"':
                        sb.Append("\\\"");
                        break;

                    case '\\':
                        sb.Append("\\\\");
                        break;

                    default:
                        if (c < 0x20 || c == 0x7F)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                            sb.Append(c);

                        break;
                }
            }

            sb.Append('"');
        }

        private static bool IsBareKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            foreach (var c in key)
            {
                if ((c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }
    }
}
