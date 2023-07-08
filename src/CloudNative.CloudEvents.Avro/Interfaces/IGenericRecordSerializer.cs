using Avro.Generic;
using System;
using System.IO;

namespace CloudNative.CloudEvents.Avro.Interfaces;

/// <summary>
/// Used to serialize and deserialize an Avro <see cref="GenericRecord"/>
/// matching the <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/formats/cloudevents.avsc">
/// CloudEvent Avro schema</see>.
/// </summary>
/// <remarks>
/// <para>
/// An implementation of this interface can optionally be supplied to the <see cref="AvroEventFormatter"/> in cases
/// where a custom Avro serialiser is required for integration with pre-existing tools/infrastructure.
/// </para>
/// <para>
/// It is recommended to use the default serializer before defining your own wherever possible.
/// </para>
/// </remarks>
public interface IGenericRecordSerializer
{
    /// <summary>
    /// Serialize an Avro <see cref="GenericRecord"/>.
    /// </summary>
    /// <remarks>
    /// The record is guaranteed to match the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/formats/cloudevents.avsc">
    /// CloudEvent Avro schema</see>.
    /// </remarks>
    ReadOnlyMemory<byte> Serialize(GenericRecord value);

    /// <summary>
    /// Deserialize a <see cref="GenericRecord"/> matching the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/formats/cloudevents.avsc">
    /// CloudEvent Avro schema</see>, represented as a stream.
    /// </summary>
    GenericRecord Deserialize(Stream messageBody);
}
