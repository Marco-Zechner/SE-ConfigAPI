# Mz.Logging

`Mz.Logging` provides transport-independent logging primitives plus helpers
for writing log files through the Space Engineers ModAPI.

The package contains:

- `Mz.Logging.Core`  -  logger, levels, entries, formatters, and sinks.
- `Mz.Logging.SpaceEngineers`  -  local, world, and global storage writers.

`Mz.SemanticVersioning` is an exact package dependency shared by all
library packages in this repository.

## Install

### Install with SELibs

[SELibs](https://github.com/Marco-Zechner/selibs) is a source-library manager
for Space Engineers mods. Its repository contains the installer, command
reference, and project setup instructions.

After installing SELibs, run these commands from the root of the mod project:

```shell
    selibs init
    selibs add Mz.Logging@0.1.1
```

Skip `selibs init` when the project already contains `selibs.json`.

SELibs installs both Logging source components and the exact
`Mz.SemanticVersioning` dependency. It records their checksums and selected
versions. Inspect that state with:

```shell
    selibs status
```

### Install manually

To install without SELibs, use the source from the matching release tag in this
repository and copy these complete folders:

```text
    src/Mz.SemanticVersioning
    src/Mz.Logging.Core
    src/Mz.Logging.SpaceEngineers
```

Place them as sibling folders under the mod's script library directory:

```text
    Data/Scripts/ExampleMod/Libraries/Mz.SemanticVersioning
    Data/Scripts/ExampleMod/Libraries/Mz.Logging.Core
    Data/Scripts/ExampleMod/Libraries/Mz.Logging.SpaceEngineers
```

Compile all contained `.cs` files as part of the mod. Logging `0.1.1`
requires `Mz.SemanticVersioning` `0.1.1`. The Space Engineers component also
depends on the Core component, so use the exact dependency versions declared
by the selected package manifest.

## Space Engineers storage logging

Create loggers during the active mod lifecycle, after
`MyAPIGateway.Utilities` is available. Dispose the owner during unload so the
writer is flushed and closed.

```csharp
    using Mz.Logging;
    using Mz.Logging.SpaceEngineers;

    private SpaceEngineersStorageLogger _logging;

    public override void LoadData()
    {
        _logging = SpaceEngineersStorageLogger.CreateLocal(
            "ExampleMod.log",
            typeof(ExampleSession),
            "ExampleMod",
            LogLevel.Information
        );

        _logging.Logger.Info("Session loaded.");
    }

    protected override void UnloadData()
    {
        if (_logging != null)
        {
            _logging.Logger.Info("Session unloading.");
            _logging.Dispose();
            _logging = null;
        }
    }
```

Available storage factories are:

- `CreateLocal`  -  assembly-scoped local storage.
- `CreateWorld`  -  assembly-scoped storage inside the active save.
- `CreateGlobalUnsafe`  -  shared global storage.

Global storage is not assembly-scoped. Use a uniquely prefixed filename when
calling `CreateGlobalUnsafe` to prevent collisions with other mods.

## Write messages

`Logger` supplies convenience methods for every supported level:

```csharp
    Logger logger = _logging.Logger;

    logger.Trace("Detailed execution trace.");
    logger.Debug("Diagnostic state.");
    logger.Info("Normal operation.");
    logger.Warning("Recoverable problem.");
    logger.Error("Operation failed.", exception);
    logger.Critical("The mod cannot continue safely.", exception);
```

Messages below `MinimumLevel` are ignored:

```csharp
    logger.MinimumLevel = LogLevel.Warning;

    bool writesDebug = logger.IsEnabled(LogLevel.Debug);
    bool writesError = logger.IsEnabled(LogLevel.Error);
```

## Use the Core library directly

A logger dispatches immutable `LogEntry` objects to an `ILogSink`.

```csharp
    using System.IO;
    using Mz.Logging;

    var writer = new StringWriter();
    var formatter = new PlainTextLogFormatter();

    using (var sink = new TextWriterLogSink(writer, formatter, leaveOpen: true))
    {
        var logger = new Logger(
            "ExampleMod",
            sink,
            LogLevel.Debug
        );

        logger.Debug("Initialized.");
        sink.Flush();
    }
```

Implement `ILogSink` to route entries elsewhere, or use
`CompositeLogSink` to dispatch each entry to several sinks in registration
order.

Implement `ILogFormatter` to replace the default deterministic plain-text
format.

## Ownership and failures

`SpaceEngineersStorageLogger` owns its writer and sink. Calling `Dispose`
flushes and closes both. Calls after disposal throw.

Sink and formatter exceptions are not swallowed by `Logger`; callers should
handle failures according to their mod's lifecycle and reliability needs.

## Package version

The released package version is available through:

```csharp
    string packageVersion = Mz.Logging.LibraryVersionFile.VersionString;
```
