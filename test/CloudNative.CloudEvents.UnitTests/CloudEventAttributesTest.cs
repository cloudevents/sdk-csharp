// Copyright 2020 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventAttributesTest
    {
        static readonly IEnumerable<ICloudEventExtension> emptyExtensions = new ICloudEventExtension[0];

        [Fact]
        public void Indexer_SetToNullValue_RegularAttribute()
        {
            var attributes = new CloudEventAttributes(CloudEventsSpecVersion.Default, emptyExtensions);
            string attributeName = CloudEventAttributes.TypeAttributeName();
            attributes[attributeName] = "some event type";
            attributes[attributeName] = null;
            Assert.Null(attributes[attributeName]);
        }

        [Fact]
        public void Indexer_SetToNullValue_SpecVersion()
        {
            var attributes = new CloudEventAttributes(CloudEventsSpecVersion.Default, emptyExtensions);
            string attributeName = CloudEventAttributes.SpecVersionAttributeName();
            Assert.Throws<InvalidOperationException>(() => attributes[attributeName] = null);
        }
    }
}
