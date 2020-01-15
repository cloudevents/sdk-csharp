// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using Avro;
    using Avro.Generic;
    using Avro.IO;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Mime;
    using System.Threading.Tasks;
                                                               
    /// <summary>
    /// Formatter that implements the JSON Event Format
    /// </summary>
    public class AvroEventFormatter : ICloudEventFormatter
    {
        static readonly RecordSchema avroSchema;
        static readonly DefaultReader avroReader;
        static readonly DefaultWriter avroWriter;
        
        static AvroEventFormatter()
        {
            // we're going to confidently assume that the embedded schema works. If not, type initialization
            // will fail and that's okay since the type is useless without the proper schema
            using ( var sr = new StreamReader(typeof(AvroEventFormatter).Assembly.GetManifestResourceStream("CloudNative.CloudEvents.Avro.AvroSchema.json")))
            {
                avroSchema = (RecordSchema)RecordSchema.Parse(sr.ReadToEnd());
            }
            avroReader = new DefaultReader(avroSchema, avroSchema);
            avroWriter = new DefaultWriter(avroSchema);
        }
        public const string MediaTypeSuffix = "+avro";

        public CloudEvent DecodeStructuredEvent(Stream data, params ICloudEventExtension[] extensions)
        {
            return DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);
        }

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<ICloudEventExtension> extensions)
        {
            return DecodeStructuredEvent(data, extensions);
        }

        public CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<ICloudEventExtension> extensions = null)
        {
            var decoder = new Avro.IO.BinaryDecoder(data);
            var rawEvent = avroReader.Read<GenericRecord>(null, decoder);
            return DecodeGenericRecord(rawEvent, extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions)
        {
            return DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions = null)
        {
            return DecodeStructuredEvent(new MemoryStream(data), extensions);
        }

        public CloudEvent DecodeGenericRecord(GenericRecord record, IEnumerable<ICloudEventExtension> extensions = null)
        {
            if (!record.TryGetValue("attribute", out var attrObj))
            {
                return null;
            }
            IDictionary<string, object> recordAttributes = (IDictionary<string, object>)attrObj;
            object data = null;
            if (!record.TryGetValue("data", out data))
            {
                data = null;
            }

            CloudEventsSpecVersion specVersion = CloudEventsSpecVersion.Default;
            var cloudEvent = new CloudEvent(specVersion, extensions);
            cloudEvent.Data = data;

            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in recordAttributes)
            {
                // skip the version since we set that above
                if (keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1), StringComparison.InvariantCultureIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2), StringComparison.InvariantCultureIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V1_0), StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                if (keyValuePair.Key == CloudEventAttributes.SourceAttributeName() ||
                    keyValuePair.Key == CloudEventAttributes.DataSchemaAttributeName())
                {
                    attributes[keyValuePair.Key] = new Uri((string)keyValuePair.Value);
                }
                else if (keyValuePair.Key == CloudEventAttributes.TimeAttributeName())
                {
                    attributes[keyValuePair.Key] = DateTime.Parse((string)keyValuePair.Value);
                }
                else
                {
                    attributes[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            return cloudEvent;
        }

        public byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType(CloudEvent.MediaType+AvroEventFormatter.MediaTypeSuffix);

            GenericRecord record = new GenericRecord(avroSchema);
            var recordAttributes = new Dictionary<string, object>();
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in attributes)
            {
                if (keyValuePair.Value == null)
                {
                    continue;
                }

                if (keyValuePair.Value is ContentType && !string.IsNullOrEmpty(((ContentType)keyValuePair.Value).MediaType))
                {
                    recordAttributes[keyValuePair.Key] = ((ContentType)keyValuePair.Value).ToString();
                }
                else if (keyValuePair.Value is Uri)
                {
                    recordAttributes[keyValuePair.Key] = ((Uri)keyValuePair.Value).ToString();
                }
                else if (keyValuePair.Value is DateTime)
                {
                    recordAttributes[keyValuePair.Key] = ((DateTime)keyValuePair.Value).ToString("o");
                }
                else if (cloudEvent.SpecVersion == CloudEventsSpecVersion.V1_0 &&
                         keyValuePair.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)))
                {
                    if (keyValuePair.Value is Stream)
                    {
                        using (var sr = new BinaryReader((Stream)keyValuePair.Value))
                        {
                            record.Add("data", sr.ReadBytes((int)sr.BaseStream.Length));
                        }
                    }
                    else
                    {
                        record.Add("data", keyValuePair.Value);
                    }
                }
                else
                {
                    recordAttributes[keyValuePair.Key] = keyValuePair.Value;
                }
            }
            record.Add("attribute", recordAttributes);
            MemoryStream memStream = new MemoryStream();
            BinaryEncoder encoder = new BinaryEncoder(memStream);
            avroWriter.Write(record, encoder);
            return new Span<byte>(memStream.GetBuffer(), 0, (int)memStream.Length).ToArray();
        }

        public object DecodeAttribute(CloudEventsSpecVersion specVersion, string name, byte[] data, IEnumerable<ICloudEventExtension> extensions = null)
        {
            throw new NotSupportedException("Encoding invidual attributes is not supported for Apache Avro");
        }

        public byte[] EncodeAttribute(CloudEventsSpecVersion specVersion, string name, object value, IEnumerable<ICloudEventExtension> extensions = null)
        {
            throw new NotSupportedException("Encoding invidual attributes is not supported for Apache Avro");
        }
    }
}