

namespace StoreAPI
{
    public interface IStoreConfig
    {
        T LoadConfig<T>(string moduleName) where T : class, new();
        void SaveConfig<T>(string moduleName, T config) where T : class, new();
    }
}
