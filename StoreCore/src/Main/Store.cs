using T3MenuSharedApi;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using StoreAPI;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.Json;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace StoreCore;

public class StoreCore : BasePlugin, IPluginConfig<StoreConfig>
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Core";
    public override string ModuleVersion => "1.1.5";
    public static StoreCore Instance { get; set; } = new StoreCore();
    public StoreConfig Config { get; set; } = new StoreConfig();
    public Dictionary<ulong, int> PlayerCredits { get; set; } = new Dictionary<ulong, int>();
    public Dictionary<ulong, int> PlayerRoundCredits { get; set; } = new Dictionary<ulong, int>();
    public static StoreAPI STORE_API { get; set; } = new();
    private IT3MenuManager? MenuManager;
    private Timer? _cleanupTimer;
    private string _configDirectory = string.Empty;

    private TimeSpan CleanupInterval => TimeSpan.FromMinutes(Config.Cleanup.CleanupIntervalMinutes);

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

        _configDirectory = Path.GetDirectoryName(configPath) ?? "";

        Database.Initialize();
        InitializeStore(hotReload);
    }
    private void InitializeStore(bool hotReload)
    {
        try
        {
            Events.Initialize();

            Commands.Initialize();

            Item.Initialize();

            if (hotReload)
            {
                Server.NextFrame(() =>
                {
                    StorePlayer.Load();
                });
            }

            StorePlayer.StartCreditsAward();

            StartCleanupTimer();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error initializing plugin components: {0}", ex.Message);
        }
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        if (hotReload)
        {
            Server.NextFrame(() =>
            {
                StorePlayer.Load();
            });
        }
    }

    public override void Unload(bool hotReload)
    {
        _cleanupTimer?.Kill();
        base.Unload(hotReload);
    }

    private void StartCleanupTimer()
    {
        if (!Config.Cleanup.EnableAutoCleanup)
        {
            Logger.LogInformation("Automatic cleanup is disabled in configuration");
            return;
        }

        _cleanupTimer = AddTimer((float)CleanupInterval.TotalSeconds, async () =>
        {
            await CleanupOrphanedItemsAsync();
        }, TimerFlags.REPEAT);

        Logger.LogInformation("Started item cleanup timer - will check for orphaned items every {0} minutes", CleanupInterval.TotalMinutes);
    }

    public async Task CleanupOrphanedItemsAsync()
    {
        try
        {
            if (Config.Cleanup.LogOrphanedItems)
            {
                Logger.LogInformation("Starting cleanup of orphaned items...");
            }

            var allItems = await Database.GetAllItemsAsync();
            if (allItems == null || allItems.Count == 0)
            {
                if (Config.Cleanup.LogOrphanedItems)
                {
                    Logger.LogInformation("No items found in database to check");
                }
                return;
            }

            var activeModuleItems = await GetActiveModuleItemsAsync();

            var orphanedItems = allItems.Where(item => !activeModuleItems.Contains(item.UniqueId)).ToList();

            if (orphanedItems.Count == 0)
            {
                if (Config.Cleanup.LogOrphanedItems)
                {
                    Logger.LogInformation("No orphaned items found");
                }
                return;
            }

            Logger.LogInformation("Found {0} orphaned items. Cleaning up...", orphanedItems.Count);

            foreach (var orphanedItem in orphanedItems)
            {
                bool result = await Database.UnregisterItemAsync(orphanedItem.UniqueId);
                if (result)
                {
                    if (Config.Cleanup.LogOrphanedItems)
                    {
                        Logger.LogInformation("Removed orphaned item: {0} (ID: {1})", orphanedItem.Name, orphanedItem.UniqueId);
                    }
                }
                else
                {
                    Logger.LogWarning("Failed to remove orphaned item: {0} (ID: {1})", orphanedItem.Name, orphanedItem.UniqueId);
                }
            }

            Logger.LogInformation("Cleanup completed. Removed {0} orphaned items", orphanedItems.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during orphaned items cleanup: {0}", ex.Message);
        }
    }

    private async Task<HashSet<string>> GetActiveModuleItemsAsync()
    {
        var activeItems = new HashSet<string>();

        try
        {
            string modulesConfigPath = Path.Combine(_configDirectory, "Modules");
            if (!Directory.Exists(modulesConfigPath))
            {
                Logger.LogWarning("Modules config directory not found: {0}", modulesConfigPath);
                return activeItems;
            }
            var configFiles = Directory.GetFiles(modulesConfigPath, "*.toml");

            foreach (string configFile in configFiles)
            {
                try
                {
                    await ExtractItemIdsFromConfigAsync(configFile, activeItems);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Error processing config file {0}: {1}", configFile, ex.Message);
                }
            }

            Logger.LogInformation("Found {0} active items across all module configs", activeItems.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error getting active module items: {0}", ex.Message);
        }

        return activeItems;
    }

    private async Task ExtractItemIdsFromConfigAsync(string configFilePath, HashSet<string> activeItems)
    {
        try
        {
            using var fs = new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string content = await sr.ReadToEndAsync();


            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                var idPatterns = new[]
                {
                    @"Id\s*=\s*""([^""]+)""",
                    @"id\s*=\s*""([^""]+)""",
                    @"UniqueId\s*=\s*""([^""]+)""",
                    @"uniqueid\s*=\s*""([^""]+)""",
                    @"ItemId\s*=\s*""([^""]+)""",
                    @"itemid\s*=\s*""([^""]+)"""
                };

                foreach (var pattern in idPatterns)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(trimmedLine, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string itemId = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(itemId))
                            {
                                activeItems.Add(itemId);

                                if (Config.Cleanup.LogOrphanedItems)
                                {
                                    Logger.LogDebug("Found item ID '{0}' in config: {1}", itemId, Path.GetFileName(configFilePath));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Error reading config file {0}: {1}", configFilePath, ex.Message);
        }
    }
}