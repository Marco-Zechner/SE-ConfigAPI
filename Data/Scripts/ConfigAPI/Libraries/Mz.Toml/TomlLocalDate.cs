using System;
using System.Globalization;

namespace Mz.Toml
{
    /// <summary>
    /// Represents a TOML local date without a timezone or time of day.
    /// </summary>
    public sealed class TomlLocalDate
    {
        /// <summary>
        /// Initializes a TOML local date.
        /// </summary>
        public TomlLocalDate(int year, int month, int day)
        {
            if (!IsValidDate(year, month, day))
                throw new ArgumentException("The supplied components do not form a valid TOML local date.");

            Year = year;
            Month = month;
            Day = day;
        }

        /// <summary>
        /// Gets the four-digit year from 0000 through 9999.
        /// </summary>
        public int Year { get; }

        /// <summary>
        /// Gets the month from 1 through 12.
        /// </summary>
        public int Month { get; }

        /// <summary>
        /// Gets the day of month.
        /// </summary>
        public int Day { get; }

        /// <summary>
        /// Returns the canonical TOML local-date spelling.
        /// </summary>
        public override string ToString()
            => $"{Year.ToString("D4", CultureInfo.InvariantCulture)}-" +
               $"{Month.ToString("D2", CultureInfo.InvariantCulture)}-" +
               $"{Day.ToString("D2", CultureInfo.InvariantCulture)}";

        internal static bool IsValidDate(int year, int month, int day)
        {
            if (year < 0 || year > 9999 || month < 1 || month > 12 || day < 1)
                return false;

            var days = DaysInMonth(year, month);
            return day <= days;
        }

        private static int DaysInMonth(int year, int month)
        {
            switch (month)
            {
                case 2: return IsLeapYear(year) ? 29 : 28;
                case 4:
                case 6:
                case 9:
                case 11: return 30;
                default: return 31;
            }
        }

        private static bool IsLeapYear(int year)
        {
            if (year % 400 == 0) return true;
            if (year % 100 == 0) return false;
            return year % 4 == 0;
        }
    }
}
