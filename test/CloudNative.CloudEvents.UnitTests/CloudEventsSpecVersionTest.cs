// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventsSpecVersionTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("bogus")]
        [InlineData("1")]
        public void FromVersionId_Unknown(string versionId) =>
            Assert.Null(CloudEventsSpecVersion.FromVersionId(versionId));

        [Theory]
        [InlineData("1.0")]
        public void FromVersionId_Known(string versionId)
        {
            var version = CloudEventsSpecVersion.FromVersionId(versionId);
            Assert.NotNull(version);
            Assert.Equal(versionId, version.VersionId);
        }
    }
}
