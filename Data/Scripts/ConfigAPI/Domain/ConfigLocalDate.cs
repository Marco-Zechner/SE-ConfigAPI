using System;

namespace MarcoZechner.ConfigAPI.Domain
{
    public sealed class ConfigLocalDate : IEquatable<ConfigLocalDate>
    {
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public ConfigLocalDate(int year, int month, int day)
        {
            if (!IsValidDate(year, month, day))
                throw new ArgumentException("The supplied components do not form a valid local date.");

            Year = year;
            Month = month;
            Day = day;
        }

        public bool Equals(ConfigLocalDate other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Year == other.Year &&
                   Month == other.Month &&
                   Day == other.Day;
        }

        public override bool Equals(object obj) => Equals(obj as ConfigLocalDate);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Year;
                hash = (hash * 397) ^ Month;
                hash = (hash * 397) ^ Day;
                return hash;
            }
        }

        private static bool IsValidDate(int year, int month, int day)
        {
            if (year < 0 || year > 9999 || month < 1 || month > 12 || day < 1)
                return false;

            return day <= DaysInMonth(year, month);
        }

        private static int DaysInMonth(int year, int month)
        {
            switch (month)
            {
                case 2:
                    return IsLeapYear(year) ? 29 : 28;
                case 4:
                case 6:
                case 9:
                case 11:
                    return 30;
                default:
                    return 31;
            }
        }

        private static bool IsLeapYear(int year)
        {
            if (year % 400 == 0)
                return true;

            if (year % 100 == 0)
                return false;

            return year % 4 == 0;
        }
    }
}