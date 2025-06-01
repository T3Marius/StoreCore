using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using StoreAPI;

namespace StoreCore;

public class HitSounds : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store Core] Hit-Sounds";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi couldn't be found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("HitSounds");

        RegisterItems();

        StoreApi.OnItemPreview += OnItemPreview;
    }
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        if (victim == null || attacker == null || StoreApi == null)
            return HookResult.Continue;
        if (Config == null)
            return HookResult.Continue;

        if (attacker != victim)
        {
            foreach (var kvp in Config.HitSounds)
            {
                string ID = kvp.Value.Id;

                if (StoreApi.IsItemEquipped(attacker.SteamID, ID, attacker.TeamNum))
                {
                    attacker.ExecuteClientCommand($"play {kvp.Value.SoundPath}");
                    break;
                }
            }
        }

        return HookResult.Continue;
    }
    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        if (Config == null)
            return;
        foreach (var kvp in Config.HitSounds)
        {
            string id = kvp.Value.Id;

            if (uniqueId == id)
            {
                player.ExecuteClientCommand($"play {kvp.Value.SoundPath}");
            }
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.HitSounds)
        {
            var hitSound = kvp.Value;
            StoreApi.RegisterItem(
                hitSound.Id,
                hitSound.Name,
                Config.CategoryName,
                "Sound",
                hitSound.Price,
                hitSound.Description,
                hitSound.Flags,
                true,
                true,
                true,
                hitSound.Duration);
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;

        foreach (var kvp in Config.HitSounds)
        {
            var hitSound = kvp.Value;
            StoreApi.UnregisterItem(hitSound.Id);
        }
    }
}
public class PluginConfig
{
    public string CategoryName { get; set; } = "Hit Sounds";
    public Dictionary<string, Hit_Sounds> HitSounds { get; set; } = new()
    {
        {
            "1", new Hit_Sounds
            {
                Id = "bell_sound",
                Name = "Bell Sound",
                SoundPath = "sounds/training/bell_normal.vsnd_c",
                Description = "Plays a bell sound",
                Flags = "",
                Price = 1200,
                Duration = 0
            }
        },
    };
}
public class Hit_Sounds
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SoundPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;

}