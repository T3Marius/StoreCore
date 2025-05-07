using T3MenuSharedApi;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using StoreAPI;
using CounterStrikeSharp.API;
using System.Reflection;
using System.IO;

namespace StoreCore;

public class StoreCore : BasePlugin, IPluginConfig<StoreConfig>
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Core";
    public override string ModuleVersion => "1.0.0";
    public static StoreCore Instance { get; set; } = new StoreCore();
    public StoreConfig Config { get; set; } = new StoreConfig();
    public Dictionary<ulong, int> PlayerCredits { get; set; } = new Dictionary<ulong, int>();
    public static StoreAPI STORE_API { get; set; } = new();

    private bool isDatabaseCreated = false;
    private IT3MenuManager? MenuManager;
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

        string configPath = Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "plugins", "StoreCore", "StoreCore.toml");
        STORE_API.SetConfigPath(configPath);

        Capabilities.RegisterPluginCapability(IStoreAPI.Capability, () => STORE_API);

        if (!isDatabaseCreated)
        {
            Database.Initialize();
            isDatabaseCreated = true;
        }

        StorePlayer.StartCreditsAward();
        Events.Initialize();
        Commands.Initialize();

        if (hotReload)
        {
            StorePlayer.Load();
        }
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        Item.Initialize();
    }
}