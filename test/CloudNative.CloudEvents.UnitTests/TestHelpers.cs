// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    /// <summary>
    /// Helpers for test code, e.g. common non-trivial assertions.
    /// </summary>
    internal class TestHelpers
    {
        /// <summary>
        /// Asserts that two timestamp values are equal, expressing the expected value as a
        /// string for compact testing.
        /// </summary>
        /// <param name="expected">The expected value, as a string</param>
        /// <param name="actual">The value to test against</param>
        internal static void AssertTimestampsEqual(string expected, DateTimeOffset actual)
        {
            // TODO: Use common RFC-3339 parsing code when we have it.
            DateTimeOffset expectedDto = DateTimeOffset.ParseExact(expected, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture);
            AssertTimestampsEqual(expectedDto, actual);
        }

        /// <summary>
        /// Asserts that two timestamp values are equal, in both "instant being represented"
        /// and "UTC offset".
        /// </summary>
        /// <param name="expected">The expected value</param>
        /// <param name="actual">The value to test against</param>
        internal static void AssertTimestampsEqual(DateTimeOffset expected, DateTimeOffset actual)
        {
            Assert.Equal(expected.UtcDateTime, actual.UtcDateTime);
            Assert.Equal(expected.Offset, actual.Offset);
        }

        /// <summary>
        /// Asserts that two timestamp values are equal, in both "instant being represented"
        /// and "UTC offset". This overload accepts nullable values, and requires that both
        /// values are null or neither is.
        /// </summary>
        /// <param name="expected">The expected value</param>
        /// <param name="actual">The value to test against</param>
        internal static void AssertTimestampsEqual(DateTimeOffset? expected, DateTimeOffset? actual)
        {
            if (expected is null && actual is null)
            {
                return;
            }
            if (expected is null || actual is null)
            {
                Assert.True(false, "Expected both values to be null, or neither to be null");
            }
            AssertTimestampsEqual(expected.Value, actual.Value);
        }
    }
}
