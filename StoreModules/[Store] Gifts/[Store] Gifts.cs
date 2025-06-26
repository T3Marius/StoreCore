using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using StoreAPI;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Utils;

namespace StoreCore;

public class Gifts : BasePlugin
{
    public override string ModuleName => "[Store] Gifts";
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleVersion => "1.0.1";

    public IStoreAPI? StoreApi;
    public GiftPluginConfig Config { get; set; } = new();
    private readonly Random _random = new();
    private bool _giftGiven = false;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("[Store Gifts] StoreAPI not found!");
        Config = StoreApi.GetModuleConfig<GiftPluginConfig>("Gifts") ?? new GiftPluginConfig();
        StoreApi.SaveModuleConfig("Gifts", Config);

        // Cadouri oferite când jocul anunță ultima rundă a unei jumătăți (inclusiv finalul meciului)
        RegisterEventHandler<EventRoundAnnounceLastRoundHalf>(OnLastRoundAnnounce);

        // Resetăm flagul la schimbarea hărții
        RegisterListener<Listeners.OnMapStart>(name => _giftGiven = false);
    }

    private HookResult OnLastRoundAnnounce(EventRoundAnnounceLastRoundHalf evt, GameEventInfo info)
    {
        if (_giftGiven)
            return HookResult.Continue;

        _giftGiven = true;

        foreach (var player in Utilities.GetPlayers().Where(p =>
            p.IsValid && !p.IsBot && p.TeamNum != (byte)CsTeam.Spectator)) // 👈 verificare adăugată
        {
            var gift = GetRandomGift();
            if (gift != null)
            {
                GiveGiftToPlayer(player, gift);
                player.ExecuteClientCommand("sounds/ui/item_drop_card_reward.vsnd_c");
                player.PrintToCenter($"🎁 You received a gift: {gift.Name}!");
                player.PrintToChat($"[Store] ******************************");
                player.PrintToChat($"[Store] 🎁 You received a gift: {gift.Name}!");
                player.PrintToChat($"[Store] ******************************");
                player.ExecuteClientCommand("play sounds/ui/item_drop1_common.vsnd_c");
                Logger.LogInformation($"[Gifts] {player.PlayerName} ({player.SteamID}) received: {gift.Name} [{gift.Type}:{gift.Value}]");
            }
        }

        return HookResult.Continue;
    }


    private GiftItem? GetRandomGift()
    {
        int totalChance = Config.Gifts.Sum(g => g.Chance);
        if (totalChance <= 0) return null;

        int roll = _random.Next(1, totalChance + 1);
        int accumulated = 0;

        foreach (var gift in Config.Gifts)
        {
            accumulated += gift.Chance;
            if (roll <= accumulated)
                return gift;
        }

        return Config.Gifts.FirstOrDefault();
    }

    private void GiveGiftToPlayer(CCSPlayerController player, GiftItem gift)
    {
        if (!player.IsValid) return;

        switch (gift.Type.ToLowerInvariant())
        {
            case "credits":
                if (int.TryParse(gift.Value, out int credits))
                    StoreApi?.AddClientCredits(player, credits);
                break;

            case "model":
                if (player.PlayerPawn.IsValid)
                    player.PlayerPawn.Value?.SetModel(gift.Value);
                break;

            case "command":
                string cmd = gift.Value
                    .Replace("{STEAMID}", player.SteamID.ToString())
                    .Replace("{PLAYERNAME}", player.PlayerName ?? "Player");
                Server.ExecuteCommand(cmd);
                break;

            default:
                Logger.LogWarning($"[Gifts] Unknown gift type: {gift.Type}");
                break;
        }
    }
}

public class GiftPluginConfig
{
    public List<GiftItem> Gifts { get; set; } = new()
    {
        new GiftItem { Name = "100 Credits", Type = "credits", Value = "100", Chance = 40 },
        new GiftItem { Name = "1000 Credits", Type = "credits", Value = "1000", Chance = 30 },
        new GiftItem { Name = "2000 Credits", Type = "credits", Value = "2000", Chance = 15 },
        new GiftItem { Name = "5000 Credits", Type = "credits", Value = "5000", Chance = 10 },
        new GiftItem { Name = "VIP 1 day", Type = "command", Value = "css_vip_adduser {steamid} VIP 86400", Chance = 5 },
        //new GiftItem { Name = "Chicken Model", Type = "model", Value = "models/chicken/chicken.vmdl", Chance = 20 },
    };
}

public class GiftItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Chance { get; set; } = 0;
}