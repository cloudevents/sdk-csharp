// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{

    internal class JTokenAsserter : IEnumerable
    {
        private readonly List<(string name, JTokenType type, object value)> expectations = new List<(string, JTokenType, object)>();

        // Just for collection initializers
        public IEnumerator GetEnumerator() => throw new NotImplementedException();

        public void Add<T>(string name, JTokenType type, T value) =>
            expectations.Add((name, type, value));

        public void AssertProperties(JObject obj, bool assertCount)
        {
            foreach (var expectation in expectations)
            {
                Assert.True(
                    obj.TryGetValue(expectation.name, out var token),
                    $"Expected property '{expectation.name}' to be present");
                Assert.Equal(expectation.type, token.Type);
                // No need to check null values, as they'll have a null token type.
                if (expectation.value is object)
                {
                    Assert.Equal(expectation.value, token.ToObject(expectation.value.GetType()));
                }
            }
            if (assertCount)
            {
                Assert.Equal(expectations.Count, obj.Count);
            }
        }
    }
}
