namespace CloudNative.CloudEvents
{
    public interface ICloudEventExtension
    {
        bool ValidateAndNormalize(string key, ref dynamic value);
        void Attach(CloudEvent cloudEvent);
    }
}