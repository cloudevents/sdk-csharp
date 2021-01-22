// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    using static TestHelpers;

    public class TimestampsTest
    {
        // TryParse and Parse are tested together, as they're effectively alternatives for the same thing.

        /// <summary>
        /// Just a selection of simple tests; this is not trying to be exhaustive.
        /// (The other parse tests check specific aspects more thoroughly.)
        /// </summary>
        [Theory]
        [InlineData("2021-01-18T14:52:01Z", 2021, 1, 18, 14, 52, 1, 0, 0)]
        [InlineData("2000-02-29T01:23:45.678+01:30", 2000, 2, 29, 1, 23, 45, 6_780_000, 90)]
        [InlineData("2000-02-29T01:23:45-01:30", 2000, 2, 29, 1, 23, 45, 0, -90)]
        void Parse_Success_Simple(string text, int year, int month, int day, int hour, int minute, int second, int ticks, int offsetMinutes)
        {
            var expected = new DateTimeOffset(year, month, day, hour, minute, second, 0, TimeSpan.FromMinutes(offsetMinutes))
                .AddTicks(ticks);
            AssertParseSuccess(expected, text);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData(".0", 0)]
        [InlineData(".1", 1_000_000)]
        [InlineData(".12", 1_200_000)]
        [InlineData(".123", 1_230_000)]
        [InlineData(".1234", 1_234_000)]
        [InlineData(".12345", 1_234_500)]
        [InlineData(".123456", 1_234_560)]
        [InlineData(".1234567", 1_234_567)]
        // We truncate nanoseconds to the tick
        [InlineData(".12345678", 1_234_567)]
        [InlineData(".123456789", 1_234_567)]
        // (Realistically we're unlikely to get any values with greater precision than nanoseconds, but
        // we might as well test it.)
        [InlineData(".12345678912345", 1_234_567)]
        void Parse_Success_VaryingFractionalSeconds(string fractionalPart, int expectedTicks)
        {
            string text = $"2021-01-18T14:52:01{fractionalPart}+05:00";
            DateTimeOffset expected = new DateTimeOffset(2021, 1, 18, 14, 52, 1, 0, TimeSpan.FromHours(5))
                .AddTicks(expectedTicks);
            AssertParseSuccess(expected, text);
        }

        [Theory]
        [InlineData("Z", 0)]
        // Alternative way of representing UTC (this is perfectly valid).
        [InlineData("+00:00", 0)]
        // This is the "unknown local offset". We treat this as UTC, as there is no reasonable
        // way to express it as a DateTimeOffset, and that's better than failing. It's unlikely
        // we'll ever see this, and arguably it's not really a "timestamp" at that point anyway.
        [InlineData("-00:00", 0)]
        [InlineData("+01:00", 60)]
        [InlineData("-01:00", -60)]
        [InlineData("+01:30", 90)]
        [InlineData("-01:30", -90)]
        // Extreme values
        [InlineData("+14:00", 14 * 60)]
        [InlineData("-14:00", -14 * 60)]
        void Parse_Success_VaryingUtcOffset(string offsetPart, int expectedOffsetMinutes)
        {
            // No fractional seconds
            string text = $"2021-01-18T14:52:01{offsetPart}";
            DateTimeOffset expected = new DateTimeOffset(2021, 1, 18, 14, 52, 1, 0, TimeSpan.FromMinutes(expectedOffsetMinutes));
            AssertParseSuccess(expected, text);

            // Single check for fractional seconds
            text = $"2021-01-18T14:52:01.500{offsetPart}";
            expected = expected.AddMilliseconds(500);
            AssertParseSuccess(expected, text);
        }

        static void AssertParseSuccess(DateTimeOffset expected, string text)
        {
            var parsed = Timestamps.Parse(text);
            AssertTimestampsEqual(expected, parsed);

            Assert.True(Timestamps.TryParse(text, out parsed));
            AssertTimestampsEqual(expected, parsed);
        }

        [Theory]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("garbage that is long enough")]
        [InlineData("2021-01-18T14:52:01")] // No UTC offset indicator
        [InlineData("2021-01-18T14:52:01.1234567XYZ")] // Garbage after 7 significant digits of sub-second
        [InlineData("2021-01-18T14:52:01Z01")] // Text after UTC offset indicator
        [InlineData("2021-01-18T14:52:01X")] // Garbage UTC offset indicator
        [InlineData("2021-01-18T14:52:01+XX:XX")] // Garbage UTC offset indicator (but right length)
        [InlineData("2021-01-18T14:52:01+01")] // Hour-only UTC offset indicator
        [InlineData("2021-01-18T14:52:01+01:30:30")] // Sub-minute UTC offset indicator
        [InlineData("2021-01-18T14:52:01+14:01")] // UTC offset indicator out of range
        [InlineData("2021-01-18T14:52:01-14:01")] // UTC offset indicator out of range
        [InlineData("2021-01-18T14:52:01-00:60")] // UTC offset indicator with invalid minutes
        [InlineData("2021-01-18 14:52:01Z")] // Space instead of 'T'
        [InlineData("2100-02-29T14:52:01Z")] // Feb 29th in non-leap-year
        [InlineData("10000-01-01T00:00:00Z")] // Year out of range
        [InlineData("2021-13-01T00:00:00Z")] // Month out of range
        [InlineData("2021-01-50T00:00:00Z")] // Day out of range
        [InlineData("2021-01-18T24:00:00Z")] // Hour out of range
        [InlineData("2021-01-18T14:60:00Z")] // Minute out of range
        [InlineData("2021-01-18T14:00:60Z")] // Second out of range
        [InlineData("100-01-01T00:00:00Z")] // Non-padded year
        [InlineData("2021-1-01T00:00:00Z")] // Non-padded month
        [InlineData("2021-01-1T00:00:00Z")] // Non-padded day
        [InlineData("2021-01-01T1:00:00Z")] // Non-padded hour
        [InlineData("2021-01-01T00:1:00Z")] // Non-padded minute
        [InlineData("2021-01-01T00:01:1Z")] // Non-padded second
        [InlineData("2021-01-01T00:01Z")] // No second part
        void Parse_Failure(string text)
        {
            Assert.False(Timestamps.TryParse(text, out _));
            Assert.Throws<FormatException>(() => Timestamps.Parse(text));
        }

        /// <summary>
        /// As we're already testing parsing thoroughly, the simplest way of providing
        /// a value to format is to parse a string. Many examples will round-trip, in which
        /// case it's simple just to provide the information once.
        /// <see cref="Format_NonRoundtrip(string, string)"/> tests situations which don't round-trip.
        /// </summary>
        /// <param name="input"></param>
        [Theory]
        [InlineData("2021-01-18T14:52:01Z")]
        [InlineData("2000-02-29T01:23:45.678+01:30")]
        [InlineData("2000-02-29T01:23:45-01:30")]
        [InlineData("2000-02-29T01:23:45.678+10:00")]
        [InlineData("2000-02-29T01:23:45-10:00")]
        [InlineData("2021-01-18T14:52:01.100Z")]
        [InlineData("2021-01-18T14:52:01.120Z")]
        [InlineData("2021-01-18T14:52:01.123Z")]
        [InlineData("2021-01-18T14:52:01.123400Z")]
        [InlineData("2021-01-18T14:52:01.123450Z")]
        [InlineData("2021-01-18T14:52:01.123456Z")]
        [InlineData("2021-01-18T14:52:01.1234567Z")]
        void Format_Roundtrip(string input)
        {
            var parsed = Timestamps.Parse(input);
            var formatted = Timestamps.Format(parsed);
            Assert.Equal(input, formatted);
        }

        [Theory]
        // Zero offset normalized to Z
        [InlineData("2021-01-18T14:52:01+00:00", "2021-01-18T14:52:01Z")]
        [InlineData("2021-01-18T14:52:01-00:00", "2021-01-18T14:52:01Z")]
        // Second precision
        [InlineData("2000-02-29T01:23:45.000-01:30", "2000-02-29T01:23:45-01:30")]
        // Millisecond precision
        [InlineData("2000-02-29T01:23:45.67+01:30", "2000-02-29T01:23:45.670+01:30")]
        [InlineData("2000-02-29T01:23:45.678000+01:30", "2000-02-29T01:23:45.678+01:30")]
        [InlineData("2000-02-29T01:23:45.6780000000+01:30", "2000-02-29T01:23:45.678+01:30")]
        // Microssecond precision
        [InlineData("2000-02-29T01:23:45.6781+01:30", "2000-02-29T01:23:45.678100+01:30")]
        [InlineData("2000-02-29T01:23:45.6781000+01:30", "2000-02-29T01:23:45.678100+01:30")]
        [InlineData("2000-02-29T01:23:45.6781000000+01:30", "2000-02-29T01:23:45.678100+01:30")]
        // Tick precision
        [InlineData("2021-01-18T14:52:01.123456789Z", "2021-01-18T14:52:01.1234567Z")]
        void Format_NonRoundtrip(string input, string expectedFormatted)
        {
            var parsed = Timestamps.Parse(input);
            var formatted = Timestamps.Format(parsed);
            Assert.Equal(expectedFormatted, formatted);
        }
    }
}
