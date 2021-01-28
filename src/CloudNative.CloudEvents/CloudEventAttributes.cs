// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.Mime;

    /// <summary>
    /// The CloudEvents attributes
    /// </summary>
    public class CloudEventAttributes : IDictionary<string, object>
    {
        private static readonly List<Func<CloudEventsSpecVersion, string>> attributeNameMethods = new List<Func<CloudEventsSpecVersion, string>>
        {
            DataAttributeName,
            DataContentTypeAttributeName,
            DataSchemaAttributeName,
            IdAttributeName,
            SourceAttributeName,
            SpecVersionAttributeName,
            SubjectAttributeName,
            TimeAttributeName,
            TypeAttributeName,
        };

        readonly CloudEventsSpecVersion specVersion;

        IDictionary<string, object> dict = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        IEnumerable<ICloudEventExtension> extensions;

        internal CloudEventAttributes(CloudEventsSpecVersion specVersion, IEnumerable<ICloudEventExtension> extensions)
        {
            this.extensions = extensions;
            this.specVersion = specVersion;
            dict[SpecVersionAttributeName(specVersion)] = SpecVersionString(specVersion);
        }

        int ICollection<KeyValuePair<string, object>>.Count => dict.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => dict.IsReadOnly;

        ICollection<string> IDictionary<string, object>.Keys => dict.Keys;

        ICollection<object> IDictionary<string, object>.Values => dict.Values;

        public CloudEventsSpecVersion SpecVersion => specVersion;

        // TODO: Consider exposing publicly.
        internal CloudEventAttributes WithSpecVersion(CloudEventsSpecVersion newVersion)
        {
            var newAttributes = new CloudEventAttributes(newVersion, extensions);
            foreach (var kv in dict)
            {
                // The constructor will have populated the spec version, so we can skip it.
                if (!kv.Key.Equals(SpecVersionAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
                {
                    string newAttributeName = ConvertAttributeName(kv.Key, SpecVersion, newVersion);
                    newAttributes[newAttributeName] = kv.Value;
                }
            }
            return newAttributes;
        }

        private static string SpecVersionString(CloudEventsSpecVersion version) =>
            version switch
            {
                CloudEventsSpecVersion.V0_1 => "0.1",
                CloudEventsSpecVersion.V0_2 => "0.2",
                CloudEventsSpecVersion.V0_3 => "0.3",
                CloudEventsSpecVersion.V1_0 => "1.0",
                _ => throw new ArgumentOutOfRangeException($"Unknown spec version: {version}")
            };

        private static string ConvertAttributeName(string name, CloudEventsSpecVersion fromVersion, CloudEventsSpecVersion toVersion)
        {
            foreach (var method in attributeNameMethods)
            {
                if (name.Equals(method(fromVersion), StringComparison.InvariantCultureIgnoreCase))
                {
                    return method(toVersion);
                }
            }
            return name;
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
                // Allow the "setting" of the spec version so long as it doesn't actually modify anything.
                if (key.Equals(SpecVersionAttributeName(SpecVersion), StringComparison.InvariantCultureIgnoreCase) && !Equals(dict[key], value))
                {
                    throw new InvalidOperationException(Strings.ErrorSpecVersionCannotBeModified);
                }

                if (value is null)
                {
                    dict.Remove(key);
                    return;
                }

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
                   (version == CloudEventsSpecVersion.V0_2 || version == CloudEventsSpecVersion.V0_3 ? "schemaurl" : "dataschema");
        }

        public static string SourceAttributeName(CloudEventsSpecVersion version = CloudEventsSpecVersion.Default)
        {
            return "source";
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
            // Note: can't throw ArgumentNullException as the null value is only part of the argument.
            if (value is null)
            {
                throw new InvalidOperationException(Strings.ErrorCannotAddNullAttributeValue);
            }
            ValidateAndNormalize(item.Key, ref value);
            dict.Add(item.Key, value);
        }

        void IDictionary<string, object>.Add(string key, object value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), Strings.ErrorCannotAddNullAttributeValue);
            }
            ValidateAndNormalize(key, ref value);
            dict.Add(key, value);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            // Clearing the collection doesn't remove the spec version attribute.
            // Preserve it, clear the dictionary, then put it back.
            string specAttributeName = SpecVersionAttributeName(this.SpecVersion);
            string specAttributeValue = (string) this[specAttributeName];
            dict.Clear();
            dict[specAttributeName] = specAttributeValue;
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
            if (item.Key.Equals(SpecVersionAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException(Strings.ErrorSpecVersionCannotBeModified);
            }
            return dict.Remove(item);
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            if (key.Equals(SpecVersionAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException(Strings.ErrorSpecVersionCannotBeModified);
            }
            return dict.Remove(key);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return dict.TryGetValue(key, out value);
        }

        private bool ValidateAndNormalize(string key, ref object value)
        {
            if (key.Equals(TypeAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorTypeValueIsNotAString);
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
                if (value is DateTimeOffset)
                {
                    return true;
                }

                if (value is string)
                {
                    if (Timestamps.TryParse((string)value, out var result))
                    {
                        value = result;
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
            else if (key.Equals(DataContentTypeAttributeName(this.SpecVersion), StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is ContentType)
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