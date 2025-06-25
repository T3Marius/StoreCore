using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;

namespace StoreCore;

public class WeaponModels : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] WeaponModels";
    public override string ModuleVersion => "1.0.0";
    public PluginConfig Config { get; set; } = new PluginConfig();
    public IStoreAPI StoreApi = null!;
    public static WeaponModels Instance { get; set; } = new WeaponModels();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        Instance = this;
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found.");
        Config = StoreApi.GetModuleConfig<PluginConfig>("WeaponModels");

        RegisterItems();

        RegisterListener<Listeners.OnServerPrecacheResources>(OnModelPrecache);
        RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
        RegisterEventHandler<EventItemEquip>(EventItemEquip);

        StoreApi.OnPlayerPurchaseItem += OnPurchaseItem;
        StoreApi.OnPlayerEquipItem += OnEquipItem;
        StoreApi.OnPlayerSellItem += OnSellItem;
        StoreApi.OnPlayerUnequipItem += OnUnequipItem;
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    private void OnSellItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (player == null)
            return;

        var weaponModel = FindWeaponModelById(item["uniqueid"]);
        if (weaponModel != null)
        {
            Server.NextFrame(() => Model.Unequip(player, weaponModel.Id));
        }
    }

    private HookResult EventItemEquip(EventItemEquip @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player == null)
            return HookResult.Continue;

        CBasePlayerWeapon? activeWeapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

        if (activeWeapon == null)
            return HookResult.Continue;

        string globalname = activeWeapon.Globalname;

        if (!string.IsNullOrEmpty(globalname) && globalname.Contains(','))
            Model.SetViewModel(player, globalname.Split(',')[1]);

        return HookResult.Continue;
    }

    private void OnEntityCreated(CEntityInstance entity)
    {
        if (!entity.DesignerName.StartsWith("weapon_"))
            return;

        CBasePlayerWeapon weapon = entity.As<CBasePlayerWeapon>();

        Server.NextWorldUpdate(() =>
        {
            if (!weapon.IsValid || weapon.OriginalOwnerXuidLow <= 0)
                return;

            CCSPlayerController? player = Utilities.GetPlayerFromSteamId(weapon.OriginalOwnerXuidLow);

            if (player == null)
                return;

            var equippedItems = StoreApi.GetPlayerItems(player.SteamID, Config.Category);
            if (equippedItems == null)
                return;

            string weaponDesignerName = Model.GetDesignerName(weapon);
            CBasePlayerWeapon? activeWeapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

            foreach (var item in equippedItems)
            {
                var weaponModel = FindWeaponModelById(item.UniqueId);
                if (weaponModel != null)
                {
                    var weaponpart = weaponModel.Model.Split(':');
                    if (weaponpart.Length < 2)
                        continue;

                    string weaponName = weaponpart[0];
                    string weaponModelPath = weaponpart[1];
                    string worldModel = weaponpart.Length == 3 ? weaponpart[2] : weaponpart[1];

                    if (weaponDesignerName == weaponName)
                    {
                        Model.UpdateModel(player, weapon, weaponModelPath, worldModel, weapon == activeWeapon);
                        break;
                    }
                }
            }
        });
    }

    private void OnUnequipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (player == null)
            return;

        var weaponModel = FindWeaponModelById(item["uniqueid"]);
        if (weaponModel != null && item["team"] == player.TeamNum.ToString())
        {
            Server.NextFrame(() => Model.Unequip(player, weaponModel.Id));
        }
    }

    private void OnEquipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (player == null)
            return;

        var weaponModel = FindWeaponModelById(item["uniqueid"]);
        if (weaponModel != null && item["team"] == player.TeamNum.ToString())
        {
            Server.NextFrame(() => Model.Equip(player, weaponModel.Id));
        }
    }

    private void OnPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (player == null)
            return;

        var weaponModel = FindWeaponModelById(item["uniqueid"]);
        if (weaponModel != null)
        {
            Server.NextFrame(() => Model.Equip(player, weaponModel.Id));
        }
    }

    private WeaponModel_Item? FindWeaponModelById(string id)
    {
        return Config.WeaponModels.Values.FirstOrDefault(w => w.Id == id);
    }

    private void OnModelPrecache(ResourceManifest resource)
    {
        foreach (var model in Config.WeaponModels.Values)
        {
            if (!string.IsNullOrEmpty(model.Model))
            {
                var parts = model.Model.Split(':');
                if (parts.Length >= 2)
                {
                    resource.AddResource(parts[1]);
                    if (parts.Length == 3)
                        resource.AddResource(parts[2]);
                }
            }
        }
    }

    private void RegisterItems()
    {
        foreach (var weapon in Config.WeaponModels.Values)
        {
            StoreApi.RegisterItem(
                weapon.Id,
                weapon.Name,
                Config.Category,
                weapon.Type,
                weapon.Price,
                weapon.Description,
                weapon.Flags,
                duration: weapon.Duration
            );
        }
    }
    private void UnregisterItems()
    {
        foreach (var weapon in Config.WeaponModels.Values)
        {
            StoreApi.UnregisterItem(weapon.Id);
        }
    }
}

public class PluginConfig
{
    public string Category { get; set; } = "Weapon Models";
    public Dictionary<string, WeaponModel_Item> WeaponModels { get; set; } = new()
    {
        {
            "1", new WeaponModel_Item
            {
                Id = "knife_model",
                Name = "Morrow Mind Knife",
                Description = "A knife model , cool one!",
                Price = 500,
                Duration = 0,
                Type = "weaponmodel",
                Flags = "",
                Model = "weapon_knife:models/weapons/nozb1/knife/morrowind/morrowind.vmdl"
            }
        },
        {
            "2", new WeaponModel_Item
            {
                Id = "ak47_model",
                Name = "Custom AK47 Model",
                Description = "A custom AK47 model",
                Price = 1000,
                Duration = 0,
                Type = "weaponmodel",
                Flags = "",
                Model = "weapon_ak47:models/weapons/nozb1/ak4x/weapon_ak4x.vmdl"
            }
        }
    };
}

public class WeaponModel_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; } = new int();
    public int Duration { get; set; } = new int();
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}