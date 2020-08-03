// Copyright 2020 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.Mime;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventTest
    {
        [Fact]
        public void SetAttributePropertiesToNull()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0, "type",
                new Uri("https://source"), "subject", "id", DateTime.UtcNow)
            {
                Data = "some data",
                DataContentType = new ContentType("text/plain"),
                DataSchema = new Uri("https://schema")
            };

            cloudEvent.Type = null;
            cloudEvent.Source = null;
            cloudEvent.Subject = null;
            cloudEvent.Id = null;
            cloudEvent.Time = null;
            cloudEvent.Data = null;
            cloudEvent.DataContentType = null;
            cloudEvent.DataSchema = null;

            Assert.Null(cloudEvent.Type);
            Assert.Null(cloudEvent.Source);
            Assert.Null(cloudEvent.Subject);
            Assert.Null(cloudEvent.Id);
            Assert.Null(cloudEvent.Time);
            Assert.Null(cloudEvent.Data);
            Assert.Null(cloudEvent.DataContentType);
            Assert.Null(cloudEvent.DataSchema);
        }
    }
}
