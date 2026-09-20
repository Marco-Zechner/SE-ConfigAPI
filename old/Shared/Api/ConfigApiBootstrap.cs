using MarcoZechner.ApiLib;

namespace MarcoZechner.ConfigAPI.Shared.Api
{
    public class ConfigApiBootstrap : ApiBootstrapConfig
    {
        public override string ApiLibVersion => "1.0.0";
        public override long DiscoveryChannel => 23456;
        public override string ApiProviderModId => "MarcoZechner.ConfigAPI";
        public override string ApiVersion => "0.1.0";
    }
}