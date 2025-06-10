using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;
using Microsoft.Extensions.Logging;

namespace StoreCore;

public class PlayerModels : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] PlayerModels";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("PlayerModels");

        RegisterItems();

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
        StoreApi.OnPlayerUnequipItem += OnPlayerUnequipItem;
        StoreApi.OnPlayerEquipItem += OnPlayerEquipItem;
        StoreApi.OnPlayerItemExpired += OnPlayerItemExpired;
        StoreApi.OnPlayerSellItem += OnPlayerSellItem;

        RegisterListener<OnServerPrecacheResources>((manifest) =>
        {
            foreach (var kvp in Config.PlayerModels)
            {
                var playerModel = kvp.Value;

                manifest.AddResource(playerModel.ModelPath);
            }
        });
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player == null || StoreApi == null)
            return HookResult.Continue;

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        if (pawn != null)
        {
            foreach (var kvp in Config.PlayerModels)
            {
                var playerModel = kvp.Value;

                if (StoreApi.IsItemEquipped(player.SteamID, playerModel.Id, pawn.TeamNum))
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel(playerModel.ModelPath);
                    });
                }
                break;
            }
        }
        return HookResult.Continue;
    }
    public void OnPlayerUnequipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var playerModel = kvp.Value;

            if (item["uniqueid"] == playerModel.Id)
            {
                if (item["team"] == player.TeamNum.ToString())
                {
                    if (player.TeamNum == 2)
                    {
                        Server.NextFrame(() =>
                        {
                            pawn.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");
                        });
                    }
                    else if (player.TeamNum == 3)
                    {
                        Server.NextFrame(() =>
                        {
                            pawn.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
                        });
                    }
                }
                break;
            }

        }
    }


    public void OnPlayerEquipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var playerModel = kvp.Value;

            if (item["uniqueid"] == playerModel.Id)
            {
                if (item["team"] == player.TeamNum.ToString())
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel(playerModel.ModelPath);
                    });
                }
                break;
            }
        }
    }

    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null || StoreApi == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var playerModel = kvp.Value;

            if (item["uniqueid"] == playerModel.Id)
            {
                Server.NextFrame(() =>
                {
                    pawn.SetModel(playerModel.ModelPath);
                });
                break;
            }
        }
    }
    public void OnPlayerItemExpired(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var playerModel = kvp.Value;

            if (item["uniqueid"] == playerModel.Id)
            {
                if (player.Team == CsTeam.Terrorist)
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");
                    });
                }
                else if (player.Team == CsTeam.CounterTerrorist)
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
                    });
                }
                break;
            }
        }
    }
    public void OnPlayerSellItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var playerModel = kvp.Value;

            if (item["uniqueid"] == playerModel.Id)
            {
                if (player.Team == CsTeam.Terrorist)
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");
                    });
                }
                else if (player.Team == CsTeam.CounterTerrorist)
                {
                    Server.NextFrame(() =>
                    {
                        pawn.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
                    });
                }
                break;
            }
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var model = kvp.Value;

            StoreApi.RegisterItem(
                model.Id,
                model.Name,
                Config.Category,
                model.Type,
                model.Price,
                model.Description,
                model.Flags,
                duration: model.Duration
            );
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.PlayerModels)
        {
            var model = kvp.Value;

            StoreApi.UnregisterItem(model.Id);
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Player Models";
    public Dictionary<string, PlayerModel_Item> PlayerModels { get; set; } = new Dictionary<string, PlayerModel_Item>()
    {
        {
            "1", new PlayerModel_Item
            {
                Id = "frogman",
                Name = "FrogMan",
                ModelPath = "characters/models/ctm_diver/ctm_diver_variantb.vmdl",
                Description = "Gives you a in-game model",
                Flags = "",
                Type = "PlayerModel",
                Price = 3500,
                Duration = 86000
            }
        }
    };
}
public class PlayerModel_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
}
