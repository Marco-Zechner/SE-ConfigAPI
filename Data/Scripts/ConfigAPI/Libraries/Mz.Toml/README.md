# Mz.Toml

`Mz.Toml` is a strict TOML 1.0 parser, document model, deterministic canonical
writer, and source-preserving syntax/editor library designed for source-copy use
in Space Engineers mods and ordinary .NET projects.

The package has an exact dependency on `Mz.SemanticVersioning` `0.1.1`,
matching the shared version and changelog model used by the other libraries in
this repository.

The library supports:

- TOML 1.0 keys, tables, dotted keys, and arrays of tables;
- basic, literal, multiline basic, and multiline literal strings;
- integers, floating-point values, Booleans, arrays, and inline tables;
- offset date-times, local date-times, local dates, and local times;
- strict UTF-8 byte parsing with BOM handling;
- deterministic canonical writing;
- exact source-preserving syntax nodes and character spans;
- preserved whitespace, newlines, and comments with objective placement data;
- the explicit Mz.Toml `#!` disabled-assignment extension;
- source-preserving enable, disable, value replacement, and source insertion;
- composable validated edits through `TomlSourceEditor`;
- recoverable syntax information for failed decoded-text parses;
- stable line and column diagnostics for parse failures.

`Toml.Write` remains a canonical semantic writer. It intentionally does not
preserve presentation details from parsed input. Use the syntax and source
editing APIs when comments, whitespace, spelling, and layout must remain intact.

## Install

### Install with SELibs

[SELibs](https://github.com/Marco-Zechner/selibs) is a source-library manager
for Space Engineers mods.

From the root of a mod project:

```shell
selibs init
selibs add Mz.Toml@0.2.0
```

Skip `selibs init` when the project already contains `selibs.json`.

SELibs installs `Mz.Toml` and its exact `Mz.SemanticVersioning` dependency.
It records the installed versions and managed file checksums.

Inspect installed state with:

```shell
selibs status
```

### Install manually

Use source from the matching release tags and copy both complete folders:

```text
src/Mz.SemanticVersioning
src/Mz.Toml
```

Place them as sibling folders under the mod's script library directory:

```text
Data/Scripts/ExampleMod/Libraries/Mz.SemanticVersioning
Data/Scripts/ExampleMod/Libraries/Mz.Toml
```

For `Mz.Toml` `0.2.0`, use `Mz.SemanticVersioning` `0.1.1`.

Keep both folder structures intact and compile every contained `.cs` file as
part of the mod. Do not combine source files from different release versions.

## Parse TOML

Use `Toml.Parse` when invalid TOML should throw:

```csharp
using Mz.Toml;

TomlDocument document = Toml.Parse(
    "title = \"Example\"\n" +
    "enabled = true\n"
);

string title = ((TomlValue)document.Root["title"]).AsString();

bool enabled = ((TomlValue)document.Root["enabled"]).AsBoolean();
```

Use `Toml.TryParse` when diagnostics should be handled without an exception:

```csharp
TomlParseResult result = Toml.TryParse(text);

if (!result.IsSuccess)
{
    TomlDiagnostic diagnostic = result.Diagnostics[0];

    Log($"{diagnostic.Line}:{diagnostic.Column} {diagnostic.Message}");
    return;
}

TomlDocument document = result.Document;
```

Diagnostics include a stable diagnostic code plus one-based line and column
information.

## Parse strict UTF-8 bytes

The byte API validates the original UTF-8 encoding before parsing TOML:

```csharp
byte[] bytes = LoadTomlBytes();

TomlParseResult result = Toml.TryParse(bytes);

if (!result.IsSuccess)
{
    HandleTomlFailure(result.Diagnostics[0]);
    return;
}

TomlDocument document = result.Document;
```

Malformed UTF-8 produces `TomlDiagnosticCode.InvalidEncoding`.

One UTF-8 BOM is accepted only at the beginning of the input. The string API
is intentionally an already-decoded Unicode API and therefore cannot detect
decoding errors that occurred before the string reached `Mz.Toml`.

## Inspect exact source syntax

Successful string parses expose both the semantic `Document` and an exact
source-preserving `Syntax` document:

```csharp
TomlParseResult result = Toml.TryParse(text);

if (!result.IsSuccess)
{
    HandleTomlFailure(result.Diagnostics[0]);
    return;
}

TomlSyntaxDocument syntax = result.Syntax;
string exactSource = syntax.Source;
```

`TomlSyntaxDocument.Source` is the exact decoded input string. `Nodes` contains
ordered top-level source ranges such as assignments, table headers, comments,
whitespace, and newlines. `TomlSourceSpan` uses zero-based half-open character
ranges: `Start` is inclusive and `End` is exclusive.

Assignments expose `ValueSpan`, allowing a host to identify only the original
value spelling without losing the surrounding key, whitespace, or trailing
comment.

`Trivia` separately reports whitespace, newlines, and comments. Trivia may
overlap a larger statement node, for example whitespace inside an array.
`TomlSyntaxTriviaPlacement` objectively classifies trivia as:

- `TopLevel` between top-level statements;
- `WithinStatement` lexically inside a statement or value;
- `Trailing` on the same line after a completed statement.

Placement describes source layout only. `Mz.Toml` does not decide that a
particular comment "belongs" to a semantic field.

## Disabled assignments

Mz.Toml 0.2.0 adds an explicit source extension for disabled assignments:

```toml
enabled = true
#!experimental = "off"
```

`#!` at the beginning of a top-level assignment disables that assignment.
The remainder must still be a valid assignment, but the disabled value is not
added to the semantic `TomlDocument`.

This syntax is an **Mz.Toml extension**, not TOML 1.0 syntax. Ordinary `#`
comments remain ordinary comments. A malformed disabled assignment reports
`TomlDiagnosticCode.InvalidDisabledAssignment`.

Hosts may choose to interpret a disabled assignment as an optional, inactive,
or null-like configuration field, but `Mz.Toml` itself does not assign such
application semantics.

## Preserve source while editing

`TomlSyntaxDocument` provides immutable one-shot source editing methods:

- `DisableAssignment`;
- `EnableAssignment`;
- `ReplaceAssignmentValue`;
- `InsertSourceBefore`;
- `InsertSourceAfter`;
- `InsertSourceAtStart`;
- `InsertSourceAtEnd`.

These methods return a new source string and leave the syntax document
unchanged.

For multiple edits, create a `TomlSourceEditor`:

```csharp
TomlParseResult result = Toml.TryParse(text);

if (!result.IsSuccess)
    throw new InvalidOperationException(result.Diagnostics[0].ToString());

TomlSourceEditor editor = result.Syntax.CreateEditor();

TomlSyntaxNode assignment = null;

for (var i = 0; i < editor.Syntax.Nodes.Count; i++)
{
    TomlSyntaxNode node = editor.Syntax.Nodes[i];

    if (node.Kind == TomlSyntaxNodeKind.Assignment)
    {
        assignment = node;
        break;
    }
}

if (assignment == null)
    throw new InvalidOperationException("No assignment was found.");

editor.ReplaceAssignmentValue(assignment, "42");

string editedSource = editor.Source;
```

Every successful editor operation reparses the complete resulting source and
replaces `editor.Syntax`. Node objects from an earlier editor state are
therefore stale and are rejected; reacquire nodes from the current
`editor.Syntax` before the next node-based edit.

Edits are atomic. If an operation would make the resulting TOML invalid, the
operation throws and the editor keeps its previous `Source` and `Syntax`.

Insertion fragments must themselves be non-empty valid TOML and must also
produce a valid complete document at the requested insertion point.

## Syntax retained on failed decoded text

`Toml.TryParse(string)` remains semantically strict: a failed parse has
`IsSuccess == false` and `Document == null`.

For decoded string input, the result can still expose `Syntax`. Source that was
safely recognized before failure remains classified. When parsing stops before
the remaining source can be classified safely, a `TomlSyntaxNodeKind.Unparsed`
node preserves that remainder exactly.

A semantic failure can be fully classified without an `Unparsed` node. For
example, duplicate keys may be syntactically recognized completely while still
making the parse fail semantically.

The strict byte API is different when UTF-8 decoding itself fails. In that
case there is no trusted exact decoded string, so `Syntax` is null and the
diagnostic is `TomlDiagnosticCode.InvalidEncoding`.

`CreateEditor()` requires currently valid TOML and rejects syntax documents
retained from failed parses.
## Build a document programmatically

Create nodes directly when TOML is being generated rather than parsed:

```csharp
var document = new TomlDocument();

document.Root.Set("title", TomlValue.FromString("Example"));

document.Root.Set("enabled", TomlValue.FromBoolean(true));

var ports = new TomlArray();

ports.Add(TomlValue.FromInteger(8000));
ports.Add(TomlValue.FromInteger(8001));

document.Root.Set("ports", ports);

string text = Toml.Write(document);
```

Tables preserve insertion order. Arrays may contain heterogeneous TOML value
kinds, as permitted by TOML 1.0.

## Read values

Every table entry is a `TomlNode`.

Inspect `TomlNode.Kind` or cast a node to its concrete type:

```csharp
TomlNode node = document.Root["title"];

if (node.Kind == TomlNodeKind.Value)
{
    TomlValue value = (TomlValue)node;

    if (value.ValueKind == TomlValueKind.String)
    {
        string title = value.AsString();
    }
}
```

`TomlValue` supplies typed accessors for strings, integers, floats, Booleans,
offset date-times, local date-times, local dates, and local times.

Calling an accessor for the wrong `TomlValueKind` throws
`InvalidOperationException`.

## Write canonical TOML

`Toml.Write` produces deterministic TOML:

```csharp
string canonicalToml = Toml.Write(document);
```

The output is intended for configuration persistence and generated documents.
It does not preserve comments, whitespace choices, quoting choices, or other
presentation details from parsed input.

Cyclic programmatic node graphs are rejected. Reusing the same node in multiple
acyclic locations is supported.

## TOML behavior

`Mz.Toml` targets TOML 1.0.

Notable format rules include:

- TOML has no `null` value;
- arrays may contain heterogeneous values;
- inline tables and arrays are represented through the same public node model;
- arrays of tables are exposed as arrays containing tables;
- temporal values use dedicated `Mz.Toml` value types so TOML edge cases are
  not lost through `System.DateTime`.

During development, the library was checked against the complete pinned TOML
1.0 `toml-test` fixture manifest:

- 210 valid fixtures accepted;
- 501 invalid fixtures rejected.

## Space Engineers compatibility

Production source targets C# 6 and is permanently source-copy validated against
the Space Engineers mod analyzer environment.

Neither `Mz.Toml` nor `Mz.SemanticVersioning` depends on Space Engineers APIs.
Both can therefore be used by ordinary .NET code and copied directly into a
mod's script source tree.
