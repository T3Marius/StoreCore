using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;
using StoreAPI;

namespace StoreCore;

public class Flags : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Flags";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Flags");

        RegisterItems();

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
        StoreApi.OnPlayerItemExpired += OnPlayerItemExpired;

    }
    public void OnPlayerItemExpired(CCSPlayerController player, Dictionary<string, string> Item)
    {
        foreach (var kvp in Config.Flags)
        {
            var flag = kvp.Value;

            if (Item["uniqueid"] == flag.Id)
            {
                AdminManager.RemovePlayerPermissions(player, flag.Flag);
                Logger.LogInformation("Removed {flag} Flag from {playername}", flag.Flag, player.PlayerName);
            }
        }
    }
    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> Item)
    {
        foreach (var kvp in Config.Flags)
        {
            var flag = kvp.Value;

            if (Item["uniqueid"] == flag.Id)
            {
                AdminManager.AddPlayerPermissions(player, flag.Flag);
                Logger.LogInformation("Added {flag} Flag to {playername}", flag.Flag, player.PlayerName);
            }
        }
    }
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;



        if (player == null || StoreApi == null)
            return HookResult.Continue;

        foreach (var kvp in Config.Flags)
        {
            var flag = kvp.Value;

            if (StoreApi.PlayerHasItem(player.SteamID, flag.Id))
            {
                AdminManager.AddPlayerPermissions(player, flag.Flag);
            }
        }

        return HookResult.Continue;
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    private void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Flags)
        {
            var flag = kvp.Value;

            StoreApi.RegisterItem(
                flag.Id,
                flag.Name,
                Config.Category,
                flag.Type,
                flag.Price,
                flag.Description,
                flag.Flags,
                duration: flag.Duration,
                isEquipable: false
                );
        }
    }
    private void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Flags)
        {
            var flag = kvp.Value;

            StoreApi.UnregisterItem(flag.Id);
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Flags";
    public Dictionary<string, Flag_Item> Flags { get; set; } = new Dictionary<string, Flag_Item>()
    {
        {
            "1", new Flag_Item
            {
                Id = "slot_flag",
                Name = "Slot Flag",
                Flag = "@css/slot",
                Description = "Gives you slot flag acces",
                Flags = "",
                Type = "Flags",
                Price = 2500,
                Duration = 0,

            }
        }
    };
}
public class Flag_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
}