// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Avro.Generic;
using CloudNative.CloudEvents.Avro.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;

namespace CloudNative.CloudEvents.UnitTests.Avro.Helpers;

internal class FakeGenericRecordSerializer : IGenericRecordSerializer
{
    public byte[]? SerializeResponse { get; private set; }
    public GenericRecord DeserializeResponse { get; private set; }
    public int DeserializeCalls { get; private set; } = 0;
    public int SerializeCalls { get; private set; } = 0;

    public FakeGenericRecordSerializer()
    {
        DeserializeResponse = new GenericRecord(CloudEvents.Avro.AvroEventFormatter.AvroSchema);
    }

    public GenericRecord Deserialize(Stream messageBody)
    {
        DeserializeCalls++;
        return DeserializeResponse;
    }

    public ReadOnlyMemory<byte> Serialize(GenericRecord value)
    {
        SerializeCalls++;
        return SerializeResponse;
    }

    public void SetSerializeResponse(byte[] response) => SerializeResponse = response;

    public void SetDeserializeResponseAttributes(string id, string type, string source) =>
        DeserializeResponse.Add("attribute", new Dictionary<string, object>()
        {
            { CloudEventsSpecVersion.SpecVersionAttribute.Name, CloudEventsSpecVersion.Default.VersionId },
            { CloudEventsSpecVersion.Default.IdAttribute.Name, id},
            { CloudEventsSpecVersion.Default.TypeAttribute.Name, type},
            { CloudEventsSpecVersion.Default.SourceAttribute.Name, source}
        });
}
