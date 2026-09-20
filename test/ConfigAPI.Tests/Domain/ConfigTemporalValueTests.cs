using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigTemporalValueTests
    {
        [Test]
        public void Temporal_Scalars_Use_Dedicated_Semantic_Kinds()
        {
            var date = new ConfigLocalDate(2026, 9, 1);
            var time = new ConfigLocalTime(7, 30, 45, "00100");

            var offset = ConfigScalarNode.OffsetDateTime(
                new ConfigOffsetDateTime(date, time, -90));

            var localDateTime = ConfigScalarNode.LocalDateTime(
                new ConfigLocalDateTime(date, time));

            var localDate = ConfigScalarNode.LocalDate(date);
            var localTime = ConfigScalarNode.LocalTime(time);

            Assert.Multiple(() =>
            {
                Assert.That(offset.Kind, Is.EqualTo(ConfigScalarKind.OffsetDateTime));
                Assert.That(localDateTime.Kind, Is.EqualTo(ConfigScalarKind.LocalDateTime));
                Assert.That(localDate.Kind, Is.EqualTo(ConfigScalarKind.LocalDate));
                Assert.That(localTime.Kind, Is.EqualTo(ConfigScalarKind.LocalTime));

                Assert.That(offset.Equals(localDateTime), Is.False);
                Assert.That(localDateTime.Equals(localDate), Is.False);
                Assert.That(localDate.Equals(localTime), Is.False);
            });
        }

        [Test]
        public void Temporal_Values_Compare_By_All_Toml_Semantic_Components()
        {
            var first = new ConfigOffsetDateTime(
                new ConfigLocalDate(1979, 5, 27),
                new ConfigLocalTime(7, 32, 0, "123"),
                0,
                true);

            var same = new ConfigOffsetDateTime(
                new ConfigLocalDate(1979, 5, 27),
                new ConfigLocalTime(7, 32, 0, "123"),
                0,
                true);

            var knownZeroOffset = new ConfigOffsetDateTime(
                new ConfigLocalDate(1979, 5, 27),
                new ConfigLocalTime(7, 32, 0, "123"),
                0,
                false);

            Assert.Multiple(() =>
            {
                Assert.That(first.Equals(same), Is.True);
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
                Assert.That(first.Equals(knownZeroOffset), Is.False);
            });
        }

        [Test]
        public void Temporal_Values_Preserve_Leap_Second_And_Fractional_Digits()
        {
            var time = new ConfigLocalTime(23, 59, 60, "0012300");

            Assert.Multiple(() =>
            {
                Assert.That(time.Second, Is.EqualTo(60));
                Assert.That(time.FractionalSeconds, Is.EqualTo("0012300"));
            });
        }

        [Test]
        public void Temporal_Constructors_Reject_Invalid_Components()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => new ConfigLocalDate(2026, 2, 30));
                Assert.Throws<ArgumentException>(() => new ConfigLocalTime(24, 0, 0));
                Assert.Throws<ArgumentException>(() => new ConfigLocalTime(0, 0, 0, "12x"));
                Assert.Throws<ArgumentException>(() =>
                    new ConfigOffsetDateTime(
                        new ConfigLocalDate(2026, 9, 1),
                        new ConfigLocalTime(0, 0, 0),
                        60,
                        true));
            });
        }
    }
}