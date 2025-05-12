using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;

namespace StoreCore;

public class SpawnEffects : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] SpawnEffects";
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
        Config = StoreApi.GetModuleConfig<PluginConfig>("SpawnEffects");

        if (!hotReload)
        {
            foreach (var kvp in Config.SpawnEffects)
            {
                var spawnEffect = kvp.Value;

                StoreApi.RegisterItem(
                    spawnEffect.Id,
                    spawnEffect.Name,
                    Config.Category,
                    spawnEffect.Type,
                    spawnEffect.Price,
                    spawnEffect.Description,
                    spawnEffect.Flags,
                    duration: spawnEffect.Duration);
            }
        }
    }
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || StoreApi == null)
            return HookResult.Continue;

        foreach (var kvp in Config.SpawnEffects)
        {
            var spawnEffect = kvp.Value;

            if (StoreApi.IsItemEquipped(player.SteamID, spawnEffect.Id, player.TeamNum))
            {
                Server.NextFrame(() => SpawnEffect(player));
                break;
            }
        }
        return HookResult.Continue;
    }
    public void SpawnEffect(CCSPlayerController player)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        CHEGrenadeProjectile? grenade = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
        if (grenade == null || !grenade.IsValid)
            return;

        var node = pawn.CBodyComponent?.SceneNode;
        if (node == null)
            return;

        Vector pos = node.AbsOrigin;
        pos.Z += 10;

        grenade.TicksAtZeroVelocity = 100;
        grenade.TeamNum = pawn.TeamNum;
        grenade.Damage = 0;
        grenade.DmgRadius = 0;
        grenade.Teleport(pos, node.AbsRotation, new Vector(0, 0, -10));
        grenade.DispatchSpawn();
        grenade.AcceptInput("InitializeSpawnFromWorld", pawn, pawn, "");
        grenade.DetonateTime = 0;
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Effects";
    public Dictionary<string, SpawnEffect_Item> SpawnEffects { get; set; } = new Dictionary<string, SpawnEffect_Item>()
    {
        {
            "1", new SpawnEffect_Item
            {
                Id = "spawn_effect",
                Name = "SpawnEffect",
                Price = 1500,
                Duration = 240,
                Type = "Effect",
                Description = "",
                Flags = "",
            }
        }
    };
}
public class SpawnEffect_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
}