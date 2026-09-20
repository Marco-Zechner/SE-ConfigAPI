# Mz.SemanticVersioning

`Mz.SemanticVersioning` provides immutable semantic-version and package
changelog value objects for Space Engineers mod source projects.

The current implementation supports exact numeric `major.minor.patch`
versions. It intentionally does not support prerelease labels or build
metadata.

## Install

### Install with SELibs

[SELibs](https://github.com/Marco-Zechner/selibs) is a source-library manager
for Space Engineers mods. Its repository contains the installer, command
reference, and project setup instructions.

After installing SELibs, run these commands from the root of the mod project:

```shell
    selibs init
    selibs add Mz.SemanticVersioning@0.1.1
```

Skip `selibs init` when the project already contains `selibs.json`.

SELibs copies the package into the configured `Libraries` directory and records
the exact installed version and file checksums. Inspect that state with:

```shell
    selibs status
```

### Install manually

To install without SELibs, use the source from the matching release tag in this
repository and copy this complete folder:

```text
    src/Mz.SemanticVersioning
```

Place it under the mod's script library directory, for example:

```text
    Data/Scripts/ExampleMod/Libraries/Mz.SemanticVersioning
```

Keep the folder structure intact and compile all contained `.cs` files as part
of the mod. Do not combine files from different release tags.

## Create and parse versions

```csharp
    using Mz.SemanticVersioning;

    var explicitVersion = new SemanticVersion(1, 2, 3);
    var parsedVersion = SemanticVersion.Parse("1.2.3");

    SemanticVersion optionalVersion;

    if (SemanticVersion.TryParse(userInput, out optionalVersion))
    {
        // optionalVersion is safe to use.
    }
```

`Parse` throws when the input is not an exact three-component numeric version.
`TryParse` returns `false` instead.

Input whitespace is ignored. Numeric components are normalized when converted
back to text, so `01.002.0003` becomes `1.2.3`.

## Compare versions

`SemanticVersion` implements `IComparable<SemanticVersion>` and supplies the
standard comparison operators:

```csharp
    var current = new SemanticVersion(1, 4, 0);
    var required = new SemanticVersion(1, 2, 0);
    var nextMajor = new SemanticVersion(2, 0, 0);

    bool accepted = current >= required && current < nextMajor;
```

Equality compares the three numeric components:

```csharp
    bool same = SemanticVersion.Parse("1.2.3") == new SemanticVersion(1, 2, 3);
```

## Define package changelogs

`ChangelogEntry` stores one semantic version and a copied, immutable list of
changes. `Changelog` validates that the complete history is unique, ordered
from newest to oldest, and begins with the current package version:

```csharp
    var changelog = new Changelog(
        "1.2.0",
        new[]
        {
            new ChangelogEntry(
                "1.2.0",
                new[]
                {
                    "Added shared package metadata."
                }
            ),
            new ChangelogEntry(
                "1.1.0",
                new[]
                {
                    "Published the previous release."
                }
            )
        }
    );

    SemanticVersion currentVersion = changelog.CurrentVersion;
    ChangelogEntry currentChanges = changelog.Current;
    ChangelogEntry[] completeHistory = changelog.Entries;
```

Returned entry and change arrays are copies. Modifying those arrays does not
modify the stored changelog.

## Package version

The released package version is available through:

```csharp
    string packageVersion = Mz.SemanticVersioning.LibraryVersionFile.VersionString;
```

This value describes the embedded library package, not a mod's own version.
