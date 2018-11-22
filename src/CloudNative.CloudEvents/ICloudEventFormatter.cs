// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System.IO;
    using System.Net.Mime;


    public interface ICloudEventFormatter
    {
        CloudEvent DecodeStructuredEvent(Stream data, params ICloudEventExtension[] extensions);
        CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions);
        byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType);
        object DecodeAttribute(string name, byte[] data);
        byte[] EncodeAttribute(string name, object value);
    }
}