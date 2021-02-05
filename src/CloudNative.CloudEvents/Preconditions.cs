// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Convenient precondition methods.
    /// </summary>
    internal static class Preconditions
    {
        internal static T CheckNotNull<T>(T value, string paramName) where T : class =>
            value ?? throw new ArgumentNullException(paramName);
    }
}
