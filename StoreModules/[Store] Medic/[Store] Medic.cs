using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace StoreCore;

public class MedicPlugin : BasePlugin
{
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleName => "[StoreCore] Medic Plugin";
    public override string ModuleVersion => "1.0.1";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private Dictionary<ulong, int> _tries = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Medic");

        RegisterItems();

        StoreApi.OnItemPreview += OnItemPreview;
    }
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            Server.PrecacheModel("weapons/w_eq_charge");
        });
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItems();
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (victim == null || attacker == null || victim == attacker)
            return HookResult.Continue;

        var attackerPawn = attacker.PlayerPawn.Value;
        if (StoreApi == null || attackerPawn == null)
            return HookResult.Continue;

        foreach (var kvp in Config.MedicItems)
        {
            var medicItem = kvp.Value;
            if (StoreApi.IsItemEquipped(attacker.SteamID, medicItem.Id, attacker.TeamNum))
            {
                attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                break;
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler(mode: HookMode.Post)]
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || player.IsBot)
            return HookResult.Continue;

        if (!_tries.ContainsKey(player.SteamID))
            _tries.Add(player.SteamID, Config.MaxUse);

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _tries.Clear();
        foreach (var player in Utilities.GetPlayers())
            _tries.TryAdd(player.SteamID, Config.MaxUse);
        return HookResult.Continue;
    }

    [ConsoleCommand("medkit", "Heal player")]
    [ConsoleCommand("medic", "Heal player")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnCommand(CCSPlayerController? activator, CommandInfo command)
    {
        if (activator == null)
            return;

        bool isSilent = Config.MedicItems.Values.Any(item =>
            StoreApi != null &&
            StoreApi.IsItemEquipped(activator.SteamID, item.Id, activator.TeamNum) &&
            item.Flags.Contains("silent", StringComparison.OrdinalIgnoreCase));

        var equippedItem = Config.MedicItems.Values.FirstOrDefault(item =>
            StoreApi != null &&
            StoreApi.IsItemEquipped(activator.SteamID, item.Id, activator.TeamNum));

        if (activator.TeamNum == (byte)CsTeam.Spectator)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}Spectators cannot use this command.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (equippedItem == null)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You must equip a {ChatColors.Red}Medic item {ChatColors.Default}from the {ChatColors.Green}Store {ChatColors.Default}to use this command.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (!activator.PawnIsAlive)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You should be alive to use this command :)");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (Config.AccessFlag != "")
        {
            if (!AdminManager.PlayerHasPermissions(activator, Config.AccessFlag))
            {
                activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You do not have access to use this command.");
                if (!isSilent && Config.HealFailureSound != "")
                    activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
                return;
            }
        }

        if (_tries[activator.SteamID] <= 0)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}The limit has been reached. Total: {ChatColors.Red}{Config.MaxUse}");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        var pawn = activator.PlayerPawn.Value;
        if (pawn == null || pawn.Health > Config.MinHealth || pawn.Health == pawn.MaxHealth)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You cannot heal now. Health must be under {ChatColors.Red}{Config.MinHealth} HP.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        activator.InGameMoneyServices!.Account -= Config.Cost;
        int healing = Math.Min(equippedItem.HealingAmount, pawn.MaxHealth - pawn.Health);
        pawn.Health += healing;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        _tries[activator.SteamID]--;

        if (Config.ShowCall)
            Server.PrintToChatAll($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}Player {ChatColors.Green}{activator.PlayerName}{ChatColors.Default} used medic and restored {ChatColors.Red}{healing}hp");

        if (!isSilent && Config.HealSuccessSound != "")
            activator.ExecuteClientCommand($"play {Config.HealSuccessSound}");
    }

    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.MedicItems)
        {
            var medicItem = kvp.Value;
            if (uniqueId == medicItem.Id)
            {
                pawn.Health = Math.Min(pawn.MaxHealth, pawn.Health + medicItem.HealingAmount);
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_iHealth");
                break;
            }
        }
    }
    public void RegisterItems()
    {
        if (StoreApi == null)
            return;


        foreach (var kvp in Config.MedicItems)
        {
            var medicItem = kvp.Value;
            StoreApi.RegisterItem(
                medicItem.Id,
                medicItem.Name,
                Config.Category,
                medicItem.Type,
                medicItem.Price,
                medicItem.Description,
                duration: medicItem.Duration);
        }
    }
    public void UnregisterItems()
    {
        if (StoreApi == null)
            return;


        foreach (var kvp in Config.MedicItems)
        {
            var medicItem = kvp.Value;
            StoreApi.UnregisterItem(medicItem.Id);
        }
    }
}

public class PluginConfig
{
    public string Category { get; set; } = "Medic Items";
    public Dictionary<string, MedicItem> MedicItems { get; set; } = new()
    {
        {
            "1", new MedicItem
            {
                Id = "medic_kit_50_3_days",
                Name = "Medic 50HP 3 days",
                Price = 500,
                Duration = 259200,
                Type = "Healing",
                Description = "A basic healing kit to restore health.",
                Flags = "",
                HealingAmount = 50
            }
        },
        {
            "2", new MedicItem
            {
                Id = "medic_kit_50_no_limit",
                Name = "Medic 50HP no limit",
                Price = 5000,
                Duration = 259200,
                Type = "Healing",
                Description = "A basic healing kit to restore health.",
                Flags = "",
                HealingAmount = 50
            }
        },
        {
            "3", new MedicItem
            {
                Id = "medic_kit_60_3_days",
                Name = "Medic 60HP 3 days",
                Price = 10000,
                Duration = 259200,
                Type = "Healing",
                Description = "A basic healing kit to restore health.",
                Flags = "",
                HealingAmount = 60
            }
        },
        {
            "4", new MedicItem
            {
                Id = "medic_kit_50_silent",
                Name = "SilentMedic 50HP nolimit",
                Price = 5000,
                Duration = 0,
                Type = "Healing",
                Description = "A quiet healing kit.",
                Flags = "silent",
                HealingAmount = 50
            }
        }
    };

    public int MinHealth { get; set; } = 100;
    public int Cost { get; set; } = 0;
    public bool ShowCall { get; set; } = true;
    public int MaxUse { get; set; } = 1;
    public string AccessFlag { get; set; } = "";
    public string HealSuccessSound { get; set; } = "items/healthshot_success_01";
    public string HealFailureSound { get; set; } = "buttons/blip2";
}

public class MedicItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int HealingAmount { get; set; } = 0;
}
