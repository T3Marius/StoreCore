using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using StoreAPI;

namespace StoreCore;

public class Killscreen : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[StoreCore] Killscreen";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Killscreen");

        if (!hotReload)
        {
            foreach (var kvp in Config.Killscreens)
            {
                var killScreen = kvp.Value;

                StoreApi.RegisterItem(
                    killScreen.Id,
                    killScreen.Name,
                    Config.Category,
                    killScreen.Type,
                    killScreen.Price,
                    killScreen.Description,
                    killScreen.Flags,
                    duration: killScreen.Duration);
            }
        }
        StoreApi.OnItemPreview += OnItemPreview;
    }
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        if (victim == null || attacker == null || victim == attacker)
            return HookResult.Continue;

        CCSPlayerPawn? attackerPawn = attacker.PlayerPawn.Value;

        if (StoreApi == null || attackerPawn == null)
            return HookResult.Continue;

        foreach (var kvp in Config.Killscreens)
        {
            var killScreen = kvp.Value;
            if (StoreApi.IsItemEquipped(attacker.SteamID, killScreen.Id, attacker.TeamNum))
            {
                attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                break;
            }
        }

        return HookResult.Continue;
    }
    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.Killscreens)
        {
            var killScreen = kvp.Value;
            if (uniqueId == killScreen.Id)
            {
                pawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                break;
            }
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Special Effects";
    public Dictionary<string, Killscreen_Item> Killscreens { get; set; } = new Dictionary<string, Killscreen_Item>()
    {
        {
            "1", new Killscreen_Item
            {
                Id = "killscreen_2_minutes",
                Name = "Killscreen (2) minutes",
                Price = 500,
                Duration = 120,
                Type = "Visual",
                Description = "",
                Flags = ""
            }
        },
    };
}
public class Killscreen_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
}