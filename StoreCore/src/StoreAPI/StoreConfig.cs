using System.Text.Json;
using System.IO;
using System;
using StoreAPI;
using Microsoft.Extensions.Logging;

namespace StoreCore;

public class StoreModuleConfig : IStoreConfig
{
    private readonly string _configDirectory;
    private readonly string _modulesDirectory;

    public StoreModuleConfig(string baseConfigPath)
    {
        _configDirectory = Path.GetDirectoryName(baseConfigPath) ?? "";
        _modulesDirectory = Path.Combine(_configDirectory, "Modules");
    }

    public T LoadConfig<T>(string moduleName) where T : class, new()
    {
        string configPath = Path.Combine(_modulesDirectory, $"{moduleName}.json");
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
        string configPath = Path.Combine(_modulesDirectory, $"{moduleName}.json");
        try
        {
            Directory.CreateDirectory(_modulesDirectory);

            string jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(configPath, jsonContent);
        }
        catch (Exception ex)
        {
            StoreCore.Instance.Logger.LogError($"Failed to save module {moduleName} config: {ex.Message}");
        }
    }
}