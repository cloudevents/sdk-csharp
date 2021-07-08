// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace CloudNative.CloudEvents.Core.UnitTests
{
    public class CloudEventAttributeTypesTest
    {
        public static readonly TheoryData<CloudEventAttributeType> AllTypes = new TheoryData<CloudEventAttributeType>
        {
            CloudEventAttributeType.Binary,
            CloudEventAttributeType.Boolean,
            CloudEventAttributeType.Integer,
            CloudEventAttributeType.String,
            CloudEventAttributeType.Timestamp,
            CloudEventAttributeType.Uri,
            CloudEventAttributeType.UriReference
        };

        [Fact]
        public void GetOrdinal_NullInput() =>
            Assert.Throws<ArgumentNullException>(() => CloudEventAttributeTypes.GetOrdinal(null!));

        [Theory]
        [MemberData(nameof(AllTypes))]
        public void GetOrdinal_NonNullInput(CloudEventAttributeType type) =>
            Assert.Equal(type.Ordinal, CloudEventAttributeTypes.GetOrdinal(type));
    }
}
