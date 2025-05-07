using System.Text.Json;
using StoreAPI;

namespace StoreCore;

public class StoreModuleConfig : IStoreConfig
{
    private readonly string _configDirectory;

    public StoreModuleConfig(string baseConfigPath)
    {
        _configDirectory = Path.GetDirectoryName(baseConfigPath) ?? "";
    }

    public T LoadConfig<T>(string moduleName) where T : class, new()
    {
        string configPath = Path.Combine(_configDirectory, $"{moduleName}.json");

        if (!File.Exists(configPath))
        {
            var defaultConfig = new T();
            SaveConfig(moduleName, defaultConfig);
            return defaultConfig;
        }

        try
        {
            string jsonContent = File.ReadAllText(configPath);
            T? config = JsonSerializer.Deserialize<T>(jsonContent);
            return config ?? new T();
        }
        catch (Exception)
        {
            return new T();
        }
    }

    public void SaveConfig<T>(string moduleName, T config) where T : class, new()
    {
        string configPath = Path.Combine(_configDirectory, $"{moduleName}.json");

        try
        {
            Directory.CreateDirectory(_configDirectory);

            string jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(configPath, jsonContent);
        }
        catch (Exception)
        {
        }
    }
}