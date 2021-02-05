// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Extension methods on collections. Some of these already exist in newer
    /// framework versions.
    /// </summary>
    internal static class CollectionExtensions
    {
        internal static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue)) =>
            dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }
}
