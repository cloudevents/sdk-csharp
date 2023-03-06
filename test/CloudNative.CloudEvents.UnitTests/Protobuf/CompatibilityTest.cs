// Copyright 2023 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.V1;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests.Protobuf;

public class CompatibilityTest
{
    [Fact]
    public void ProtoSchemaReflectionEquivalence()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Same(CloudeventsReflection.Descriptor, ProtoSchemaReflection.Descriptor);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
