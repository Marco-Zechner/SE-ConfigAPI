using System;
using Mz.ConfigApi;
using Mz.Logging;

namespace MarcoZechner.ConfigAPI.V2
{
    public sealed class ConfigApiRuntimeConfig
    {
        public LogLevel MinimumLogLevel = LogLevel.Trace;
    }

    public static class ConfigApiRuntimeConfigDefinition
    {
        public static readonly ConfigDefinition<ConfigApiRuntimeConfig> Definition = new ConfigDefinition<ConfigApiRuntimeConfig>(
            "ConfigAPI", "ConfigAPI.toml", () => new ConfigApiRuntimeConfig(), Serialize, Deserialize);

        private static ConfigDocument Serialize(ConfigApiRuntimeConfig config)
        {
            return new ConfigDocument(new ConfigEntry("MinimumLogLevel", ConfigValue.String(config.MinimumLogLevel.ToString())));
        }

        private static ConfigApiRuntimeConfig Deserialize(ConfigDocument document)
        {
            ConfigValue minimumLogLevel;

            if (!document.TryGet("MinimumLogLevel", out minimumLogLevel) || minimumLogLevel.Kind != ConfigValueKind.String)
                throw new FormatException("ConfigAPI runtime config requires a string MinimumLogLevel.");

            return new ConfigApiRuntimeConfig { MinimumLogLevel = ParseLogLevel((string)minimumLogLevel.ScalarValue) };
        }

        private static LogLevel ParseLogLevel(string value)
        {
            if (value == null)
                throw new FormatException("ConfigAPI MinimumLogLevel must not be null.");

            switch (value.Trim().ToLowerInvariant())
            {
                case "trace": return LogLevel.Trace;
                case "debug": return LogLevel.Debug;
                case "information":
                case "info": return LogLevel.Information;
                case "warning":
                case "warn": return LogLevel.Warning;
                case "error": return LogLevel.Error;
                case "critical":
                case "fatal": return LogLevel.Critical;
                default: throw new FormatException("Unknown ConfigAPI MinimumLogLevel: " + value);
            }
        }
    }
}