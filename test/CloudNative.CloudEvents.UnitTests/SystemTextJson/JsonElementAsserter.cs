// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests
{
    internal class JsonElementAsserter : IEnumerable
    {
        private readonly List<(string name, JsonValueKind type, object? value)> expectations = new List<(string, JsonValueKind, object?)>();

        // Just for collection initializers
        public IEnumerator GetEnumerator() => throw new NotImplementedException();

        public void Add<T>(string name, JsonValueKind type, T value) =>
            expectations.Add((name, type, value));

        public void AssertProperties(JsonElement obj, bool assertCount)
        {
            foreach (var expectation in expectations)
            {
                Assert.True(
                    obj.TryGetProperty(expectation.name, out var property),
                    $"Expected property '{expectation.name}' to be present");
                Assert.Equal(expectation.type, property.ValueKind);
                // No need to check null values, as they'll have a null token type.
                if (expectation.value is object)
                {
                    var value = property.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => property.GetString(),
                        JsonValueKind.Number => property.GetInt32(),
                        JsonValueKind.Null => (object?) null,
                        _ => throw new Exception($"Unhandled value kind: {property.ValueKind}")
                    };

                    Assert.Equal(expectation.value, value);
                }
            }
            if (assertCount)
            {
                Assert.Equal(expectations.Count, obj.EnumerateObject().Count());
            }
        }
    }
}