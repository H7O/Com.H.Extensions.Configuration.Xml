using Microsoft.Extensions.Configuration;
namespace Com.H.Extensions.Configuration.Xml;
public static class ConfigurationExtensions
{
    public static void Save(this IConfiguration configuration)
    {
        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers)
            {
                if (provider is XmlWritableConfigurationProvider xmlProvider)
                {
                    xmlProvider.Save();
                }
            }
        }
    }

    public static void SetWithCData(this IConfiguration configuration, string key, string value)
    {
        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers)
            {
                if (provider is XmlWritableConfigurationProvider xmlProvider)
                {
                    xmlProvider.SetWithCData(key, value);
                }
            }
        }
    }
}
