// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    /// <summary>
    /// Helpers for test code, e.g. common non-trivial assertions.
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// A set of extension attributes covering all attributes types.
        /// The name of each attribute is the lower-cased form of the attribute type
        /// name, with all punctuation removed (e.g. "urireference").
        /// The attributes do not have any extra validation.
        /// </summary>
        internal static IEnumerable<CloudEventAttribute> AllTypesExtensions { get; } = new List<CloudEventAttribute>
        {
            CloudEventAttribute.CreateExtension("binary", CloudEventAttributeType.Binary),
            CloudEventAttribute.CreateExtension("boolean", CloudEventAttributeType.Boolean),
            CloudEventAttribute.CreateExtension("integer", CloudEventAttributeType.Integer),
            CloudEventAttribute.CreateExtension("string", CloudEventAttributeType.String),
            CloudEventAttribute.CreateExtension("timestamp", CloudEventAttributeType.Timestamp),
            CloudEventAttribute.CreateExtension("uri", CloudEventAttributeType.Uri),
            CloudEventAttribute.CreateExtension("urireference", CloudEventAttributeType.UriReference)
        }.AsReadOnly();

        /// <summary>
        /// Arbitrary binary data to be used for testing.
        /// Calling code should not rely on the exact value.
        /// </summary>
        internal static byte[] SampleBinaryData { get; } = new byte[] { 1, 2, 3 };

        /// <summary>
        /// The base64 representation of <see cref="SampleBinaryData"/>.
        /// </summary>
        internal static string SampleBinaryDataBase64 { get; } = Convert.ToBase64String(SampleBinaryData);

        /// <summary>
        /// Arbitrary timestamp to be used for testing.
        /// Calling code should not rely on the exact value.
        /// </summary>
        internal static DateTimeOffset SampleTimestamp { get; } = new DateTimeOffset(2021, 2, 19, 12, 34, 56, 789, TimeSpan.FromHours(1));

        /// <summary>
        /// The RFC 3339 text representation of <see cref="SampleTimestamp"/>.
        /// </summary>
        internal static string SampleTimestampText { get; } = "2021-02-19T12:34:56.789+01:00";

        /// <summary>
        /// Arbitrary absolute URI to be used for testing.
        /// Calling code should not rely on the exact value.
        /// </summary>
        internal static Uri SampleUri { get; } = new Uri("https://absoluteuri/path");

        /// <summary>
        /// The textual representation of <see cref="SampleUri"/>.
        /// </summary>
        internal static string SampleUriText { get; } = "https://absoluteuri/path";

        /// <summary>
        /// Arbitrary absolute URI to be used for testing.
        /// Calling code should not rely on the exact value.
        /// </summary>
        internal static Uri SampleUriReference { get; } = new Uri("//urireference/path", UriKind.RelativeOrAbsolute);

        /// <summary>
        /// The textual representation of <see cref="SampleUriReference"/>.
        /// </summary>
        internal static string SampleUriReferenceText { get; } = "//urireference/path";

        /// <summary>
        /// Populates a CloudEvent with minimal valid attribute values.
        /// Calling code should not take a dependency on the exact values used.
        /// </summary>
        /// <returns>The original CloudEvent reference, for method chaining purposes.</returns>
        internal static CloudEvent PopulateRequiredAttributes(this CloudEvent cloudEvent)
        {
            cloudEvent.Id = "test-id";
            cloudEvent.Source = new Uri("//test", UriKind.RelativeOrAbsolute);
            cloudEvent.Type = "test-id";
            return cloudEvent;
        }

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
