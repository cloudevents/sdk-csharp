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
        // Note: this is a bit more specialized than the versoin in the framework, to make defaulting simpler to handle.
        internal static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : class =>
            dictionary.TryGetValue(key, out var value) ? value : default(TValue?);
    }
}
