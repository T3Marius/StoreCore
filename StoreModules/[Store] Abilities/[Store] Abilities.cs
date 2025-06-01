using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Core;
using StoreAPI;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Memory;

namespace StoreCore;

public class Abilities : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Abilities";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;

    public Timer? SpeedTimer;
    public Timer? GravityTimer;
    public Timer? GodTimer;
    public Timer? NoClipTimer;

    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Abilities");

        RegisterItems();

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;

        RegisterEventHandler<EventRoundStart>(OnRoundStart);

    }
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        NoClipTimer?.Kill();
        GodTimer?.Kill();
        GravityTimer?.Kill();
        SpeedTimer?.Kill();

        NoClipTimer = null;
        GodTimer = null;
        GravityTimer = null;
        SpeedTimer = null;

        return HookResult.Continue;
    }
    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;


        foreach (var kvp in Config.Speeds)
        {
            var speed = kvp.Value;

            if (item["uniqueid"] == speed.Id)
            {
                pawn.VelocityModifier = speed.Speed;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");

                if (speed.Timer > 0)
                {
                    SpeedTimer = AddTimer(speed.Timer, () =>
                    {
                        pawn.VelocityModifier = 1.0f;
                        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");

                        SpeedTimer?.Kill();
                        SpeedTimer = null;
                    });
                }
            }
        }
        foreach (var kvp in Config.Gravitys)
        {
            var gravity = kvp.Value;

            if (item["uniqueid"] == gravity.Id)
            {
                pawn.GravityScale = gravity.Gravity;
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale");

                if (gravity.Timer > 0)
                {
                    GravityTimer = AddTimer(gravity.Timer, () =>
                    {
                        pawn.GravityScale = 1.0f;
                        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale");

                        GravityTimer?.Kill();
                        GravityTimer = null;
                    });
                }
            }
        }
        foreach (var kvp in Config.Gods)
        {
            var god = kvp.Value;

            if (item["uniqueid"] == god.Id)
            {
                pawn.TakesDamage = false;

                if (god.Timer > 0)
                {
                    GodTimer = AddTimer(god.Timer, () =>
                    {
                        pawn.TakesDamage = true;
                        GodTimer?.Kill();
                        GodTimer = null;
                    });
                }
            }
        }
        foreach (var kvp in Config.NoClips)
        {
            var noclip = kvp.Value;

            if (item["uniqueid"] == noclip.Id)
            {
                ChangeMovetype(pawn, MoveType_t.MOVETYPE_NOCLIP);

                if (noclip.Timer > 0)
                {
                    NoClipTimer = AddTimer(noclip.Timer, () =>
                    {
                        ChangeMovetype(pawn, MoveType_t.MOVETYPE_WALK);
                        NoClipTimer?.Kill();
                        GodTimer = null;
                    });
                }
            }
        }
    }
    private void ChangeMovetype(CBasePlayerPawn pawn, MoveType_t movetype)
    {
        pawn.MoveType = movetype;
        Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;


        foreach (var kvp in Config.Speeds)
        {
            var speed = kvp.Value;

            StoreApi.RegisterItem(
                speed.Id,
                speed.Name,
                Config.Category,
                speed.Type,
                speed.Price,
                speed.Description,
                speed.Flags,
                isEquipable: false
                );
        }
        foreach (var kvp in Config.Gravitys)
        {
            var gravity = kvp.Value;

            StoreApi.RegisterItem(
                gravity.Id,
                gravity.Name,
                Config.Category,
                gravity.Type,
                gravity.Price,
                gravity.Description,
                gravity.Flags,
                isEquipable: false
                );
        }
        foreach (var kvp in Config.Gods)
        {
            var god = kvp.Value;

            StoreApi.RegisterItem(
                god.Id,
                god.Name,
                Config.Category,
                god.Type,
                god.Price,
                god.Description,
                god.Flags,
                isEquipable: false
                );
        }
        foreach (var kvp in Config.NoClips)
        {
            var noclip = kvp.Value;

            StoreApi.RegisterItem(
                noclip.Id,
                noclip.Name,
                Config.Category,
                noclip.Type,
                noclip.Price,
                noclip.Description,
                noclip.Flags,
                isEquipable: false
                );
        }
    }
    public void UnregisterItems()
    {
        foreach (var item in Config.Gods.Values)
        {
            StoreApi?.UnregisterItem(item.Id);
        }
        foreach (var item in Config.NoClips.Values)
        {
            StoreApi?.UnregisterItem(item.Id);
        }
        foreach (var item in Config.Speeds.Values)
        {
            StoreApi?.UnregisterItem(item.Id);
        }
        foreach (var item in Config.Gravitys.Values)
        {
            StoreApi?.UnregisterItem(item.Id);
        }
    }

}
public class PluginConfig
{
    public string Category { get; set; } = "Abilites";
    public Dictionary<string, Speed_Item> Speeds { get; set; } = new Dictionary<string, Speed_Item>
    {
        {
            "1", new Speed_Item
            {
                Id = "speed_10_seconds",
                Name = "3.5 Speed (10 sec)",
                Speed = 3.5f,
                Timer = 10.0f,
                Description = "Gives you 3.5 speed for 10 seconds",
                Type = "abilties",
                Price = 1500
            }
        },
    };
    public Dictionary<string, Gravity_Item> Gravitys { get; set; } = new Dictionary<string, Gravity_Item>
    {
        {
            "1", new Gravity_Item
            {
                Id = "gravity_10_seconds",
                Name = "2.5 Gravity (10 sec)",
                Gravity = 2.5f,
                Timer = 10.0f,
                Description = "Gives you 2.5 gravity for 10 seconds",
                Type = "abilities",
                Price = 1850
            }
        }
    };
    public Dictionary<string, God_Item> Gods { get; set; } = new Dictionary<string, God_Item>
    {
        {
            "1", new God_Item
            {
                Id = "god_10_seconds",
                Name = "God (10 sec)",
                Timer = 10.0f,
                Description = "Gives you god for 10 seconds",
                Flags = "@css/god,@css/generic",
                Type = "abilities",
                Price = 1850
            }
        }
    };
    public Dictionary<string, NoClip_Item> NoClips { get; set; } = new Dictionary<string, NoClip_Item>
    {
        {
            "1", new NoClip_Item
            {
                Id = "noclip_10_seconds",
                Name = "NoClip (10 sec)",
                Timer = 10.0f,
                Description = "Gives you no clip for 10 seconds",
                Flags = "",
                Type ="abilities",
                Price = 2000
            }
        }
    };
}
public class Speed_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Speed { get; set; } = 0;
    public float Timer { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
}
public class Gravity_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Gravity { get; set; } = 0;
    public float Timer { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
}
public class NoClip_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Timer { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
}
public class God_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Timer { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
}