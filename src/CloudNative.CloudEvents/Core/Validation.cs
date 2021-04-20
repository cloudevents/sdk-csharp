// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Core
{
    /// <summary>
    /// Validation methods which are typically convenient for implementers of CloudEvent formatters
    /// and protocol bindings.
    /// </summary>
    public static class Validation
    {
        /// <summary>
        /// Validates that the given reference is non-null.
        /// </summary>
        /// <typeparam name="T">Type of the value to check</typeparam>
        /// <param name="value">The reference to check for nullity</param>
        /// <param name="paramName">The parameter name to use in the exception if <paramref name="value"/> is null.
        /// May be null.</param>
        /// <returns>The value of <paramref name="value"/>, for convenient method chaining or assignment.</returns>
        public static T CheckNotNull<T>(T value, string paramName) where T : class =>
            value ?? throw new ArgumentNullException(paramName);

        /// <summary>
        /// Validates an argument-dependent condition, throwing an exception if the check fails.
        /// </summary>
        /// <param name="condition">The condition to validate; this method will throw an <see cref="ArgumentException"/> if this is false.</param>
        /// <param name="paramName">The name of the parameter being validated. May be null.</param>
        /// <param name="message">The message to use in the exception, if one is thrown.</param>
        public static void CheckArgument(bool condition, string paramName, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>
        /// Validates an argument-dependent condition, throwing an exception if the check fails.
        /// </summary>
        /// <param name="condition">The condition to validate; this method will throw an <see cref="ArgumentException"/> if this is false.</param>
        /// <param name="paramName">The name of the parameter being validated. May be null.</param>
        /// <param name="messageFormat">The string format to use in the exception message, if one is thrown.</param>
        /// <param name="arg1">The first argument in the string format.</param>
        public static void CheckArgument(bool condition, string paramName, string messageFormat,
            object arg1)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(messageFormat, arg1), paramName);
            }
        }

        /// <summary>
        /// Validates an argument-dependent condition, throwing an exception if the check fails.
        /// </summary>
        /// <param name="condition">The condition to validate; this method will throw an <see cref="ArgumentException"/> if this is false.</param>
        /// <param name="paramName">The name of the parameter being validated. May be null.</param>
        /// <param name="messageFormat">The string format to use in the exception message, if one is thrown.</param>
        /// <param name="arg1">The first argument in the string format.</param>
        /// <param name="arg2">The first argument in the string format.</param>
        public static void CheckArgument(bool condition, string paramName, string messageFormat,
            object arg1, object arg2)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(messageFormat, arg1, arg2), paramName);
            }
        }

        /// <summary>
        /// Validates that the specified CloudEvent is valid in the same way as <see cref="CloudEvent.IsValid"/>,
        /// but throwing an <see cref="ArgumentException"/> using the given parameter name
        /// if the event is invalid. This is typically used within protocol bindings or event formatters
        /// as the last step in decoding an event, or as the first step when encoding an event.
        /// </summary
        /// <param name="cloudEvent">The event to validate.
        /// <param name="paramName">The parameter name to use in the exception if <paramref name="cloudEvent"/> is null or invalid.
        /// May be null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cloudEvent"/> is null.</exception>
        /// <exception cref="ArgumentException">The event is invalid.</exception>
        /// <returns>A reference to the same object, for simplicity of method chaining.</returns>
        public static CloudEvent CheckCloudEventArgument(CloudEvent cloudEvent, string paramName)
        {
            CheckNotNull(cloudEvent, paramName);
            if (cloudEvent.IsValid)
            {
                return cloudEvent;
            }
            var missing = cloudEvent.SpecVersion.RequiredAttributes.Where(attr => cloudEvent[attr] is null).ToList();
            string joinedMissing = string.Join(", ", missing);
            throw new ArgumentException($"CloudEvent is missing required attributes: {joinedMissing}", paramName);
        }

        /// <summary>
        /// Validates that the specified batch is valid, by asserting that it is non-null,
        /// and that it only contains non-null references to valid CloudEvents.
        /// </summary>
        /// <param name="cloudEvents">The event batch to validate.
        /// <param name="paramName">The parameter name to use in the exception if <paramref name="cloudEvent"/> is null or invalid.
        /// May be null.</param>
        public static void CheckCloudEventBatchArgument(IReadOnlyList<CloudEvent> cloudEvents, string paramName)
        {
            CheckNotNull(cloudEvents, paramName);
            foreach (var cloudEvent in cloudEvents)
            {
                CheckCloudEventArgument(cloudEvent, paramName);
            }
        }
    }
}
