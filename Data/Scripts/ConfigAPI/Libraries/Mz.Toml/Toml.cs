using System;
using System.Text;

namespace Mz.Toml
{
    /// <summary>
    /// Provides the primary TOML parsing and writing API.
    /// </summary>
    public static class Toml
    {
        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// Parses TOML text and throws <see cref="TomlParseException"/> on failure.
        /// </summary>
        public static TomlDocument Parse(string text)
        {
            TomlParseResult result = TryParse(text);
            if (result.IsSuccess)
                return result.Document;

            throw new TomlParseException(result.Diagnostics[0]);
        }

        /// <summary>
        /// Parses TOML text without throwing for syntax errors.
        /// </summary>
        public static TomlParseResult TryParse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return Internal.TomlParser.Parse(text);
        }

        /// <summary>
        /// Parses UTF-8 encoded TOML bytes and throws
        /// <see cref="TomlParseException"/> on failure.
        /// A single UTF-8 BOM is accepted only at the start of the input.
        /// </summary>
        public static TomlDocument Parse(byte[] utf8)
        {
            TomlParseResult result = TryParse(utf8);
            if (result.IsSuccess)
                return result.Document;

            throw new TomlParseException(result.Diagnostics[0]);
        }

        /// <summary>
        /// Parses UTF-8 encoded TOML bytes without throwing for invalid
        /// encoding or TOML syntax.
        /// A single UTF-8 BOM is accepted only at the start of the input.
        /// </summary>
        public static TomlParseResult TryParse(byte[] utf8)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));

            var offset = 0;
            var count = utf8.Length;

            if (count >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF)
            {
                offset = 3;
                count -= 3;
            }

            string text;

            try
            {
                text = _strictUtf8.GetString(utf8, offset, count);
            }
            catch (DecoderFallbackException)
            {
                return new TomlParseResult(null, new[]
                {
                    new TomlDiagnostic("The TOML input is not valid UTF-8.", 1, 1, TomlDiagnosticCode.InvalidEncoding)
                });
            }

            return TryParse(text);
        }
        /// <summary>
        /// Writes a TOML document using deterministic formatting.
        /// </summary>
        public static string Write(TomlDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return Internal.TomlWriter.Write(document);
        }
    }
}
