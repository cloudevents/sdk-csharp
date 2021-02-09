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

        internal static void CheckArgument(bool condition, string paramName, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        internal static void CheckArgument(bool condition, string paramName, string messageFormat,
            object arg1)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(messageFormat, arg1), paramName);
            }
        }

        internal static void CheckArgument(bool condition, string paramName, string messageFormat,
            object arg1, object arg2)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(messageFormat, arg1, arg2), paramName);
            }
        }
    }
}
