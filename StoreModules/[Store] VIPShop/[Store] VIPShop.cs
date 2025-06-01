using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using StoreAPI;

namespace StoreCore;

public class VIPShop : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] VIPShop";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("VIPShop");

        RegisterItems();

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        foreach (var vip in Config.Vips.Values)
        {
            if (item["uniqueid"] == vip.Id)
            {
                string command = vip.Command.Replace("{steamid}", player.SteamID.ToString());
                Server.ExecuteCommand(command);
                Logger.LogInformation("Executed command {command} for {steamid}", command, player.SteamID);
            }
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Vips)
        {
            var vip = kvp.Value;

            StoreApi.RegisterItem(
                vip.Id,
                vip.Name,
                Config.Category,
                vip.Type,
                vip.Price,
                vip.Description,
                vip.Flags,
                isEquipable: false,
                isSellable: false
            );
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Vips)
        {
            var vip = kvp.Value;

            StoreApi.UnregisterItem(vip.Id);
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "VIP Shop";
    public Dictionary<string, Vip_Item> Vips { get; set; } = new Dictionary<string, Vip_Item>()
    {
        {
            "1", new Vip_Item
            {
                Id = "vip_2_days",
                Name = "Vip [2 Days]",
                Command = "css_vip_adduser {steamid} VIP 172800",
                Type = "vip",
                Description = "Gives you VIP for 2 days",
                Flags = "",
                Price = 5000,
            }
        }
    };
}
public class Vip_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
}