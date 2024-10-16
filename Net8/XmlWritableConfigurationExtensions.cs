using Microsoft.Extensions.Configuration;
namespace Com.H.Extensions.Configuration.Xml;
public static class XmlWritableConfigurationExtensions
{
    public static IConfigurationBuilder AddXmlFileWithSave(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        bool reloadOnChange = false)
    {
        return builder.Add(new XmlWritableConfigurationSource
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange
        });
    }
}
