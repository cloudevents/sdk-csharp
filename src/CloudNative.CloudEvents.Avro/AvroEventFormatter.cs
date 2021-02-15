// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Avro;
using Avro.Generic;
using Avro.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Formatter that implements the Avro Event Format
    /// </summary>
    public class AvroEventFormatter : CloudEventFormatter
    {
        private const string DataName = "data";
        private static readonly RecordSchema avroSchema;
        private static readonly DefaultReader avroReader;
        private static readonly DefaultWriter avroWriter;
        
        static AvroEventFormatter()
        {
            // we're going to confidently assume that the embedded schema works. If not, type initialization
            // will fail and that's okay since the type is useless without the proper schema
            using (var sr = new StreamReader(typeof(AvroEventFormatter).Assembly.GetManifestResourceStream("CloudNative.CloudEvents.Avro.AvroSchema.json")))
            {
                avroSchema = (RecordSchema)RecordSchema.Parse(sr.ReadToEnd());
            }
            avroReader = new DefaultReader(avroSchema, avroSchema);
            avroWriter = new DefaultWriter(avroSchema);
        }
        public const string MediaTypeSuffix = "+avro";

        public CloudEvent DecodeStructuredEvent(Stream data, params CloudEventAttribute[] extensionAttributes) =>
            DecodeStructuredEvent(data, (IEnumerable<CloudEventAttribute>)extensionAttributes);

        // FIXME: We shouldn't use synchronous stream methods...
        public override Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            Task.FromResult(DecodeStructuredEvent(data, extensionAttributes));

        public override CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var decoder = new Avro.IO.BinaryDecoder(data);
            var rawEvent = avroReader.Read<GenericRecord>(null, decoder);
            return DecodeGenericRecord(rawEvent, extensionAttributes);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params CloudEventAttribute[] extensionAttributes) =>
            DecodeStructuredEvent(data, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        public override CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeStructuredEvent(new MemoryStream(data), extensionAttributes);

        public CloudEvent DecodeGenericRecord(GenericRecord record, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            if (!record.TryGetValue("attribute", out var attrObj))
            {
                return null;
            }
            IDictionary<string, object> recordAttributes = (IDictionary<string, object>)attrObj;

            CloudEventsSpecVersion specVersion = CloudEventsSpecVersion.Default;
            if (recordAttributes.TryGetValue(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var versionId) &&
                versionId is string versionIdString)
            {
                specVersion = CloudEventsSpecVersion.FromVersionId(versionIdString);
            }
            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
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

            return cloudEvent;
        }

        public override byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType(CloudEvent.MediaType+AvroEventFormatter.MediaTypeSuffix);

            GenericRecord record = new GenericRecord(avroSchema);
            record.Add(DataName, SerializeData(cloudEvent.Data));
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
            record.Add("attribute", recordAttributes);
            MemoryStream memStream = new MemoryStream();
            BinaryEncoder encoder = new BinaryEncoder(memStream);
            avroWriter.Write(record, encoder);
            return new Span<byte>(memStream.GetBuffer(), 0, (int)memStream.Length).ToArray();
        }

        /// <summary>
        /// Convert data into a suitable format for inclusion in an Avro record.
        /// TODO: Asynchronous version of this...
        /// </summary>
        private static object SerializeData(object data)
        {
            if (data is Stream stream)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            return data;
        }

        // TODO: Validate that this is correct...
        public override byte[] EncodeData(object value) =>
            throw new NotSupportedException("The Avro event formatter does not support binary content mode");

        public override object DecodeData(byte[] value, string contentType) =>
            throw new NotSupportedException("The Avro event formatter does not support binary content mode");
    }
}