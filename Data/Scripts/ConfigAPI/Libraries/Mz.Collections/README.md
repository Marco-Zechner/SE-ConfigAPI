# Mz.Collections

`Mz.Collections` provides small collection helpers for Space Engineers source
projects where the desired framework collection wrappers are unavailable to mod
code.

The initial package provides:

- `ReadOnlyListView<T>` for exposing an `IList<T>` as `IReadOnlyList<T>`.
- `ReadOnlyDictionaryView<TKey, TValue>` for exposing an
  `IDictionary<TKey, TValue>` as `IReadOnlyDictionary<TKey, TValue>`.

Both types are live views. They do not copy their backing collection. Changes
made through the original mutable collection are immediately visible through
the read-only view.

## Install

### Install with SELibs

After installing SELibs, run these commands from the root of the mod project:

    selibs init
    selibs add Mz.Collections@0.1.0

Skip `selibs init` when the project already contains `selibs.json`.

### Install manually

Use the source from the matching release tag and copy:

    src/Mz.SemanticVersioning
    src/Mz.Collections

as sibling folders under the mod's script library directory. Compile all
contained `.cs` files as part of the mod and keep package versions aligned with
the exact dependencies declared by `LibraryVersionFile`.

## Read-only list view

    using System.Collections.Generic;
    using Mz.Collections;

    List<string> names = new List<string>();
    IReadOnlyList<string> namesView = new ReadOnlyListView<string>(names);

    names.Add("Alpha");

    int count = namesView.Count;
    string first = namesView[0];

The wrapper prevents mutation through its public interface while preserving the
live relationship with the backing list.

## Read-only dictionary view

    using System.Collections.Generic;
    using Mz.Collections;

    Dictionary<string, int> counts = new Dictionary<string, int>();
    IReadOnlyDictionary<string, int> countsView =
        new ReadOnlyDictionaryView<string, int>(counts);

    counts["Alpha"] = 1;

    int alpha = countsView["Alpha"];

The dictionary view forwards lookup, key/value enumeration, count, and
enumeration to its backing dictionary.

## Ownership

The wrappers do not own, clone, or synchronize their backing collections.
Callers remain responsible for mutation and thread-safety.

## Package version

The released package version is available through:

    string packageVersion = Mz.Collections.LibraryVersionFile.VersionString;