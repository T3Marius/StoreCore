using System.Drawing;
using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;
using Microsoft.Extensions.Logging;

namespace StroeCore;

public class Trails : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Trails";
    public override string ModuleVersion => "1.0.1";

    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    public readonly Vector[] TrailLastOrigin = new Vector[64];
    public readonly Vector[] TrailEndOrigin = new Vector[64];

    public static readonly Color[] rainbowColors = GenerateRainbowColors();
    public static int colorIndex = 0;

    public static int tickCounter = 0;
    private static int ticksForUpdate = 0;
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Trails");

        foreach (var kvp in Config.Trails)
        {
            var trail = kvp.Value;

            StoreApi.RegisterItem(
                trail.Id,
                trail.Name,
                Config.Category,
                trail.Type,
                trail.Price,
                trail.Description,
                trail.Flags,
                duration: trail.Duration
            );
        }

        for (int i = 0; i < 64; i++)
        {
            TrailLastOrigin[i] = new Vector();
            TrailEndOrigin[i] = new Vector();
        }

        RegisterListener<OnTick>(OnTick);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterListener<OnServerPrecacheResources>((manifest) =>
        {
            foreach (var trail in Config.Trails.Values)
            {
                manifest.AddResource(trail.Path);
            }
        });
    }
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null)
            return HookResult.Continue;

        ResetPlayerTrail(player);

        return HookResult.Continue;
    }
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null)
            return HookResult.Continue;

        ResetPlayerTrail(player);

        return HookResult.Continue;
    }
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ResetAllTrails();

        return HookResult.Continue;
    }
    public void OnTick()
    {
        tickCounter++;
        if (tickCounter < ticksForUpdate || StoreApi == null)
            return;

        tickCounter = 0;

        foreach (var p in Utilities.GetPlayers())
        {
            foreach (var trail in Config.Trails.Values)
            {
                if (StoreApi.IsItemEquipped(p.SteamID, trail.Id, p.TeamNum))
                {
                    var absOrigin = p.PlayerPawn.Value?.AbsOrigin;
                    if (absOrigin == null)
                        return;

                    if (ShouldUpdateTrail(p, absOrigin))
                    {
                        UpdatePlayerPosition(p, absOrigin);

                        Color color = Color.White;
                        if (trail.Color == "Rainbow" || trail.Color == "rainbow")
                        {
                            color = GetNextRainbowColor();
                        }
                        else
                        {
                            color = Color.FromName(trail.Color);
                        }
                        CreateTrail(p, absOrigin, trail.Path, color, trail.Width, trail.LifeTime);
                        break;
                    }
                }
            }
        }
    }
    public void CreateTrail(CCSPlayerController player, Vector absOrigin, string file, Color color, float width = 1.0f, float lifetime = 1.0f)
    {
        if (player == null || !player.IsValid)
            return;

        if (string.IsNullOrEmpty(file))
            return;

        if (file.EndsWith(".vpcf"))
        {
            CreateParticleTrail(player, absOrigin, file, lifetime);
        }
        else
        {
            CreateBeamTrail(player, absOrigin, file, color, width, lifetime);
        }
    }
    public void CreateParticleTrail(CCSPlayerController player, Vector absOrigin, string particleFile, float lifetime = 1.0f)
    {
        CParticleSystem? particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (particle == null || pawn == null)
            return;

        particle.EffectName = particleFile;
        particle.DispatchSpawn();
        particle.AcceptInput("Start");
        particle.AcceptInput("FollowEntity", pawn, pawn, "!activator");

        particle.Teleport(absOrigin, new QAngle(), new Vector());
        AddTimer(lifetime, () =>
        {
            if (particle != null && particle.IsValid)
            {
                particle.Remove();
            }
        });

    }
    public void CreateBeamTrail(CCSPlayerController player, Vector absOrigin, string beamFile, Color colorValue, float width = 1.0f, float lifetime = 1.0f)
    {
        if (VecIsZero(TrailEndOrigin[player.Slot]))
        {
            VecCopy(absOrigin, TrailEndOrigin[player.Slot]);
            return;
        }

        var beam = Utilities.CreateEntityByName<CEnvBeam>("env_beam")!;
        if (beam == null)
            return;

        beam.Width = width;
        beam.Render = colorValue;
        beam.Teleport(absOrigin, new QAngle(), new Vector());

        VecCopy(TrailEndOrigin[player.Slot], beam.EndPos);
        VecCopy(absOrigin, TrailEndOrigin[player.Slot]);
        Utilities.SetStateChanged(beam, "CBeam", "m_vecEndPos");

        AddTimer(lifetime, () =>
        {
            if (beam != null && beam.IsValid)
                beam.Remove();
        });
    }
    public float VecCalculateDistance(Vector vector1, Vector vector2)
    {
        float dx = vector2.X - vector1.X;
        float dy = vector2.Y - vector1.Y;
        float dz = vector2.Z - vector1.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    public void VecCopy(Vector source, Vector destination)
    {
        destination.X = source.X;
        destination.Y = source.Y;
        destination.Z = source.Z;
    }
    public bool VecIsZero(Vector vector)
    {
        return vector.LengthSqr() == 0;
    }

    public Color GetNextRainbowColor()
    {
        Color result = rainbowColors[colorIndex];
        colorIndex = (colorIndex + 1) % rainbowColors.Length;
        return result;
    }

    public static Color[] GenerateRainbowColors()
    {
        List<Color> colors = new List<Color>();

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 255, 25 * i, 0));

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 255 - 25 * i, 255, 0));

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 0, 255, 25 * i));

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 0, 255 - 25 * i, 255));

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 25 * i, 0, 255));

        for (int i = 0; i <= 10; i++)
            colors.Add(Color.FromArgb(255, 255, 0, 255 - 25 * i));

        return colors.ToArray();
    }

    public void UpdatePlayerPosition(CCSPlayerController player, Vector absOrigin)
    {
        if (player == null || !player.IsValid)
            return;

        VecCopy(absOrigin, TrailLastOrigin[player.Slot]);
    }

    public bool ShouldUpdateTrail(CCSPlayerController player, Vector currentPosition, float minDistance = 5.0f)
    {
        if (player == null || !player.IsValid)
            return false;

        return VecCalculateDistance(TrailLastOrigin[player.Slot], currentPosition) > minDistance;
    }

    public void ResetPlayerTrail(CCSPlayerController player)
    {
        if (player == null)
            return;

        TrailEndOrigin[player.Slot] = new Vector();
        TrailLastOrigin[player.Slot] = new Vector();
    }

    public void ResetAllTrails()
    {
        for (int i = 0; i < 64; i++)
        {
            TrailEndOrigin[i] = new Vector();
            TrailLastOrigin[i] = new Vector();
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Trails";
    public Dictionary<string, Trail_Item> Trails { get; set; } = new Dictionary<string, Trail_Item>()
    {
        {
            "1", new Trail_Item
            {
                Id = "red_trail",
                Name = "Red Trail",
                Path = "materials/sprites/laserbeam.vtex",
                Color = "Red",
                Price = 2500,
                Duration = 5233,
                Type = "trail",
                Description = "Leaves a red trail behind you",
                Width = 1.0f,
                LifeTime = 1.0f
            }
        },
        {
            "2", new Trail_Item
            {
                Id = "rainbow_trail",
                Name = "Rainbow Trail",
                Path = "materials/sprites/laserbeam.vtex",
                Color = "Rainbow",
                Price = 5000,
                Duration = 10080,
                Type = "trail",
                Description = "Leaves a rainbow trail behind you",
                Flags = "@css/gay",
                Width = 1.0f,
                LifeTime = 1.0f
            }
        },
        {
            "3", new Trail_Item
            {
                Id = "particle_trail",
                Name = "Particle Trail",
                Path = "particles/ambient_fx/ambient_sparks_glow.vpcf",
                Color = "",
                Price = 5000,
                Duration = 10080,
                Type = "trail",
                Description = "Leaves a particle trail behind you",
                Width = 1.0f,
                LifeTime = 1.0f
            }
        }
    };
}
public class Trail_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public float Width { get; set; } = 0;
    public float LifeTime { get; set; } = 0;
}