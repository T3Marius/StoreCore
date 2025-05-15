using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;

namespace StoreCore;

public class MedicPlugin : BasePlugin
{
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleName => "[Store] Medic Plugin";
    public override string ModuleVersion => "1.0.0";
    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private Dictionary<ulong, int> _tries = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Medic");

        if (!hotReload)
        {
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

        StoreApi.OnItemPreview += OnItemPreview;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        if (victim == null || attacker == null || victim == attacker)
            return HookResult.Continue;

        CCSPlayerPawn? attackerPawn = attacker.PlayerPawn.Value;

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

        bool hasEquippedMedicItem = Config.MedicItems.Values.Any(item =>
        StoreApi != null &&
        StoreApi.IsItemEquipped(activator.SteamID, item.Id, activator.TeamNum));

        if (activator.TeamNum == (byte)CsTeam.Spectator)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}Spectators cannot use this command.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (!hasEquippedMedicItem)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You must equip a {ChatColors.Red}Medic item {ChatColors.Default}from the store to use this command.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (!activator.PawnIsAlive)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You should be alive to use this command.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (Config.AccessFlag != "")
        {
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

        if (activator.PlayerPawn.Value != null && activator.PlayerPawn.Value.Health > Config.MinHealth)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}Too much health to use medic. Need: {ChatColors.Red}{Config.MinHealth}hp or less");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        if (activator.PlayerPawn.Value != null && activator.PlayerPawn.Value.Health == activator.PlayerPawn.Value.MaxHealth)
        {
            activator.PrintToChat($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}You are already at full health.");
            if (!isSilent && Config.HealFailureSound != "")
                activator.ExecuteClientCommand($"play {Config.HealFailureSound}");
            return;
        }

        activator.InGameMoneyServices!.Account -= Config.Cost;

        var total = activator.PlayerPawn.Value != null && (activator.PlayerPawn.Value.MaxHealth >= activator.PlayerPawn.Value.Health + Config.HealHealth)
            ? Config.HealHealth
            : activator.PlayerPawn.Value!.MaxHealth - activator.PlayerPawn.Value.Health;

        activator.PlayerPawn.Value.Health += total;
        Utilities.SetStateChanged(activator.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

        _tries[activator.SteamID]--;

        if (Config.ShowCall)
            Server.PrintToChatAll($" {ChatColors.Green}[StoreMedic] {ChatColors.Default}Player {ChatColors.Green}{activator.PlayerName}{ChatColors.Default} used medic and restored {ChatColors.Red}{total}hp");

        if (!isSilent && Config.HealSuccessSound != "")
            activator.ExecuteClientCommand($"play {Config.HealSuccessSound}");
    }

    private bool HasAccess(CCSPlayerController player)
    {
        return AdminManager.PlayerHasPermissions(player, Config.AccessFlag);
    }

    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
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
}

public class PluginConfig
{
    public string Category { get; set; } = "Medic Items";
    public Dictionary<string, MedicItem> MedicItems { get; set; } = new Dictionary<string, MedicItem>()
    {
        {
            "1", new MedicItem
            {
                Id = "medic_kit_50_3_days",
                Name = "Medic (50 HP) 3 days",
                Price = 500,
                Duration = 259200,
                Type = "Healing",
                HealingAmount = 50,
                Description = "A basic healing kit to restore health.",
                Flags = ""
            }
        },
        {
            "2", new MedicItem
            {
                Id = "medic_kit_50_no_limit",
                Name = "Medic (50 HP) no limit",
                Price = 5000,
                Duration = 259200,
                Type = "Healing",
                HealingAmount = 50,
                Description = "A basic healing kit to restore health.",
                Flags = ""
            }
        },
        {
            "3", new MedicItem
            {
                Id = "medic_kit_60_3_days",
                Name = "Medic (60 HP) 3 days",
                Price = 10000,
                Duration = 259200,
                Type = "Healing",
                HealingAmount = 60,
                Description = "A basic healing kit to restore health.",
                Flags = ""
            }
        },
        {
            "4", new MedicItem
            {
                Id = "medic_kit_50_silent",
                Name = "SilentMedic(50 HP)nolimit",
                Price = 5000,
                Duration = 0,
                Type = "Healing",
                HealingAmount = 50,
                Description = "A quiet healing kit.",
                Flags = "silent"
            }
        }
    };

    public int MinHealth { get; init; } = 100;
    public int HealHealth { get; init; } = 50;
    public int Cost { get; init; } = 0;
    public bool ShowCall { get; init; } = true;
    public int MaxUse { get; init; } = 1;
    public string AccessFlag { get; init; } = "";
    public string HealSuccessSound { get; init; } = "items/healthshot_success_01";
    public string HealFailureSound { get; init; } = "buttons/blip2";
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