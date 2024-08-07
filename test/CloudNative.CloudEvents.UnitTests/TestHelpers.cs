// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    /// <summary>
    /// Helpers for test code, e.g. common non-trivial assertions.
    /// </summary>
    internal static class TestHelpers
    {
        internal static IEqualityComparer<DateTimeOffset> InstantOnlyTimestampComparer => EqualityComparer<DateTimeOffset>.Default;
        internal static IEqualityComparer<DateTimeOffset> StrictTimestampComparer => StrictTimestampComparerImpl.Instance;

        internal static CloudEventAttribute[] EmptyExtensionArray { get; } = new CloudEventAttribute[0];
        internal static IEnumerable<CloudEventAttribute> EmptyExtensionSequence { get; } = new List<CloudEventAttribute>().AsReadOnly();

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
        internal static string SampleBinaryDataBase64 { get; } = Convert.ToBase64String(SampleBinaryData); // AQID

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
            cloudEvent.Type = "test-type";
            return cloudEvent;
        }

        /// <summary>
        /// Creates a batch of two CloudEvents, one of which has (plain text) content.
        /// </summary>
        internal static List<CloudEvent> CreateSampleBatch()
        {
            var event1 = new CloudEvent().PopulateRequiredAttributes();
            event1.Id = "event1";
            event1.Data = "simple text";
            event1.DataContentType = "text/plain";

            var event2 = new CloudEvent().PopulateRequiredAttributes();
            event2.Id = "event2";

            return new List<CloudEvent> { event1, event2 };
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
                Assert.Fail("Expected both values to be null, or neither to be null");
            }
            AssertTimestampsEqual(expected!.Value, actual!.Value);
        }

        // TODO: Use this more widely
        // TODO: Document handling of timestamps, and potentially parameterize it.
        internal static void AssertCloudEventsEqual(CloudEvent expected, CloudEvent actual,
            IEqualityComparer<DateTimeOffset>? timestampComparer = null,
            IEqualityComparer<object?>? dataComparer = null)
        {
            timestampComparer ??= StrictTimestampComparer;
            Assert.Equal(expected.SpecVersion, actual.SpecVersion);
            var expectedAttributes = expected.GetPopulatedAttributes().ToList();
            var actualAttributes = actual.GetPopulatedAttributes().ToList();

            Assert.Equal(expectedAttributes.Count, actualAttributes.Count);
            foreach (var expectedAttribute in expectedAttributes)
            {
                var actualAttribute = actualAttributes.FirstOrDefault(actual => actual.Key.Name == expectedAttribute.Key.Name);
                Assert.NotNull(actualAttribute.Key);

                Assert.Equal(expectedAttribute.Key.Type, actualAttribute.Key.Type);
                if (expectedAttribute.Value is DateTimeOffset expectedDto &&
                    actualAttribute.Value is DateTimeOffset actualDto)
                {
                    Assert.Equal(expectedDto, actualDto, timestampComparer);
                }
                else
                {
                    Assert.Equal(expectedAttribute.Value, actualAttribute.Value);
                }
            }
            Assert.Equal(expected.Data, actual.Data, dataComparer ?? EqualityComparer<object?>.Default);
        }

        internal static void AssertBatchesEqual(IReadOnlyList<CloudEvent> expectedBatch, IReadOnlyList<CloudEvent> actualBatch,
            IEqualityComparer<DateTimeOffset>? timestampComparer = null,
            IEqualityComparer<object?>? dataComparer = null)
        {
            Assert.Equal(expectedBatch.Count, actualBatch.Count);
            foreach (var pair in expectedBatch.Zip(actualBatch, (x, y) => (x, y)))
            {
                AssertCloudEventsEqual(pair.x, pair.y, timestampComparer, dataComparer);
            }
        }

        /// <summary>
        /// Loads the resource with the given name, copying it into a MemoryStream.
        /// (That's often easier to work with when debugging.)
        /// </summary>
        internal static MemoryStream LoadResource(string resource)
        {
            using var stream = typeof(TestHelpers).Assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                throw new ArgumentException($"Resource {resource} is missing. Known resources: {string.Join(", ", typeof(TestHelpers).Assembly.GetManifestResourceNames())}");
            }
            var output = new MemoryStream();
            stream.CopyTo(output);
            output.Position = 0;
            return output;
        }

        private class StrictTimestampComparerImpl : IEqualityComparer<DateTimeOffset>
        {
            internal static StrictTimestampComparerImpl Instance { get; } = new StrictTimestampComparerImpl();

            private StrictTimestampComparerImpl()
            {
            }

            public bool Equals(DateTimeOffset x, DateTimeOffset y) =>
                x.UtcDateTime == y.UtcDateTime &&
                x.Offset == y.Offset;

            public int GetHashCode([DisallowNull] DateTimeOffset obj) =>
                obj.UtcDateTime.GetHashCode() ^ obj.Offset.GetHashCode();
        }
    }
}
