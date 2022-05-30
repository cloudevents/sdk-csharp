// Copyright 2022 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Xunit;

namespace CloudNative.CloudEvents.Core.UnitTests
{
    public class BinaryDataUtilitiesTest
    {
        [Fact]
        public void AsArray_ReturningOriginal()
        {
            byte[] original = { 1, 2, 3, 4, 5 };
            var segment = new ArraySegment<byte>(original);
            Assert.Same(original, BinaryDataUtilities.AsArray(segment));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void AsArray_FromPartialSegment(int offset)
        {
            byte[] original = { 1, 2, 3, 4, 5 };
            var segment = new ArraySegment<byte>(original, offset, 4);
            var actual = BinaryDataUtilities.AsArray(segment);
            Assert.True(actual.SequenceEqual(segment));
            Assert.NotSame(original, actual);
        }
    }
}
