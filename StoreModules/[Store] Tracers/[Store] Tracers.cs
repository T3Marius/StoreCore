using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using StoreAPI;

namespace StoreCore;

public class Tracers : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Tracers";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public static readonly QAngle RotationZero = new(0, 0, 0);
    public static readonly Vector VectorZero = new(0, 0, 0);
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventBulletImpact>(OnBulletImpact, HookMode.Pre);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Tracers");

        RegisterItems();
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }
    public HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || StoreApi == null)
            return HookResult.Continue;

        foreach (var tracer in Config.Tracers.Values)
        {
            if (StoreApi.IsItemEquipped(player.SteamID, tracer.Id, player.TeamNum))
            {
                Vector? PlayerPosition = player.Pawn.Value?.AbsOrigin;
                Vector? BulletOrigin = new(PlayerPosition!.X, PlayerPosition.Y, PlayerPosition.Z + 57);
                Vector? BulletDestination = new(@event.X, @event.Y, @event.Z);

                Color color = Color.FromName(tracer.Color);
                if (tracer.Color == "Random")
                {
                    color = GetRandomColor();
                }
                else if (tracer.Color == "Team")
                {
                    color = player.TeamNum == 3 ? Color.Blue : Color.Yellow;
                }

                DrawTracer(BulletOrigin, BulletDestination, color, 0.3f, 1.0f, 0.5f);
                break;
            }
        }

        return HookResult.Continue;
    }
    public (int, CBeam?) DrawTracer(Vector startPos, Vector endPos, Color color, float life, float startWidht, float endWidth)
    {
        if (startPos == null || endPos == null)
            return (-1, null);

        CBeam? beam = Utilities.CreateEntityByName<CBeam>("beam");

        if (beam == null)
            return (-1, null);

        beam.Render = color;
        beam.Width = startWidht / 2.0f;
        beam.EndWidth = endWidth / 2.0f;

        beam.Teleport(startPos, RotationZero, VectorZero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        AddTimer(life, () =>
        {
            try
            {
                if (beam.IsValid)
                    beam.Remove();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove beam {message}", ex.Message);
            }
        });

        return ((int)beam.Index, beam);
    }
    public Color GetRandomColor()
    {
        Random random = new();
        return Color.FromArgb(random.Next(256), random.Next(256), random.Next(256));
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Tracers)
        {
            var tracer = kvp.Value;

            StoreApi.RegisterItem(
                tracer.Id,
                tracer.Name,
                Config.Category,
                tracer.Type,
                tracer.Price,
                tracer.Description,
                tracer.Flags,
                duration: tracer.Duration
            );
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.Tracers)
        {
            var trail = kvp.Value;

            StoreApi.UnregisterItem(trail.Id);
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Tracers";
    public Dictionary<string, Tracer_Item> Tracers { get; set; } = new Dictionary<string, Tracer_Item>()
    {
      {
        "1", new Tracer_Item
        {
            Id = "red_tracer",
            Name = "Red Tracer",
            Color = "Red",
            Type = "tracer",
            Description = "Gives you red tracer color.",
            Price = 1250,
            Duration = 82300
        }
      },
      {
        "2", new Tracer_Item
        {
            Id = "green_tracer",
            Name = "Green Tracer",
            Color = "Green",
            Type = "tracer",
            Description = "Gives you green tracer color",
            Price = 1250,
            Duration = 82300
        }
      },
      {
        "3", new Tracer_Item
        {
            Id = "team_tracer",
            Name = "Team Tracer",
            Color = "Team",
            Type = "tracer",
            Description = "Gives you team tracer color",
            Price = 2500,
            Duration = 82300
        }
      },
      {
        "4", new Tracer_Item
        {
            Id = "random_tracer",
            Name = "Random Tracer",
            Color = "Random",
            Type = "tracer",
            Description = "Gives you random tracer color",
            Flags = "@css/vip,@css/generic",
            Price = 2500,
            Duration = 82300
        }
      }
    };
}
public class Tracer_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
}