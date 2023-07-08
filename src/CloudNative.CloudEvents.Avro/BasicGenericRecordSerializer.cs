// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Avro.Generic;
using Avro.IO;
using CloudNative.CloudEvents.Avro.Interfaces;
using System;
using System.IO;

namespace CloudNative.CloudEvents.Avro;

/// <summary>
/// The default implementation of the <see cref="IGenericRecordSerializer"/>.
/// </summary>
/// <remarks>
/// Makes use of the Avro <see cref="DefaultReader"/> and <see cref="DefaultWriter"/>
/// together with the embedded Avro schema.
/// </remarks>
internal sealed class BasicGenericRecordSerializer : IGenericRecordSerializer
{
    private readonly DefaultReader avroReader;
    private readonly DefaultWriter avroWriter;

    public BasicGenericRecordSerializer()
    {
        avroReader = new DefaultReader(AvroEventFormatter.AvroSchema, AvroEventFormatter.AvroSchema);
        avroWriter = new DefaultWriter(AvroEventFormatter.AvroSchema);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(GenericRecord record)
    {
        var memStream = new MemoryStream();
        var encoder = new BinaryEncoder(memStream);
        avroWriter.Write(record, encoder);
        return memStream.ToArray();
    }

    /// <inheritdoc />
    public GenericRecord Deserialize(Stream rawMessagebody)
    {
        var decoder = new BinaryDecoder(rawMessagebody);
        // The reuse parameter *is* allowed to be null...
        return avroReader.Read<GenericRecord>(reuse: null!, decoder);
    }
}
