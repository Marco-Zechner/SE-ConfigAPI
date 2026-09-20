namespace Mz.Toml.Internal
{
    internal enum TomlTableDefinitionKind
    {
        Root,
        Programmatic,
        Implicit,
        Explicit,
        DottedKey,
        Inline
    }
}
