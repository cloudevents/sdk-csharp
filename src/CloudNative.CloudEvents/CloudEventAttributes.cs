// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Mime;

    /// <summary>
    /// The CloudEvents attributes
    /// </summary>
    public class CloudEventAttributes : IDictionary<string, object>
    {
        public const string ContentTypeAttributeName = "contenttype";

        public const string DataAttributeName = "data";

        public const string IdAttributeName = "id";

        public const string SchemaUrlAttributeName = "schemaurl";

        public const string SourceAttributeName = "source";

        public const string SpecVersionAttributeName = "specversion";

        public const string TimeAttributeName = "time";

        public const string TypeAttributeName = "type";

        IDictionary<string, object> dict = new Dictionary<string, object>();
        
        IEnumerable<ICloudEventExtension> extensions;

        internal CloudEventAttributes(IEnumerable<ICloudEventExtension> extensions)
        {
            this.extensions = extensions;
        }

        int ICollection<KeyValuePair<string, object>>.Count => dict.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => dict.IsReadOnly;

        ICollection<string> IDictionary<string, object>.Keys => dict.Keys;

        ICollection<object> IDictionary<string, object>.Values => dict.Values;

        object IDictionary<string, object>.this[string key]
        {
            get => dict[key];
            set
            {
                ValidateAndNormalize(key, ref value);
                dict[key] = value;
            }
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
            switch (key)
            {
                case TypeAttributeName:
                    if (value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorTypeValueIsNotAString);
                case SpecVersionAttributeName:
                    if (value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorSpecVersionValueIsNotAString);
                case IdAttributeName:
                    if (value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorIdValueIsNotAString);
                case TimeAttributeName:
                    if (value is null || value is DateTime)
                    {
                        return true;
                    }

                    if (value is string)
                    {
                        if (DateTime.TryParseExact((string)value, "o", CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal, out var dateTimeVal))
                        {
                            value = dateTimeVal;
                            return true;
                        }
                    }

                    throw new InvalidOperationException(Strings.ErrorTimeValueIsNotATimestamp);
                case SourceAttributeName:
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

                case SchemaUrlAttributeName:
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
                case ContentTypeAttributeName:
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
                case DataAttributeName:
                    return true;
                default:
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

                    break;
            }

            return false;
        }
    }
}