using Microsoft.Extensions.Configuration;
namespace Com.H.Extensions.Configuration.Xml;
public class XmlWritableConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new XmlWritableConfigurationProvider(this);
    }
}
