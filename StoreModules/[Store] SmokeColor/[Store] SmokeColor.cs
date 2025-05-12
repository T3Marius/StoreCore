using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API.Core;
using StoreAPI;
using CounterStrikeSharp.API;

namespace StoreCore;

public class SmokeColor : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] SmokeColor";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void Load(bool hotReload)
    {
        RegisterListener<OnEntitySpawned>(OnEntitySpawned);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("SmokeColor");

        if (!hotReload)
        {
            foreach (var kvp in Config.Smokes)
            {
                var smoke = kvp.Value;

                StoreApi.RegisterItem(
                    smoke.Id,
                    smoke.Name,
                    Config.Category,
                    smoke.Type,
                    smoke.Price,
                    smoke.Description,
                    duration: smoke.Duration);
            }
        }
    }
    public void OnEntitySpawned(CEntityInstance entity)
    {
        string designerName = entity.DesignerName;
        if (designerName != "smokegrenade_projectile" || StoreApi == null)
            return;

        CSmokeGrenadeProjectile smoke = new CSmokeGrenadeProjectile(entity.Handle);

        Server.NextFrame(() =>
        {
            var thrower = smoke.Thrower.Value?.Controller.Value;
            if (thrower == null)
                return;

            CCSPlayerController player = new CCSPlayerController(thrower.Handle);

            foreach (var kvp in Config.Smokes)
            {
                var _smoke = kvp.Value;
                var color = _smoke.Color;

                if (StoreApi.IsItemEquipped(player.SteamID, _smoke.Id, player.TeamNum))
                {
                    if (color.Count >= 3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            smoke.SmokeColor[i] = color[i] == -1f ? Random.Shared.NextSingle() * 255f : color[i];
                        }
                    }
                    break;
                }
            }
        });
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Smoke Colors";
    public Dictionary<string, Smoke_Item> Smokes { get; set; } = new Dictionary<string, Smoke_Item>()
    {
        {
            "1", new Smoke_Item
            {
                Color = new List<float> { 255, 0, 0 },
                Name = "Red Smoke",
                Id = "red_smoke",
                Type = "Smoke",
                Description = "Turns your smoke RED",
                Flags = "",
                Price = 100,
                Duration = 30
            }
        }
    };
}
public class Smoke_Item
{
    public List<float> Color { get; set; } = new List<float>();
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
}
