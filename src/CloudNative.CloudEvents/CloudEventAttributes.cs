// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net.Mime;

    /// <summary>
    /// The CloudEvents attributes
    /// </summary>
    public class CloudEventAttributes : IDictionary<string, object>
    {
        readonly CloudEventsSpecVersion specVersion;

        IDictionary<string, object> dict = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        IEnumerable<ICloudEventExtension> extensions;

        internal CloudEventAttributes(CloudEventsSpecVersion specVersion, IEnumerable<ICloudEventExtension> extensions)
        {
            this.extensions = extensions;
            this.specVersion = specVersion;
            dict[SpecVersionAttributeName(specVersion)] = (specVersion == CloudEventsSpecVersion.V0_1 ? "0.1" :
                (specVersion == CloudEventsSpecVersion.V0_2 ? "0.2" : "1.0"));
        }

        int ICollection<KeyValuePair<string, object>>.Count => dict.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => dict.IsReadOnly;

        ICollection<string> IDictionary<string, object>.Keys => dict.Keys;

        ICollection<object> IDictionary<string, object>.Values => dict.Values;

        public CloudEventsSpecVersion SpecVersion
        {
            get
            {
                object val;
                if (dict.TryGetValue(SpecVersionAttributeName(CloudEventsSpecVersion.V0_1), out val) ||
                    dict.TryGetValue(SpecVersionAttributeName(CloudEventsSpecVersion.V0_2), out val) ||
                    dict.TryGetValue(SpecVersionAttributeName(CloudEventsSpecVersion.V1_0), out val))
                {
                    return (val as string) == "0.1" ? CloudEventsSpecVersion.V0_1 : 
                           (val as string) == "0.2" ? CloudEventsSpecVersion.V0_2 : CloudEventsSpecVersion.V1_0;

                }

                return CloudEventsSpecVersion.Default;
            }
            set
            {
                // this setter sets the version and initiates a transform to the new target version if
                // required. The transformation may fail under some circumstances where CloudEvents 
                // versions are in mutual conflict

                var currentSpecVersion = SpecVersion;
                object val;
                if (dict.TryGetValue(SpecVersionAttributeName(CloudEventsSpecVersion.V0_1), out val))
                {
                    if (value == CloudEventsSpecVersion.V0_1 && (val as string) == "0.1")
                    {
                        return;
                    }
                }
                else if ( dict.TryGetValue(SpecVersionAttributeName(), out val)) // 0.2 and 1.0 are the same
                {
                    if (value == CloudEventsSpecVersion.V0_2 && (val as string) == "0.2")
                    {
                        return;
                    }
                    if (value == CloudEventsSpecVersion.V1_0 && (val as string) == "1.0")
                    {
                        return;
                    }
                }                                                                      

                // transform to new version
                var copy = new Dictionary<string, object>(dict);
                dict.Clear();
                this[SpecVersionAttributeName(value)] = value == CloudEventsSpecVersion.V0_1 ? "0.1" : (value == CloudEventsSpecVersion.V0_2 ? "0.2" : "1.0");
                foreach (var kv in copy)
                {
                    if (SpecVersionAttributeName(CloudEventsSpecVersion.V0_2).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase) ||
                        SpecVersionAttributeName(CloudEventsSpecVersion.V0_1).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase) ||
                        SpecVersionAttributeName(CloudEventsSpecVersion.V1_0).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))  
                    {
                        continue;
                    }
                    if (DataContentTypeAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[DataContentTypeAttributeName(value)] = kv.Value;
                    }
                    else if (DataAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[DataAttributeName(value)] = kv.Value;
                    }
                    else if (IdAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[IdAttributeName(value)] = kv.Value;
                    }
                    else if (DataSchemaAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[DataSchemaAttributeName(value)] = kv.Value;
                    }
                    else if (SourceAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[SourceAttributeName(value)] = kv.Value;
                    }
                    else if (SubjectAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[SubjectAttributeName(value)] = kv.Value;
                    }
                    else if (TimeAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[TimeAttributeName(value)] = kv.Value;
                    }
                    else if (TypeAttributeName(currentSpecVersion).Equals(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this[TypeAttributeName(value)] = kv.Value;
                    }
                    else
                    {
                        this[kv.Key] = kv.Value;
                    }
                }
            }
        }

        public object this[string key]
        {
            get
            {
                if (!dict.TryGetValue(key, out var result))
                {
                    return null;
                }             
                return result;
            } 
            set
            {
                ValidateAndNormalize(key, ref value);
                dict[key] = value;
            }
        }

        public static string DataContentTypeAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "contentType" :
                (version == CloudEventsSpecVersion.V0_2 ? "contenttype" : "datacontenttype");
        }

        public static string DataAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return "data";
        }

        public static string IdAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "eventID" : "id";
        }

        public static string DataSchemaAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "schemaUrl" : 
                   (version == CloudEventsSpecVersion.V0_2 ? "schemaurl" : "dataschema");
        }

        public static string SourceAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return "source";
        }

        public static string SubjectAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return "subject";
        }

        public static string SpecVersionAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "cloudEventsVersion" : "specversion";
        }

        public static string SubjectAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return "subject";
        }

        public static string TimeAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "eventTime" : "time";
        }

        public static string TypeAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return version == CloudEventsSpecVersion.V0_1 ? "eventType" : "type";
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            object value = item.Value;
            ValidateAndNormalize(item.Key, ref value);
            dict.Add(item.Key, value);
        }

        void IDictionary<string, object>.Add(string key, object value)
        {
            ValidateAndNormalize(key, ref value);
            dict.Add(key, value);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            dict.Clear();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            return dict.Contains(item);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            return dict.ContainsKey(key);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            dict.CopyTo(array, arrayIndex);
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)dict).GetEnumerator();
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return dict.Remove(item);
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            return dict.Remove(key);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return dict.TryGetValue(key, out value);
        }

        internal virtual bool ValidateAndNormalize(string key, ref object value)
        {
            if (key.Equals(TypeAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorTypeValueIsNotAString);
            }
            else if (key.Equals(SpecVersionAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorSpecVersionValueIsNotAString);
            }
            else if (key.Equals(IdAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorIdValueIsNotAString);
            }
            else if (key.Equals(TimeAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is null || value is DateTime)
                {
                    return true;
                }

                if (value is string)
                {
                    if (DateTime.TryParse((string)value, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var dateTimeVal))
                    {
                        value = dateTimeVal;
                        return true;
                    }
                }

                throw new InvalidOperationException(Strings.ErrorTimeValueIsNotATimestamp);
            }
            else if (key.Equals(SourceAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is Uri)
                {
                    return true;
                }

                if (value is string)
                {
                    if (Uri.TryCreate((string)value, UriKind.RelativeOrAbsolute, out var uriVal))
                    {
                        value = uriVal;
                        return true;
                    }
                }

                throw new InvalidOperationException(Strings.ErrorSchemaUrlIsNotAUri);
            }
            else if (key.Equals(SubjectAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorSubjectValueIsNotAString);
            }
            else if (key.Equals(DataSchemaAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is null || value is Uri)
                {
                    return true;
                }

                if (value is string)
                {
                    if (Uri.TryCreate((string)value, UriKind.RelativeOrAbsolute, out var uriVal))
                    {
                        value = uriVal;
                        return true;
                    }
                }

                throw new InvalidOperationException(Strings.ErrorSchemaUrlIsNotAUri);
            }
            else if (key.Equals(DataContentTypeAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is null || value is ContentType)
                {
                    return true;
                }

                if (value is string)
                {
                    try
                    {
                        value = new ContentType((string)value);
                        return true;
                    }
                    catch (FormatException fe)
                    {
                        throw new InvalidOperationException(Strings.ErrorContentTypeIsNotRFC2046, fe);
                    }
                }

                throw new InvalidOperationException(Strings.ErrorContentTypeIsNotRFC2046);
            }
            else if (key.Equals(DataAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                if (extensions != null)
                {
                    foreach (var extension in extensions)
                    {
                        if (extension.ValidateAndNormalize(key, ref value))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}