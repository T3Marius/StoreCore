using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;

namespace StoreCore;

public class Parachute : BasePlugin
{
    public override string ModuleAuthor => "Mesharsky";
    public override string ModuleName => "[StoreCore] Parachute";
    public override string ModuleVersion => "1.0.0";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private readonly Dictionary<IntPtr, PlayerData> _playerDatas = [];

    public class PlayerData
    {
        public CDynamicProp? Entity;
        public bool Flying;
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);

        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterListener<Listeners.OnTick>(OnTick);

        if (hotReload)
        {
            List<CCSPlayerController> players = Utilities.GetPlayers();
            foreach (CCSPlayerController player in players)
            {
                _playerDatas[player.Handle] = new PlayerData();
            }
        }
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Parachute");

        RegisterItems();

        StoreApi.OnItemPreview += OnItemPreview;
    }

    public void OnServerPrecacheResources(ResourceManifest manifest)
    {
        foreach (var kvp in Config.Parachutes)
        {
            if (!string.IsNullOrEmpty(kvp.Value.Model))
                manifest.AddResource(kvp.Value.Model);
        }
    }

    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player)
            return HookResult.Continue;

        _playerDatas[player.Handle] = new PlayerData();
        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player)
            return HookResult.Continue;

        RemoveParachute(player);
        _playerDatas.Remove(player.Handle);
        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player)
            return HookResult.Continue;

        RemoveParachute(player);
        _playerDatas[player.Handle] = new PlayerData();
        return HookResult.Continue;
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player)
            return HookResult.Continue;

        RemoveParachute(player);
        _playerDatas[player.Handle] = new PlayerData();
        return HookResult.Continue;
    }

    public void OnTick()
    {
        if (StoreApi == null || _playerDatas.Count == 0)
            return;

        List<CCSPlayerController> players = Utilities.GetPlayers();
        foreach (CCSPlayerController player in players)
        {
            if (!_playerDatas.TryGetValue(player.Handle, out PlayerData? playerData) ||
                player.PlayerPawn.Value is not { } playerPawn ||
                playerPawn.LifeState != (int)LifeState_t.LIFE_ALIVE)
                continue;

            ParachuteItem? equippedParachute = null;
            foreach (var kvp in Config.Parachutes)
            {
                if (StoreApi.IsItemEquipped(player.SteamID, kvp.Value.Id, player.TeamNum))
                {
                    equippedParachute = kvp.Value;
                    break;
                }
            }

            if (equippedParachute == null)
                continue;

            if (player.Buttons.HasFlag(PlayerButtons.Use) && !playerPawn.GroundEntity.IsValid)
            {
                Vector velocity = playerPawn.AbsVelocity;

                if (velocity.Z >= 0.0)
                {
                    playerPawn.GravityScale = 1.0f;
                    continue;
                }

                if (playerData.Entity == null)
                {
                    playerData.Entity = CreateParachute(playerPawn, equippedParachute.Model);
                }

                float fallSpeed = equippedParachute.FallSpeed * -1.0f;
                float decrease = equippedParachute.FallDecrease;
                bool linear = equippedParachute.Linear;

                velocity.Z = (velocity.Z >= fallSpeed && linear) || decrease == 0.0f
                    ? fallSpeed
                    : velocity.Z + decrease;

                if (!playerData.Flying)
                {
                    playerPawn.GravityScale = 0.1f;
                    playerData.Flying = true;
                }
            }
            else if (playerData.Flying)
            {
                RemoveParachute(player);
                playerData.Entity = null;
                playerData.Flying = false;
                playerPawn.GravityScale = 1.0f;
            }
        }
    }

    private CDynamicProp? CreateParachute(CCSPlayerPawn playerPawn, string model)
    {
        CDynamicProp? entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (entity?.IsValid is not true)
            return null;

        entity.Teleport(playerPawn.AbsOrigin);
        entity.DispatchSpawn();
        entity.SetModel(model);
        entity.AcceptInput("SetParent", playerPawn, playerPawn, "!activator");
        return entity;
    }

    private void RemoveParachute(CCSPlayerController player)
    {
        if (_playerDatas.TryGetValue(player.Handle, out PlayerData? playerData) && playerData.Entity?.IsValid is true)
            playerData.Entity.Remove();
    }

    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        if (player.PlayerPawn.Value is not { } playerPawn)
            return;

        foreach (var kvp in Config.Parachutes)
        {
            var parachute = kvp.Value;
            if (uniqueId == parachute.Id)
            {
                CDynamicProp? entity = CreateParachute(playerPawn, parachute.Model);

                AddTimer(3.0f, () =>
                {
                    if (entity?.IsValid is true)
                        entity.Remove();
                });

                break;
            }
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Parachutes)
        {
            var parachute = kvp.Value;

            StoreApi.RegisterItem(
                parachute.Id,
                parachute.Name,
                Config.Category,
                parachute.Type,
                parachute.Price,
                parachute.Description,
                parachute.Flags,
                duration: parachute.Duration);
        }
    }
}

public class PluginConfig
{
    public string Category { get; set; } = "Special Items";
    public Dictionary<string, ParachuteItem> Parachutes { get; set; } = new Dictionary<string, ParachuteItem>()
    {
        {
            "1", new ParachuteItem
            {
                Id = "parachute_basic",
                Name = "Basic Parachute",
                Price = 500,
                Duration = 86400,
                Type = "Equipment",
                Description = "Slow your fall by using USE key (E) while in mid-air",
                Model = "models/props_survival/parachute/chute.vmdl",
                FallSpeed = 85,
                FallDecrease = 15,
                Linear = true,
                Flags = ""
            }
        },
        {
            "2", new ParachuteItem
            {
                Id = "parachute_premium",
                Name = "Premium Parachute",
                Price = 1500,
                Duration = 604800, // 7 days in seconds
                Type = "Equipment",
                Description = "Deluxe parachute with slower falling speed. Use key (E) in mid-air",
                Model = "models/props_survival/parachute/chute.vmdl",
                FallSpeed = 50,
                FallDecrease = 10,
                Linear = true,
                Flags = ""
            }
        },
        {
            "3", new ParachuteItem
            {
                Id = "parachute_permanent",
                Name = "Permanent Parachute",
                Price = 8000,
                Duration = 0,
                Type = "Equipment",
                Description = "Never-expiring parachute with ultimate performance. Use key (E) in mid-air",
                Model = "models/props_survival/parachute/chute.vmdl",
                FallSpeed = 40,
                FallDecrease = 5,
                Linear = true,
                Flags = ""
            }
        }
    };
}

public class ParachuteItem
{
    public string Id { get; set; } = "store_parachute";
    public string Name { get; set; } = "Parachute";
    public int Price { get; set; } = 1500;
    public int Duration { get; set; } = 240;
    public string Type { get; set; } = "Equipment";
    public string Description { get; set; } = "";
    public string Model { get; set; } = "models/props_survival/parachute/chute.vmdl";
    public float FallSpeed { get; set; } = 85;
    public float FallDecrease { get; set; } = 15;
    public bool Linear { get; set; } = true;
    public string Flags { get; set; } = string.Empty;
}