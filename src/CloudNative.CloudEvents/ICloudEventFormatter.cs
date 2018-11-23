// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Mime;


    public interface ICloudEventFormatter
    {
        CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<ICloudEventExtension> extensions);
        CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions);
        byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType, IEnumerable<ICloudEventExtension> extensions);
        object DecodeAttribute(string name, byte[] data, IEnumerable<ICloudEventExtension> extensions);
        byte[] EncodeAttribute(string name, object value, IEnumerable<ICloudEventExtension> extensions);
    }
}