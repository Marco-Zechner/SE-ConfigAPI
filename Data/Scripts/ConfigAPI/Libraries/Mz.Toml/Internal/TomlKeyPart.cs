namespace Mz.Toml.Internal
{
    internal sealed class TomlKeyPart
    {
        public TomlKeyPart(string value, int line, int column)
        {
            Value = value;
            Line = line;
            Column = column;
        }

        public string Value { get; }

        public int Line { get; }

        public int Column { get; }
    }
}
