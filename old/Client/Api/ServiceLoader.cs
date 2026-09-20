using MarcoZechner.ApiLib;
using MarcoZechner.ConfigAPI.Shared.Api;

namespace MarcoZechner.ConfigAPI.Client.Api
{
    public static class ServiceLoader
    {
        public static bool ApiLoaded => _bridge != null && _bridge.ApiLoaded;

        private static ApiConsumerBridge _bridge;
        private static ConfigService _service;

        public static ConfigService Service => ApiLoaded ? _service : null;

        public static void Init(ulong modId, string modName)
        {
            _bridge = new ApiConsumerBridge(
                modId,
                modName,
                new ConfigApiBootstrap(),
                new ConfigUserHooksImpl(),
                SetMainApi
            );

            _bridge.Init();
        }

        public static void Unload()
        {
            _bridge?.Unload();
            _bridge = null;

            _service = null;
        }

        private static void SetMainApi(IApiProvider mainApi) => _service = new ConfigService(mainApi);
    }
}