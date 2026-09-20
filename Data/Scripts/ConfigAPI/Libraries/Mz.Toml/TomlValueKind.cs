namespace Mz.Toml
{
    /// <summary>
    /// Identifies the scalar type stored by a <see cref="TomlValue"/>.
    /// </summary>
    public enum TomlValueKind
    {
        /// <summary>
        /// A TOML basic or literal string value.
        /// </summary>
        String,

        /// <summary>
        /// A TOML integer value.
        /// </summary>
        Integer,

        /// <summary>
        /// A TOML floating-point value.
        /// </summary>
        Float,

        /// <summary>
        /// A TOML Boolean value.
        /// </summary>
        Boolean,

        /// <summary>
        /// A TOML offset date-time value.
        /// </summary>
        OffsetDateTime,

        /// <summary>
        /// A TOML local date-time value.
        /// </summary>
        LocalDateTime,

        /// <summary>
        /// A TOML local date value.
        /// </summary>
        LocalDate,

        /// <summary>
        /// A TOML local time value.
        /// </summary>
        LocalTime
    }
}
