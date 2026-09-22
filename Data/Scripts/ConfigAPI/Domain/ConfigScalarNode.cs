using System;

namespace MarcoZechner.ConfigAPI.Domain
{
    public enum ConfigScalarKind
    {
        Boolean = 0,
        Integer = 1,
        Float = 2,
        String = 3,
        OffsetDateTime = 4,
        LocalDateTime = 5,
        LocalDate = 6,
        LocalTime = 7
    }

    public sealed class ConfigScalarNode : ConfigNode
    {
        public ConfigScalarKind Kind { get; }
        public object Value { get; }

        private ConfigScalarNode(ConfigScalarKind kind, object value)
        {
            Kind = kind;
            Value = value;
        }

        public static ConfigScalarNode Boolean(bool value) => new ConfigScalarNode(ConfigScalarKind.Boolean, value);
        public static ConfigScalarNode Integer(long value) => new ConfigScalarNode(ConfigScalarKind.Integer, value);
        public static ConfigScalarNode Float(double value) => new ConfigScalarNode(ConfigScalarKind.Float, value);

        public static ConfigScalarNode String(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigScalarNode(ConfigScalarKind.String, value);
        }

        public static ConfigScalarNode OffsetDateTime(ConfigOffsetDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigScalarNode(ConfigScalarKind.OffsetDateTime, value);
        }

        public static ConfigScalarNode LocalDateTime(ConfigLocalDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigScalarNode(ConfigScalarKind.LocalDateTime, value);
        }

        public static ConfigScalarNode LocalDate(ConfigLocalDate value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigScalarNode(ConfigScalarKind.LocalDate, value);
        }

        public static ConfigScalarNode LocalTime(ConfigLocalTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigScalarNode(ConfigScalarKind.LocalTime, value);
        }

        protected override bool EqualsNode(ConfigNode other)
        {
            var scalar = other as ConfigScalarNode;
            if (scalar == null || Kind != scalar.Kind)
                return false;

            switch (Kind)
            {
                case ConfigScalarKind.Boolean:
                    return (bool)Value == (bool)scalar.Value;
                case ConfigScalarKind.Integer:
                    return (long)Value == (long)scalar.Value;
                case ConfigScalarKind.Float:
                    return ((double)Value).Equals((double)scalar.Value);
                case ConfigScalarKind.String:
                    return string.Equals((string)Value, (string)scalar.Value, StringComparison.Ordinal);
                case ConfigScalarKind.OffsetDateTime:
                    return ((ConfigOffsetDateTime)Value).Equals((ConfigOffsetDateTime)scalar.Value);
                case ConfigScalarKind.LocalDateTime:
                    return ((ConfigLocalDateTime)Value).Equals((ConfigLocalDateTime)scalar.Value);
                case ConfigScalarKind.LocalDate:
                    return ((ConfigLocalDate)Value).Equals((ConfigLocalDate)scalar.Value);
                case ConfigScalarKind.LocalTime:
                    return ((ConfigLocalTime)Value).Equals((ConfigLocalTime)scalar.Value);
                default:
                    throw new InvalidOperationException("Unknown config scalar kind: " + Kind);
            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;

                switch (Kind)
                {
                    case ConfigScalarKind.Boolean:
                        return (hash * 397) ^ ((bool)Value).GetHashCode();
                    case ConfigScalarKind.Integer:
                        return (hash * 397) ^ ((long)Value).GetHashCode();
                    case ConfigScalarKind.Float:
                        return (hash * 397) ^ ((double)Value).GetHashCode();
                    case ConfigScalarKind.String:
                        return (hash * 397) ^ StringComparer.Ordinal.GetHashCode((string)Value);
                    case ConfigScalarKind.OffsetDateTime:
                        return (hash * 397) ^ ((ConfigOffsetDateTime)Value).GetHashCode();
                    case ConfigScalarKind.LocalDateTime:
                        return (hash * 397) ^ ((ConfigLocalDateTime)Value).GetHashCode();
                    case ConfigScalarKind.LocalDate:
                        return (hash * 397) ^ ((ConfigLocalDate)Value).GetHashCode();
                    case ConfigScalarKind.LocalTime:
                        return (hash * 397) ^ ((ConfigLocalTime)Value).GetHashCode();
                    default:
                        throw new InvalidOperationException("Unknown config scalar kind: " + Kind);
                }
            }
        }
    }
}