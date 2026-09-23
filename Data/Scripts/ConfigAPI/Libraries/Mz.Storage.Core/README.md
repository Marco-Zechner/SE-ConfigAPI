# Mz.Storage

`Mz.Storage` provides indexed text-file storage for Space Engineers mods.
It maintains a persisted logical-name index because the ModAPI can probe known
filenames but cannot enumerate all files in a storage directory.

The package contains:

- `Mz.Storage.Core` - portable indexed storage behavior over `IStorageBackend`.
- `Mz.Storage.SpaceEngineers` - Local, World, and safely namespaced Global adapters.

## Install

### Install with SELibs

After installing SELibs, run these commands from the root of the mod project:

    selibs init
    selibs add Mz.Storage@0.1.1

Skip `selibs init` when the project already contains `selibs.json`.

### Install manually

Use the source from the matching release tag and copy these folders:

    src/Mz.SemanticVersioning
    src/Mz.Storage.Core
    src/Mz.Storage.SpaceEngineers

as sibling folders under the mod's script library directory. Compile all
contained `.cs` files as part of the mod and keep package versions aligned with
the exact dependencies declared by `LibraryVersionFile`.

## Create Space Engineers storage

Create storage after `MyAPIGateway.Utilities` becomes available in the mod lifecycle:

    using Mz.Storage;
    using Mz.Storage.SpaceEngineers;

    IndexedStorage local = SpaceEngineersStorage.CreateLocal(typeof(MySession));
    IndexedStorage world = SpaceEngineersStorage.CreateWorld(typeof(MySession));
    IndexedStorage global = SpaceEngineersStorage.CreateGlobal("MyMod");

Local storage is scoped by the calling type's assembly. World storage uses the
same assembly scope inside the active save. Their private index filename is
`.index`.

Global storage is shared by all mods, so it needs a stable owner supplied to
`CreateGlobal(string)`. Space Engineers uses an internal assembly scope for
Local and World storage, but the mod whitelist does not expose the reflection
APIs required to recover that exact scope name safely for Global storage.

For owner `MyMod`, logical name `config.toml` is stored physically as
`MyMod.config.toml`, while leading-dot metadata such as `.defaults` is stored as
`.MyMod.defaults` and the private global index is `.MyMod.index`. Callers
continue to work only with the unprefixed logical name `config.toml`.

## Save, load, probe, and list

    world.Save("server.toml", "enabled = true");

    bool exists = world.Exists("server.toml");
    string text = world.Load("server.toml");
    string[] known = world.ListKnown();

`Save` writes the file and records its logical name in the persisted index.
`Load` throws `FileNotFoundException` when the physical file is missing. A
successful load of a deterministic filename that was not already indexed adds
that logical name to the index, which supports discovery of pre-existing files.

`Exists` probes the deterministic physical filename without changing the index.
`ListKnown` returns logical names in ordinal order and removes stale entries whose
physical files disappeared. Duplicate, blank, and reserved index entries are also
repaired when the index is read.

## Logical names and the private index

Logical names must be nonblank, cannot contain line breaks, and cannot equal
`.index`. The reserved logical/default index filename is an implementation
detail and is never returned by `ListKnown`.

Local and World use `.index` physically inside their already isolated storage
scope. Global storage maps its private index to `.<owner>.index`, keeping each
owner's index separate in the shared global storage directory.

A leading dot is only a naming convention here. Windows does not automatically
apply the Hidden file attribute merely because a filename starts with `.`.

## Use the portable core

Implement `IStorageBackend` when storage comes from another deterministic text-file
transport:

    public interface IStorageBackend
    {
        bool Exists(string fileName);
        string Read(string fileName);
        void Write(string fileName, string content);
    }

Then construct `IndexedStorage` directly. The two-argument constructor prefixes
physical filenames using the default `.index` name. The three-argument
constructor lets a backend choose an independent physical index filename while
preserving the same logical names. The four-argument constructor additionally
allows leading-dot logical metadata names to use a separate physical prefix.

## Package version

The released package version is available through:

    string packageVersion = Mz.Storage.LibraryVersionFile.VersionString;
