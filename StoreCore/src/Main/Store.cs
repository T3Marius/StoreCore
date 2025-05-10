using T3MenuSharedApi;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using StoreAPI;
using CounterStrikeSharp.API;
using System.Reflection;
using System.IO;
using Microsoft.Extensions.Logging;

namespace StoreCore;

public class StoreCore : BasePlugin, IPluginConfig<StoreConfig>
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Core";
    public override string ModuleVersion => "1.0.2";
    public static StoreCore Instance { get; set; } = new StoreCore();
    public StoreConfig Config { get; set; } = new StoreConfig();
    public Dictionary<ulong, int> PlayerCredits { get; set; } = new Dictionary<ulong, int>();
    public static StoreAPI STORE_API { get; set; } = new();
    private IT3MenuManager? MenuManager;
    private bool _isHotReload = false;

    public IT3MenuManager? GetMenuManager()
    {
        if (MenuManager == null)
            MenuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get() ?? throw new Exception("Couldn't find t3menuapi");
        return MenuManager;
    }

    public void OnConfigParsed(StoreConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Instance = this;
        _isHotReload = hotReload;

        string configPath = Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "plugins", "StoreCore", "StoreCore.toml");
        STORE_API.SetConfigPath(configPath);
        Capabilities.RegisterPluginCapability(IStoreAPI.Capability, () => STORE_API);

        Database.Initialize();


        InitializeStore(hotReload);
        
        
    }

    private void InitializeStore(bool hotReload)
    {
        try
        {
            Events.Initialize();

            Commands.Initialize();

            if (!hotReload)
            {
                Item.Initialize();
            }
            else
            {

                Server.NextFrame(() =>
                {
                    StorePlayer.Load();
                });
            }

            StorePlayer.StartCreditsAward();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error initializing plugin components: {ex.Message}");
        }
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        if (hotReload)
        {
            Server.NextFrame(() => {
                StorePlayer.Load();
            });
        }
    }
}