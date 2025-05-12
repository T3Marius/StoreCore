using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using static CounterStrikeSharp.API.Core.Listeners;
using StoreAPI;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace StoreCore;

public class Bhop : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Bhop";
    public override string ModuleVersion => "1.0.1";

    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private readonly ConVar? _autobunnyhopping = ConVar.Find("sv_autobunnyhopping");
    private readonly ConVar? _enablebunnyhopping = ConVar.Find("sv_enablebunnyhopping");
    private bool _wasBunnyhoppingChanged;
    private bool _wasEnableBunnyhoppingChanged;
    private readonly Dictionary<int, BhopPlayerData> _activeBhopPlayers = new();

    private static readonly MemoryFunctionVoid<CCSPlayer_MovementServices, IntPtr>
        ProcessMovement = new(GameData.GetSignature("CCSPlayer_MovementServices_ProcessMovement"));

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);

        ProcessMovement.Hook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Hook(ProcessMovementPost, HookMode.Post);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Bhop");

        foreach (var kvp in Config.Bhops)
        {
            var bhop = kvp.Value;

            StoreApi.RegisterItem(
                bhop.Id,
                bhop.Name,
                Config.Category,
                bhop.Type,
                bhop.Price,
                bhop.Description,
                bhop.Flags,
                duration: bhop.Duration
            );
        }

        RegisterListener<OnTick>(OnTick);
        RegisterListener<OnClientDisconnectPost>(OnClientDisconnect);
        RegisterListener<OnClientConnected>(OnClientConnect);
    }

    private void OnClientConnect(int slot)
    {
        if (!_activeBhopPlayers.ContainsKey(slot))
        {
            _activeBhopPlayers[slot] = new BhopPlayerData();
        }
    }

    private void OnClientDisconnect(int slot)
    {
        if (_activeBhopPlayers.ContainsKey(slot))
        {
            _activeBhopPlayers.Remove(slot);
        }
    }

    public void OnTick()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, PawnIsAlive: true }))
        {
            bool playerHasBhop = false;
            int maxSpeed = 0;

            foreach (var bhop in Config.Bhops.Values)
            {
                if (StoreApi!.IsItemEquipped(player.SteamID, bhop.Id, player.TeamNum))
                {
                    playerHasBhop = true;
                    maxSpeed = bhop.MaxSpeed;
                    break;
                }
            }

            if (!_activeBhopPlayers.ContainsKey(player.Slot))
            {
                _activeBhopPlayers[player.Slot] = new BhopPlayerData();
            }

            if (playerHasBhop != _activeBhopPlayers[player.Slot].Active)
            {
                SetBunnyhop(player, playerHasBhop);
                _activeBhopPlayers[player.Slot].Active = playerHasBhop;
            }

            _activeBhopPlayers[player.Slot].MaxSpeed = maxSpeed;

            if (playerHasBhop && maxSpeed > 0)
            {
                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn != null)
                {
                    if (Math.Round(playerPawn.AbsVelocity.Length2D()) > maxSpeed)
                    {
                        ChangeVelocity(playerPawn, maxSpeed);
                    }
                }
            }
        }
    }

    private void SetBunnyhop(CCSPlayerController player, bool value)
    {
        player.ReplicateConVar("sv_autobunnyhopping", Convert.ToString(value));
        player.ReplicateConVar("sv_enablebunnyhopping", Convert.ToString(value));
    }

    private void ChangeVelocity(CCSPlayerPawn? pawn, float vel)
    {
        if (pawn == null) return;

        var currentVelocity = new Vector(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
        var currentSpeed3D = Math.Sqrt(currentVelocity.X * currentVelocity.X +
                                      currentVelocity.Y * currentVelocity.Y +
                                      currentVelocity.Z * currentVelocity.Z);

        pawn.AbsVelocity.X = (float)(currentVelocity.X / currentSpeed3D) * vel;
        pawn.AbsVelocity.Y = (float)(currentVelocity.Y / currentSpeed3D) * vel;
        pawn.AbsVelocity.Z = (float)(currentVelocity.Z / currentSpeed3D) * vel;
    }

    private HookResult ProcessMovementPre(DynamicHook hook)
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null)
        {
            return HookResult.Continue;
        }

        _wasBunnyhoppingChanged = false;
        _wasEnableBunnyhoppingChanged = false;

        var movementServices = hook.GetParam<CCSPlayer_MovementServices>(0);
        if (!IsBhopEnabled(movementServices))
        {
            return HookResult.Continue;
        }

        if (!_autobunnyhopping.GetPrimitiveValue<bool>())
        {
            _autobunnyhopping.SetValue(true);
            _wasBunnyhoppingChanged = true;
        }

        if (!_enablebunnyhopping.GetPrimitiveValue<bool>())
        {
            _enablebunnyhopping.SetValue(true);
            _wasEnableBunnyhoppingChanged = true;
        }

        return HookResult.Continue;
    }

    private HookResult ProcessMovementPost(DynamicHook hook)
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null)
        {
            return HookResult.Continue;
        }

        if (_wasBunnyhoppingChanged)
        {
            _autobunnyhopping.SetValue(false);
            _wasBunnyhoppingChanged = false;
        }

        if (_wasEnableBunnyhoppingChanged)
        {
            _enablebunnyhopping.SetValue(false);
            _wasEnableBunnyhoppingChanged = false;
        }

        return HookResult.Continue;
    }

    private int? GetSlot(CCSPlayer_MovementServices? movementServices)
    {
        var index = movementServices?.Pawn.Value?.Controller.Value?.Index;
        if (index == null)
            return null;

        return (int)index.Value - 1;
    }

    private bool IsBhopEnabled(CCSPlayer_MovementServices movementServices)
    {
        var slot = GetSlot(movementServices);
        if (slot == null)
        {
            return false;
        }

        return _activeBhopPlayers.ContainsKey(slot.Value) && _activeBhopPlayers[slot.Value].Active;
    }

    public override void Unload(bool hotReload)
    {
        ProcessMovement.Unhook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Unhook(ProcessMovementPost, HookMode.Post);

        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true }))
        {
            if (_activeBhopPlayers.ContainsKey(player.Slot) && _activeBhopPlayers[player.Slot].Active)
            {
                SetBunnyhop(player, false);
            }
        }

        base.Unload(hotReload);
    }
}

public class PluginConfig
{
    public string Category { get; set; } = "Bhop";
    public Dictionary<string, Bhop_Item> Bhops { get; set; } = new Dictionary<string, Bhop_Item>()
    {
        {
            "1", new Bhop_Item
            {
                Id = "full_speed_bhop",
                Name = "Bhop (Full Speed)",
                Duration = 120,
                Price = 2500,
                MaxSpeed = 0,
                Description = "Gives you full speed bhop for 2 minutes",
                Type = "bhop",
            }
        },
        {
            "2", new Bhop_Item
            {
                Id = "limited_speed_bhop",
                Name = "Bhop (Limited Speed)",
                Duration = 180,
                Price = 2000,
                MaxSpeed = 350,
                Description = "Gives you bhop with speed limit of 350 for 3 minutes",
                Type = "bhop",
            }
        },
        {
            "3", new Bhop_Item
            {
                Id = "admin_speed_bhop",
                Name = "Bhop (Only Admin)",
                Duration = 180,
                Price = 2000,
                MaxSpeed = 350,
                Description = "Gives you bhop with speed limit of 350 for 3 minutes",
                Flags = "@css/generic",
                Type = "bhop",
            }
        }
    };
}

public class Bhop_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; } = 0;
    public int Price { get; set; } = 0;
    public int MaxSpeed { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
}

public class BhopPlayerData
{
    public bool Active { get; set; } = false;
    public int MaxSpeed { get; set; } = 0;
}