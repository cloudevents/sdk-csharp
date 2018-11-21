using System;
using System.Collections.Generic;
using System.Text;

namespace CloudNative.CloudEvents
{
    using System.Linq;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonEventFormatter
    {
        public static CloudEvent FromJson(string jsonText, params ICloudEventExtension[] extensions)
        {
            var jo = JObject.Parse(jsonText);
            var ce = new CloudEvent(extensions);
            var attr = ce.GetAttributes();
            foreach (var prop in jo)
            {
                switch (prop.Value.Type)
                {
                    case JTokenType.String:
                        attr[prop.Key] = prop.Value.ToObject<string>();
                        break;
                    case JTokenType.Date:
                        attr[prop.Key] = prop.Value.ToObject<DateTime>();
                        break;
                    case JTokenType.Uri:
                        attr[prop.Key] = prop.Value.ToObject<Uri>();
                        break;
                    case JTokenType.Null:
                        attr[prop.Key] = null;
                        break;
                    case JTokenType.Integer:
                        attr[prop.Key] = prop.Value.ToObject<int>();
                        break;
                    default:
                        attr[prop.Key] = (dynamic)prop.Value;
                        break;
                }
            }
            return ce;
        }

        public static string ToJsonString(this CloudEvent cloudEvent)
        {
            JObject jo = new JObject();
            var attrs = cloudEvent.GetAttributes();
            foreach (var attr in attrs)
            {
                if (attr.Value is ContentType)
                {
                    jo[attr.Key] = JToken.FromObject(((ContentType)attr.Value).ToString());
                }                                                                          
                else
                {
                    jo[attr.Key] = JToken.FromObject(attr.Value);
                }
            }
            return jo.ToString();
        }
    }
}
