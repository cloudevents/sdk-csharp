// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.Net.Http.Headers;

    public class CloudEventInputFormatter : TextInputFormatter
    {
        public CloudEventInputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cloudevents"));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
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
                var cloudEvent = request.ToCloudEvent();
                return InputFormatterResult.SuccessAsync(cloudEvent);
            }
            catch (Exception)
            {
                return InputFormatterResult.FailureAsync();
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
