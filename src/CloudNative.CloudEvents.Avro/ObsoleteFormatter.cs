// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;

namespace CloudNative.CloudEvents;

/// <summary>
/// Formatter that implements the Avro Event Format.
/// </summary>
/// <remarks>
/// This class is the wrong namespace, and is only present for backward compatibility reasons.
/// Please use CloudNative.CloudEvents.Avro.AvroEventFormatter instead
/// (which this class derives from for convenience).
/// </remarks>
[Obsolete("This class is the wrong namespace, and is only present for backward compatibility reasons. Please use CloudNative.CloudEvents.Avro.AvroEventFormatter.")]
public class AvroEventFormatter : Avro.AvroEventFormatter
{
}
