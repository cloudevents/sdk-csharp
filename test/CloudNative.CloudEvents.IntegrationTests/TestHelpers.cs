// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Linq;
using Xunit;

namespace CloudNative.CloudEvents.IntegrationTests
{
    /// <summary>
    /// Helpers for test code, e.g. common non-trivial assertions.
    /// </summary>
    internal static class TestHelpers
    {
        // TODO: Use this more widely
        internal static void AssertCloudEventsEqual(CloudEvent expected, CloudEvent actual)
        {
            Assert.Equal(expected.SpecVersion, actual.SpecVersion);
            var expectedAttributes = expected.GetPopulatedAttributes().ToList();
            var actualAttributes = actual.GetPopulatedAttributes().ToList();

            Assert.Equal(expectedAttributes.Count, actualAttributes.Count);
            foreach (var expectedAttribute in expectedAttributes)
            {
                var actualAttribute = actualAttributes.FirstOrDefault(actual => actual.Key.Name == expectedAttribute.Key.Name);
                Assert.NotNull(actualAttribute.Key);

                Assert.Equal(actualAttribute.Key.Type, expectedAttribute.Key.Type);
                Assert.Equal(actualAttribute.Value, expectedAttribute.Value);
            }
        }
    }
}
