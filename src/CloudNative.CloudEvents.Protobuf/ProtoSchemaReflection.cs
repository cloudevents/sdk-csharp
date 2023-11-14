// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Google.Protobuf.Reflection;
using System;

namespace CloudNative.CloudEvents.V1;

/// <summary>
/// Access to reflection information for the CloudEvents protobuf schema.
/// </summary>
/// <remarks>
/// This class exists for backward-compatibility, when the protobuf messages
/// were generated with a file named ProtoSchema.proto instead of cloudevents.proto.
/// </remarks>
[Obsolete($"Use {nameof(CloudeventsReflection)} instead.")]
public class ProtoSchemaReflection
{
    /// <summary>File descriptor for cloudevents.proto</summary>
    public static FileDescriptor Descriptor => CloudeventsReflection.Descriptor;
}
