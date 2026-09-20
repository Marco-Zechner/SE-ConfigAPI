# Mz.Toml copy-paste example

The package README explains the TOML node model, parser, diagnostics, writer,
installation, and format behavior. This guide shows a complete small typed
configuration wrapper that can be copied into a project and adapted to a real
mod.

The example deliberately keeps the application-specific configuration model
outside `Mz.Toml`. `Mz.Toml` parses and writes the format; the consuming code
decides which keys are required, which defaults exist, and which value kinds
are valid for the application.

## Setup

Install `Mz.Toml@0.2.0` in the project:

```shell
selibs add Mz.Toml@0.2.0
```

SELibs also installs the exact `Mz.SemanticVersioning` dependency required by
the package.

The configuration used by this example is:

```toml
name = "Example Mod"
enabled = true

[network]
channel = 45123
```

## Complete typed configuration wrapper

Copy this file into the consuming project and adapt the properties and keys to
the real configuration.

```csharp
using System;
using Mz.Toml;

namespace Example.Mod
{
    /// <summary>
    /// Small application-specific configuration model backed by TOML.
    ///
    /// Mz.Toml handles TOML syntax. This class owns the schema expected by the
    /// application.
    /// </summary>
    public sealed class ExampleTomlConfig
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public long NetworkChannel { get; set; }

        /// <summary>
        /// Parses TOML and maps it into the application's typed configuration.
        /// Syntax errors and schema errors are returned through <paramref name="error"/>.
        /// </summary>
        public static bool TryParse(string text, out ExampleTomlConfig config, out string error)
        {
            config = null;
            error = null;

            var result = Toml.TryParse(text);

            if (!result.IsSuccess)
            {
                error = result.Diagnostics[0].ToString();
                return false;
            }

            try
            {
                var root = result.Document.Root;
                var network = RequireTable(root, "network");

                config = new ExampleTomlConfig
                {
                    Name = RequireValue(root, "name", TomlValueKind.String).AsString(),
                    Enabled = RequireValue(root, "enabled", TomlValueKind.Boolean).AsBoolean(),
                    NetworkChannel = RequireValue(network, "channel", TomlValueKind.Integer).AsInteger()
                };

                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Writes this configuration as deterministic canonical TOML.
        /// </summary>
        public string ToToml()
        {
            var document = new TomlDocument();

            document.Root.Set("name", TomlValue.FromString(Name));
            document.Root.Set("enabled", TomlValue.FromBoolean(Enabled));

            var network = new TomlTable();
            network.Set("channel", TomlValue.FromInteger(NetworkChannel));

            document.Root.Set("network", network);

            return Toml.Write(document);
        }

        private static TomlTable RequireTable(TomlTable table, string key)
        {
            TomlNode node;

            if (!table.TryGetValue(key, out node))
                throw new FormatException($"Missing required TOML table '{key}'.");

            var nestedTable = node as TomlTable;

            if (nestedTable == null)
                throw new FormatException($"TOML key '{key}' must be a table.");

            return nestedTable;
        }

        private static TomlValue RequireValue(TomlTable table, string key, TomlValueKind expectedKind)
        {
            TomlNode node;

            if (!table.TryGetValue(key, out node))
                throw new FormatException($"Missing required TOML key '{key}'.");

            var value = node as TomlValue;

            if (value == null || value.ValueKind != expectedKind)
                throw new FormatException($"TOML key '{key}' must be {expectedKind}.");

            return value;
        }
    }
}
```

The wrapper has two separate validation layers:

1. `Toml.TryParse` validates TOML syntax. When the `byte[]` overload is used,
   it also validates the original UTF-8 encoding.
2. `ExampleTomlConfig` validates the application's expected keys and value
   kinds.

That separation is intentional. `Mz.Toml` does not know whether a particular
application requires a key named `network`, whether `channel` has a default, or
whether an integer must fit into a smaller application-specific range.

## Using the wrapper

A consuming system can load configuration without handling the raw TOML node
model throughout the rest of the application:

```csharp
ExampleTomlConfig config;
string error;

if (!ExampleTomlConfig.TryParse(text, out config, out error))
{
    Log($"Could not load configuration: {error}");
    return;
}

Log($"Loaded config for {config.Name}. Channel: {config.NetworkChannel}.");
```

When the configuration changes, serialize it back through the same wrapper:

```csharp
string toml = config.ToToml();

SaveConfiguration(toml);
```

`Toml.Write` produces deterministic canonical TOML. Repeatedly parsing and
writing the same document therefore produces stable output.

## Optional keys and defaults

Application defaults belong in the application mapping code.

For example:

```csharp
bool enabled = true;
TomlNode enabledNode;

if (root.TryGetValue("enabled", out enabledNode))
{
    var enabledValue = enabledNode as TomlValue;

    if (enabledValue == null || enabledValue.ValueKind != TomlValueKind.Boolean)
        throw new FormatException("TOML key 'enabled' must be Boolean.");

    enabled = enabledValue.AsBoolean();
}
```

A missing key and a malformed key are different situations. A missing optional
key may use a default; a present key with the wrong type should normally be
reported as invalid configuration.

## Arrays

TOML arrays are ordered and may contain heterogeneous values.

Programmatic construction:

```csharp
var ports = new TomlArray();

ports.Add(TomlValue.FromInteger(8000));
ports.Add(TomlValue.FromInteger(8001));

document.Root.Set("ports", ports);
```

Reading an application-specific integer array:

```csharp
TomlNode portsNode;

if (!document.Root.TryGetValue("ports", out portsNode))
    throw new FormatException("Missing required TOML array 'ports'.");

var ports = portsNode as TomlArray;

if (ports == null)
    throw new FormatException("TOML key 'ports' must be an array.");

for (var i = 0; i < ports.Count; i++)
{
    var value = ports[i] as TomlValue;

    if (value == null || value.ValueKind != TomlValueKind.Integer)
        throw new FormatException($"TOML array 'ports' contains an invalid value at index {i}.");

    long port = value.AsInteger();
}
```

The index is intentionally retained in this example because it is used in the
diagnostic message. When the position has no meaning, normal application code
can simply use `foreach`.

## Temporal values

TOML temporal values use dedicated `Mz.Toml` types rather than
`System.DateTime`, preserving TOML-specific details such as leap-second
spelling, arbitrary fractional-second digits, and the RFC 3339 `-00:00`
unknown-local-offset marker.

Example:

```csharp
var timestamp = new TomlOffsetDateTime(new TomlLocalDate(2026, 8, 31), new TomlLocalTime(12, 30, 0), 120);

document.Root.Set("timestamp", TomlValue.FromOffsetDateTime(timestamp));
```

Read it with:

```csharp
TomlOffsetDateTime timestamp = ((TomlValue)document.Root["timestamp"]).AsOffsetDateTime();
```

For application code that needs `System.DateTime` or another date/time model,
perform that conversion explicitly after reading the TOML value.

## Strict UTF-8 input

When the original bytes are available, prefer the byte parsing API when invalid
UTF-8 must be detected rather than silently replaced by an earlier decoder:

```csharp
byte[] bytes = LoadConfigurationBytes();

TomlParseResult result = Toml.TryParse(bytes);

if (!result.IsSuccess)
{
    TomlDiagnostic diagnostic = result.Diagnostics[0];

    Log($"TOML error at {diagnostic.Line}:{diagnostic.Column}: {diagnostic.Message}");
    return;
}
```

Malformed UTF-8 reports `TomlDiagnosticCode.InvalidEncoding`.

The string overload operates on text that has already been decoded and cannot
detect encoding damage that happened before the string reached the library.

## `Parse` versus `TryParse`

Use `Toml.Parse` when invalid TOML is exceptional and should throw:

```csharp
TomlDocument document = Toml.Parse(text);
```

A syntax failure throws `TomlParseException`. Its `Diagnostic` property
contains the stable diagnostic code, message, line, and column.

Use `Toml.TryParse` when invalid user configuration is an expected condition:

```csharp
TomlParseResult result = Toml.TryParse(text);

if (!result.IsSuccess)
    HandleDiagnostic(result.Diagnostics[0]);
```

## Preserving a hand-edited configuration

`Toml.Write` is appropriate when the application owns the whole generated
document. When users also edit the file, use the source-preserving syntax
instead so comments and formatting can remain untouched.

For example, this changes only the value source of the `enabled` assignment:

```csharp
TomlParseResult result = Toml.TryParse(text);

if (!result.IsSuccess)
{
    Log(result.Diagnostics[0].ToString());
    return;
}

TomlSourceEditor editor = result.Syntax.CreateEditor();
TomlSyntaxNode enabledAssignment = null;

for (var i = 0; i < editor.Syntax.Nodes.Count; i++)
{
    TomlSyntaxNode node = editor.Syntax.Nodes[i];

    if (node.Kind != TomlSyntaxNodeKind.Assignment)
        continue;

    string statement = editor.Source.Substring(node.Span.Start, node.Span.Length);

    if (statement.StartsWith("enabled", StringComparison.Ordinal))
    {
        enabledAssignment = node;
        break;
    }
}

if (enabledAssignment == null)
    throw new FormatException("Missing TOML assignment 'enabled'.");

editor.ReplaceAssignmentValue(enabledAssignment, "false");

SaveConfiguration(editor.Source);
```

Only the assignment's exact `ValueSpan` changes. Existing whitespace, comments,
line endings, and unrelated source remain unchanged.

The same editor also exposes:

```csharp
editor.DisableAssignment(node);
editor.EnableAssignment(node);
editor.InsertSourceBefore(node, sourceFragment);
editor.InsertSourceAfter(node, sourceFragment);
editor.InsertSourceAtStart(sourceFragment);
editor.InsertSourceAtEnd(sourceFragment);
```

A successful edit reparses the resulting text and refreshes `editor.Syntax`.
That means node references from before the edit are stale. Reacquire the node
you need from the current syntax before the next node-based operation.

If an edit would make the document invalid, the editor throws and keeps its
previous source and syntax unchanged.

## Comments and trivia

The syntax document keeps comments, whitespace, and newlines as exact source
ranges.

`TomlSyntaxTrivia.Placement` reports objective layout:

- `TopLevel` for trivia between statements;
- `WithinStatement` for trivia lexically inside a statement or value;
- `Trailing` for same-line trivia after a completed statement.

This intentionally does not decide comment ownership. A configuration host can
build its own field/comment conventions without Mz.Toml guessing whether a
user comment belongs to a specific field.

## Disabled assignments

Mz.Toml defines this source extension:

```toml
enabled = true
#!experimentalMode = "unsafe"
```

The `#!` form is parsed as `TomlSyntaxNodeKind.DisabledAssignment`. Its key and
value must still use valid assignment syntax, but the assignment does not
populate the semantic document.

Ordinary comments still begin with `#`. `#!` is an Mz.Toml extension and is not
part of standard TOML 1.0.

A host may use disabled assignments to represent an inactive optional setting,
including a null-like application state. Mz.Toml itself does not convert a
disabled assignment into a null value.

## Working with invalid user source

A failed `Toml.TryParse(string)` still has no semantic `Document`, but it can
retain an exact `Syntax` document for diagnostics or a host editor.

Safely recognized source remains classified. Any remaining source that cannot
be classified safely after the first fatal parse failure is represented by
`TomlSyntaxNodeKind.Unparsed`.

Do not assume every failed parse contains `Unparsed`: a semantic failure such as
a duplicate key can be completely classified before it is rejected.

When the `byte[]` overload rejects invalid UTF-8, no trusted decoded source
exists and `result.Syntax` is null.

`TomlSourceEditor` deliberately accepts only currently valid TOML, so recovery
or repair policy stays with the consuming application.
## Important writer behavior

`Toml.Write` is a canonical writer, not a document editor.

It preserves the TOML data represented by the document model, but it does not
preserve the parsed source's:

- comments;
- whitespace;
- original quote style;
- original numeric spelling;
- table-layout choices.

Do not round-trip a hand-edited file through the semantic document and canonical
writer if preserving its presentation is a requirement. Use
`TomlSyntaxDocument` or `TomlSourceEditor` for source-preserving changes.

The writer rejects cyclic programmatic node graphs. Reusing the same node in
multiple acyclic locations is supported.

## TOML has no null value

TOML 1.0 has no `null`.

A consuming configuration layer must decide what application `null` means. The
usual choices are:

- omit the key;
- use an application-defined sentinel value;
- reject the configuration;
- represent the concept through another explicit TOML structure.

Do not expect `Mz.Toml` to serialize a null node or null scalar.

Mz.Toml's `#!` disabled-assignment extension can be used by a consuming
configuration layer as an explicit inactive or null-like source state, but that
mapping is application policy. The semantic TOML document simply excludes the
disabled assignment.

## Source-copy use in Space Engineers

`Mz.Toml` production source targets C# 6 and is validated for Space Engineers
source-copy use.

With SELibs, install the package normally and compile the managed library
sources as part of the mod.

For a manual installation, copy the complete matching release folders:

```text
Mz.SemanticVersioning
Mz.Toml
```

Do not mix source files from different package versions.

`Mz.Toml` itself does not depend on Space Engineers APIs, so the same public API
can also be used by ordinary .NET projects.
