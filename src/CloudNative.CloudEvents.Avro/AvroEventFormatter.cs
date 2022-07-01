// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Avro;
using Avro.Generic;
using Avro.IO;
using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;

namespace CloudNative.CloudEvents.Avro
{
    /// <summary>
    /// Formatter that implements the Avro Event Format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event formatter currently only supports structured-mode messages.
    /// </para>
    /// <para>
    /// When encoding a CloudEvent, the data must be serializable as described in the
    /// <a href="https://github.com/cloudevents/spec/blob/v1.0.1/avro-format.md#3-data">CloudEvents Avro Event
    /// Format specification</a>.
    /// </para>
    /// <para>
    /// When decoding a CloudEvent, the <see cref="CloudEvent.Data"/> property is populated directly from the
    /// Avro record, so the value will have the natural Avro deserialization type for that data (which may
    /// not be exactly the same as the type that was serialized).
    /// </para>
    /// <para>
    /// This event formatter does not infer any data content type.
    /// </para>
    /// </remarks>
    public class AvroEventFormatter : CloudEventFormatter
    {
        private const string MediaTypeSuffix = "+avro";
        private const string AttributeName = "attribute";
        private const string DataName = "data";

        private static readonly string CloudEventAvroMediaType = MimeUtilities.MediaType + MediaTypeSuffix;
        private static readonly RecordSchema avroSchema;
        private static readonly DefaultReader avroReader;
        private static readonly DefaultWriter avroWriter;
        
        static AvroEventFormatter()
        {
            // We're going to confidently assume that the embedded schema works. If not, type initialization
            // will fail and that's okay since the type is useless without the proper schema.
            using (var sr = new StreamReader(typeof(AvroEventFormatter).Assembly.GetManifestResourceStream("CloudNative.CloudEvents.Avro.AvroSchema.json")))
            {
                avroSchema = (RecordSchema) Schema.Parse(sr.ReadToEnd());
            }
            avroReader = new DefaultReader(avroSchema, avroSchema);
            avroWriter = new DefaultWriter(avroSchema);
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var decoder = new BinaryDecoder(body);
            // The reuse parameter *is* allowed to be null...
            var rawEvent = avroReader.Read<GenericRecord>(reuse: null!, decoder);
            return DecodeGenericRecord(rawEvent, extensionAttributes);
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            return DecodeStructuredModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            throw new NotSupportedException("The Avro event formatter does not support batch content mode");

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvent, out ContentType contentType) =>
            throw new NotSupportedException("The Avro event formatter does not support batch content mode");

        private CloudEvent DecodeGenericRecord(GenericRecord record, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            if (!record.TryGetValue(AttributeName, out var attrObj))
            {
                throw new ArgumentException($"Record has no '{AttributeName}' field");
            }
            IDictionary<string, object> recordAttributes = (IDictionary<string, object>)attrObj;

            if (!recordAttributes.TryGetValue(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var versionId) ||
                !(versionId is string versionIdString))
            {
                throw new ArgumentException("Specification version attribute is missing");
            }
            CloudEventsSpecVersion? version = CloudEventsSpecVersion.FromVersionId(versionIdString);
            if (version is null)
            {
                throw new ArgumentException($"Unsupported CloudEvents spec version '{versionIdString}'");
            }

            var cloudEvent = new CloudEvent(version, extensionAttributes);
            cloudEvent.Data = record.TryGetValue(DataName, out var data) ? data : null;

            foreach (var keyValuePair in recordAttributes)
            {
                string key = keyValuePair.Key;
                object value = keyValuePair.Value;
                if (value is null)
                {
                    continue;
                }

                if (key == CloudEventsSpecVersion.SpecVersionAttribute.Name || key == DataName)
                {
                    continue;
                }

                // The Avro schema allows the value to be a Boolean, integer, string or bytes.
                // Timestamps and URIs are represented as strings, so we just use SetAttributeFromString to handle those.
                // TODO: This does mean that any extensions of these types must have been registered beforehand.
                if (value is bool || value is int || value is byte[])
                {
                    cloudEvent[key] = value;
                }
                else if (value is string)
                {
                    cloudEvent.SetAttributeFromString(key, (string)value);
                }
                else
                {
                    throw new ArgumentException($"Invalid value type from Avro record: {value.GetType()}");
                }
            }

            return Validation.CheckCloudEventArgument(cloudEvent, nameof(record));
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            contentType = new ContentType(CloudEventAvroMediaType);

            // We expect the Avro encoded to detect data types that can't be represented in the schema.
            GenericRecord record = new GenericRecord(avroSchema);
            record.Add(DataName, cloudEvent.Data);
            var recordAttributes = new Dictionary<string, object>();
            recordAttributes[CloudEventsSpecVersion.SpecVersionAttribute.Name] = cloudEvent.SpecVersion.VersionId;

            foreach (var keyValuePair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = keyValuePair.Key;
                var value = keyValuePair.Value;
                // TODO: Create a mapping method in each direction, to have this logic more clearly separated.
                var avroValue = value is bool || value is int || value is byte[] || value is string
                    ? value
                    : attribute.Format(value);
                recordAttributes[attribute.Name] = avroValue;
            }
            record.Add(AttributeName, recordAttributes);
            MemoryStream memStream = new MemoryStream();
            BinaryEncoder encoder = new BinaryEncoder(memStream);
            avroWriter.Write(record, encoder);
            return memStream.ToArray();
        }

        // TODO: Validate that this is correct...
        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent) =>
            throw new NotSupportedException("The Avro event formatter does not support binary content mode");

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent) =>
            throw new NotSupportedException("The Avro event formatter does not support binary content mode");
    }
}