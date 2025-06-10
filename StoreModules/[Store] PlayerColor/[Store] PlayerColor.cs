using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using StoreAPI;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Timers;

namespace StoreCore;

public class PlayerColor : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] PlayerColor";
    public override string ModuleVersion => "1.0.0";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public Dictionary<int, Timer> RainbowTimer { get; set; } = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("PlayerColor");

        RegisterItems();

        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterListener<OnClientDisconnect>(OnClientDisconnect);

        StoreApi.OnPlayerEquipItem += OnPlayerEquipItem;
        StoreApi.OnPlayerUnequipItem += OnPlayerUnequipItem;
        StoreApi.OnPlayerSellItem += OnPlayerSellItem;
        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var cfg in Config.PlayerColors.Values)
        {
            if (item["uniqueid"] == cfg.Id)
            {
                if (cfg.Color == "Rainbow" || cfg.Color == "rainbow")
                {
                    StartRainbowEffect(player, pawn);
                }
                else
                {
                    SetColor(pawn, cfg.Color);
                }
            }
        }
    }
    public void OnPlayerSellItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var cfg in Config.PlayerColors.Values)
        {
            if (item["uniqueid"] == cfg.Id)
            {
                if (cfg.Color == "Rainbow" || cfg.Color == "rainbow")
                {
                    StopRainbow(player.Slot);
                }
                pawn.Render = Color.FromArgb(255, 255, 255, 255);
                Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
            }
        }
    }
    public void OnPlayerUnequipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var cfg in Config.PlayerColors.Values)
        {
            if (item["uniqueid"] == cfg.Id)
            {
                if (item["team"] == player.TeamNum.ToString())
                {
                    if (cfg.Color == "Rainbow" || cfg.Color == "rainbow")
                    {
                        StopRainbow(player.Slot);
                    }
                    pawn.Render = Color.FromArgb(255, 255, 255, 255);
                    Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }
    public void OnPlayerEquipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var cfg in Config.PlayerColors.Values)
        {
            if (item["uniqueid"] == cfg.Id)
            {
                if (item["team"] == player.TeamNum.ToString())
                {
                    if (cfg.Color == "Rainbow" || cfg.Color == "rainbow")
                    {
                        StartRainbowEffect(player, pawn);
                    }
                    else
                    {
                        SetColor(pawn, cfg.Color);
                    }
                }
            }
        }
    }
    public void OnClientDisconnect(int slot)
    {
        CCSPlayerController? player = Utilities.GetPlayerFromSlot(slot);
        if (player == null)
            return;

        StopRainbow(slot);
    }
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player == null)
            return HookResult.Continue;

        StopRainbow(player.Slot);

        return HookResult.Continue;
    }
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || player.IsBot || player.IsHLTV || StoreApi == null)
            return HookResult.Continue;

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return HookResult.Continue;

        foreach (var cfg in Config.PlayerColors.Values)
        {
            if (StoreApi.IsItemEquipped(player.SteamID, cfg.Id, player.TeamNum))
            {
                if (cfg.Color == "Rainbow" || cfg.Color == "rainbow")
                {
                    StartRainbowEffect(player, pawn);
                }
                else
                {
                    SetColor(pawn, cfg.Color);
                }
            }
        }

        return HookResult.Continue;
    }
    public void SetColor(CCSPlayerPawn pawn, string color)
    {
        pawn.Render = Color.FromName(color);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }
    public void SetRaibow(CCSPlayerPawn pawn, int r = 256, int g = 255, int b = 255)
    {
        pawn.Render = Color.FromArgb(255, r, b, g);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }
    public void StartRainbowEffect(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        StopRainbow(player.Slot);

        Random rnd = new Random();
        Timer timer = AddTimer(0.5f, () =>
        {
            if (pawn != null && pawn.IsValid)
                SetRaibow(pawn, rnd.Next(0, 255), rnd.Next(0, 255), rnd.Next(0, 255));
        }, TimerFlags.REPEAT);

        RainbowTimer[player.Slot] = timer;
    }
    public void StopRainbow(int playerSlot)
    {
        if (RainbowTimer.TryGetValue(playerSlot, out Timer? timer))
        {
            timer.Kill();
            RainbowTimer.Remove(playerSlot);
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.PlayerColors)
        {
            var color = kvp.Value;

            StoreApi.RegisterItem(
                color.Id,
                color.Name,
                Config.Category,
                color.Type,
                color.Price,
                color.Description,
                color.Flags,
                duration: color.Duration
            );
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.PlayerColors)
        {
            var color = kvp.Value;

            StoreApi.UnregisterItem(color.Id);
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Player Color";
    public Dictionary<string, Player_Color> PlayerColors { get; set; } = new Dictionary<string, Player_Color>()
    {
        {
            "1", new Player_Color
            {
                Id = "red_player",
                Name = "Red",
                Color = "Red",
                Description = "Turns your character red",
                Flags = string.Empty,
                Type = "playercolor",
                Price = 2500,
                Duration = 0
            }
        },
        {
            "2", new Player_Color
            {
                Id = "rainbow_player",
                Name = "Rainbow",
                Color = "Rainbow",
                Description = "Turns your character rainbow",
                Flags = string.Empty,
                Type = "playercolor",
                Price = 5000,
                Duration = 0
            }
        }
    };
}
public class Player_Color
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
}