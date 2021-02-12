// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.Text;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
    // FIXME: This doesn't get called for binary CloudEvents without content, or with a different data content type.
    // FIXME: This shouldn't really be tied to JSON. We need to work out how we actually want this to be used.
    // See 

    public class CloudEventJsonInputFormatter : TextInputFormatter
    {
        private readonly ICloudEventFormatter _formatter;

        public CloudEventJsonInputFormatter(ICloudEventFormatter formatter)
        {
            _formatter = formatter;
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cloudevents+json"));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            var request = context.HttpContext.Request;

            try
            {
                var cloudEvent = await request.ReadCloudEventAsync(_formatter);
                return await InputFormatterResult.SuccessAsync(cloudEvent);
            }
            catch (Exception)
            {
                return await InputFormatterResult.FailureAsync();
            }
        }

        protected override bool CanReadType(Type type)
        {
            if (type == typeof(CloudEvent))
            {
                return base.CanReadType(type);
            }

            return false;
        }
    }
}
