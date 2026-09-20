using System;
using System.Text.RegularExpressions;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using Sandbox.ModAPI;

namespace MarcoZechner.ConfigAPI.Client.Core
{
    public static class ConfigXmlSerializer
    {
        public static string SerializeToXml(ConfigBase config)
        {
            return MyAPIGateway.Utilities.SerializeToXML(config);
        }

        private const string INVALID_ENUM_PATTERN = @"'(?<value>[^']+)' is not a valid value for (?<enum>\w+)";

        public static T DeserializeFromXml<T>(string xml) where T :
            ConfigBase, new()
        {
            try
            {
                return MyAPIGateway.Utilities.SerializeFromXML<T>(xml);
            }
            catch (Exception ex) // catch invalid enum deserialization
            {
                var message = ex.InnerException?.Message;
                if (message == null) throw;
                var match = Regex.Match(message, INVALID_ENUM_PATTERN);
                if (!match.Success)
                {
                    CfgLogWorld.Warning($"Unknown deserialization error in ConfigType {typeof(T).Name}: {message}");
                    return null;
                }

                var enumName = match.Groups["enum"].Value;
                var attemptedValue = match.Groups["value"].Value;
                CfgLogWorld.Warning($"Deserialization error in ConfigType {typeof(T).Name}: '{attemptedValue}' is not a valid value for enum '{enumName}'.");
                return null;
            }
        }
    }
}