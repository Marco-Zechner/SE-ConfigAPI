using System;
using MarcoZechner.ConfigAPI.Domain;
using Mz.Logging;

namespace MarcoZechner.ConfigAPI
{
    public sealed class ConfigApiRuntimeConfig
    {
        public LogLevel MinimumLogLevel = LogLevel.Trace;
    }

    public static class ConfigApiRuntimeConfigDefinition
    {
        public const string ConfigKey = "ConfigAPI";
        public const string FileName = "ConfigAPI.toml";

        public static ConfigDocument CreateDefaults() => Serialize(new ConfigApiRuntimeConfig());

        public static ConfigDocument Serialize(ConfigApiRuntimeConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new ConfigDocument(new ConfigObjectNode(
                new ConfigObjectEntry("MinimumLogLevel", ConfigScalarNode.String(config.MinimumLogLevel.ToString()))));
        }

        public static ConfigApiRuntimeConfig Deserialize(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            ConfigNode node;
            if (!document.TryGet(new ConfigValuePath("MinimumLogLevel"), out node))
                throw new FormatException("ConfigAPI runtime config requires a string MinimumLogLevel.");

            var minimumLogLevel = node as ConfigScalarNode;
            if (minimumLogLevel == null || minimumLogLevel.Kind != ConfigScalarKind.String)
                throw new FormatException("ConfigAPI runtime config requires a string MinimumLogLevel.");

            return new ConfigApiRuntimeConfig { MinimumLogLevel = ParseLogLevel((string)minimumLogLevel.Value) };
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