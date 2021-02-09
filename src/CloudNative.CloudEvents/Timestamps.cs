// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Helper methods for CloudEvent timestamp attributes, which are represented
    /// as <see cref="DateTimeOffset"/> values within the SDK, and use RFC-3339
    /// for string representations (e.g. in headers).
    /// </summary>
    internal static class Timestamps
    {
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        /// <summary>
        /// Length of shortest valid value ("yyyy-MM-ddTHH:mm:ssZ")
        /// </summary>
        private const int MinLength = 20;

        /// <summary>
        /// Earliest position of UTC offset indicator in a valid timestamp.
        /// </summary>
        private const int MinOffsetIndex = 19;

        /// <summary>
        /// Length of longest the date/time part of valid value that we'll actually parse
        /// ("yyyy-MM-ddTHH:mm:ss.FFFFFFF"). Further subsecond digits may be present, but
        /// we'll ignore them.
        /// </summary>
        private const int MaxDateTimeParseLength = 27;

        /// <summary>
        /// Maximum number of minutes in an offset: DateTimeOffset only handles up to +/- 14 hours.
        /// </summary>
        private const int MaxOffsetMinutes = 14 * 60;

        private static readonly char[] offsetLeadingCharacters = { 'Z', '+', '-' };

        /// <summary>
        /// Attempts to parse a string as an RFC-3339-formatted date/time and UTC offset.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryParse(string input, out DateTimeOffset result)
        {
            // TODO: Check this and add a test
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (input.Length < MinLength) // "yyyy-MM-ddTHH:mm:ssZ" is the shortest possible value.
            {
                result = default;
                return false;
            }
            // Find the UTC offset indicator, by starting at index 19 (the earliest possible index)
            // and looking for Z, + or -.
            int offsetIndex = input.IndexOfAny(offsetLeadingCharacters, MinOffsetIndex);
            if (offsetIndex == -1)
            {
                result = default;
                return false;
            }
            if (!TryParseLocalPart(input, offsetIndex, out var localPart) ||
                !TryParseOffset(input, offsetIndex, out var offset))
            {
                result = default;
                return false;
            }
            result = new DateTimeOffset(localPart, offset);
            return true;
        }

        private static bool TryParseLocalPart(string input, int offsetIndex, out DateTime localPart)
        {
            // Find the end of the text we want to parse with DateTime.TryParseExact.
            // We truncate timestamps that have sub-tick precision, but we need to validate that any later characters are digits.
            string textToParse;
            if (offsetIndex <= MaxDateTimeParseLength)
            {
                textToParse = input.Substring(0, offsetIndex);
            }
            else
            {
                for (int index = MaxDateTimeParseLength; index < offsetIndex; index++)
                {
                    if (!IsAsciiDigit(input[index]))
                    {
                        localPart = default;
                        return false;
                    }
                }
                textToParse = input.Substring(0, MaxDateTimeParseLength);
            }
            return DateTime.TryParseExact(
                textToParse, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
                CultureInfo.InvariantCulture, DateTimeStyles.None,
                out localPart);
        }

        private static bool TryParseOffset(string input, int offsetIndex, out TimeSpan offset)
        {
            char prefix = input[offsetIndex]; // We already know this will be Z, + or -
            if (prefix == 'Z')
            {
                offset = TimeSpan.Zero; // This is the value we want whether or not the length is right
                return input.Length == offsetIndex + 1; // We expect the Z to be the end of the string
            }
            // Non-Z offsets much be exactly in the format +XX:YY or -XX:YY, so we can check the length and
            // expected characters very straightforwardly.
            if (input.Length != offsetIndex + 6 ||
                !IsAsciiDigit(input[offsetIndex + 1]) ||
                !IsAsciiDigit(input[offsetIndex + 2]) ||
                input[offsetIndex + 3] != ':' ||
                !IsAsciiDigit(input[offsetIndex + 4]) ||
                !IsAsciiDigit(input[offsetIndex + 5]))
            {
                offset = default;
                return false;
            }

            // Parse the digits as simply as possible.
            int hours = ParseDigit(input[offsetIndex + 1]) * 10 + ParseDigit(input[offsetIndex + 2]);
            int minutes = ParseDigit(input[offsetIndex + 4]) * 10 + ParseDigit(input[offsetIndex + 5]);
            int totalMinutes = hours * 60 + minutes;
            if (minutes >= 60 || totalMinutes > MaxOffsetMinutes)
            {
                return false;
            }

            // Handle the sign
            if (input[offsetIndex] == '-')
            {
                totalMinutes = -totalMinutes;
            }

            offset = TimeSpan.FromMinutes(totalMinutes);
            return true;

            static int ParseDigit(char c) => c - '0';
        }

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        /// <summary>
        /// Parses a string as an RFC-3339-formatted date/time and UTC offset.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static DateTimeOffset Parse(string input) =>
            TryParse(input, out var result) ? result : throw new FormatException("Invalid timestamp");

        /// <summary>
        /// Converts a <see cref="DateTimeOffset"/> value to a string using RFC-3339 format.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The sub-second precision in the result is determined by the first of the following conditions
        /// to be met:
        /// If the value is a whole number of seconds, the result contains no fractional-second
        /// indicator at all.
        /// If the sub-second value is a whole number of milliseconds, the result will contain three
        /// digits of sub-second precision.
        /// If the sub-second value is a whole number of microseconds, the result will contain six
        /// digits of sub-second precision.
        /// Otherwise, the result will contain 7 digits of sub-second precision. (This is the maximum
        /// precision of <see cref="DateTimeOffset"/>.)
        /// </para>
        /// <para>
        /// If the UTC offset is zero, this is represented as a suffix of 'Z';
        /// otherwise, the offset is represented in the "+HH:mm" or "-HH:mm" format.
        /// </para>
        /// </remarks>
        /// <param name="value">The value to convert to an RFC-3339 format string.</param>
        /// <returns>The formatted string.</returns>
        public static string Format(DateTimeOffset value)
        {
            var ticks = value.Ticks;
            string formatString =
                value.Offset.Ticks == 0 ?
                    // UTC+0 branch: hard-code 'Z'
                    (ticks % TimeSpan.TicksPerSecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss'Z'" :
                    ticks % TimeSpan.TicksPerMillisecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" :
                    ticks % TicksPerMicrosecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'" :
                    "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'") :
                    // Non-UTC branch: use zzz to format the offset
                    (ticks % TimeSpan.TicksPerSecond == 0 ? "yyyy-MM-dd'T'HH:mm:sszzz" :
                    ticks % TimeSpan.TicksPerMillisecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss.fffzzz" :
                    ticks % TicksPerMicrosecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz" :
                    "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz");
            return value.ToString(formatString, CultureInfo.InvariantCulture);
        }
    }
}
