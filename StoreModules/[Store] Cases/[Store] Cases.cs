using CounterStrikeSharp.API.Core;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Timers;
using StoreAPI;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using CS2ScreenMenuAPI;


namespace StoreCore;

public class Cases : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Cases";
    public override string ModuleVersion => "1.1.1";

    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private readonly Dictionary<CCSPlayerController, Timer> _caseTimers = new Dictionary<CCSPlayerController, Timer>();
    private readonly HashSet<CCSPlayerController> _playerOpeningCase = new HashSet<CCSPlayerController>();
    private readonly Random _random = new Random();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("[Store Cases] StoreAPI not found! Plugin cannot load.");

        var loadedConfig = StoreApi.GetModuleConfig<PluginConfig>("Cases");
        if (loadedConfig != null)
        {
            Config = loadedConfig;
        }


        foreach (var storeCase in Config.Cases)
        {
            StoreApi.RegisterItem(
                storeCase.Id,
                storeCase.Name,
                Config.Category,
                "store_case",
                storeCase.Price,
                storeCase.Description,
                storeCase.Flags,
                isEquipable: false
            );
        }

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
        StoreApi.OnItemPreview += OnItemPreview;

        RegisterListener<OnServerPrecacheResources>(OnServerPrecacheResourcesHandler);
        RegisterListener<OnClientDisconnect>(OnClientDisconnect);
    }

    public override void Unload(bool hotReload)
    {
        foreach (var timer in _caseTimers.Values)
        {
            timer.Kill();
        }
        _caseTimers.Clear();
        _playerOpeningCase.Clear();
    }

    private void OnServerPrecacheResourcesHandler(ResourceManifest manifest)
    {
        if (Config?.Cases == null) return;

        foreach (var storeCase in Config.Cases)
        {
            foreach (var item in storeCase.Items)
            {
                if (item.Type.Equals("model", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.Value))
                {
                    manifest.AddResource(item.Value);
                }
            }
        }
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;


        if (_caseTimers.TryGetValue(player, out var timer))
        {
            timer.Kill();
            _caseTimers.Remove(player);
        }
    }

    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        if (player == null || !player.IsValid) return;

        var storeCase = Config.Cases.FirstOrDefault(c => c.Id == uniqueId);
        if (storeCase != null)
        {
            ShowCasePreview(player, storeCase);
        }
    }

    private void ShowCasePreview(CCSPlayerController player, Store_Case storeCase)
    {
        if (player == null || !player.IsValid) return;

        int totalWeight = storeCase.Items.Sum(item => item.Chance);
        if (totalWeight <= 0)
        {
            player.PrintToChat(Localizer["case.preview.no_items"]);
            return;
        }

        player.PrintToChat(Localizer["case.content.title", storeCase.Name]);
        player.PrintToChat(Localizer["phrase.line.1"]);

        int place = 1;
        foreach (var item in storeCase.Items.OrderByDescending(i => i.Chance))
        {
            double percentage = (double)item.Chance / totalWeight * 100;
            player.PrintToChat(Localizer.ForPlayer(player, "case.content", place, item.Name, percentage.ToString("0.##")));
            place++;
        }
        player.PrintToChat(Localizer["phrase.line.2"]);
    }

    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        if (player == null || !player.IsValid) return;
        if (!itemData.TryGetValue("uniqueid", out var uniqueId)) return;

        var storeCase = Config.Cases.FirstOrDefault(c => c.Id == uniqueId);
        if (storeCase != null)
        {
            OpenCaseMenu(player, storeCase);
        }
    }

    private void OpenCaseMenu(CCSPlayerController player, Store_Case storeCase)
    {
        if (player == null || !player.IsValid) return;

        if (_playerOpeningCase.Contains(player))
        {
            player.PrintToChat(Localizer["prefix"] + Localizer["case.already.opening"]);
            return;
        }
        _playerOpeningCase.Add(player);

        Menu screenMenu = new Menu(player, this)
        {
            Title = Localizer.ForPlayer(player, "case<menu>", storeCase.Name),
            ShowDisabledOptionNum = false,
            HasExitButon = false,
        };

        screenMenu.AddItem(Localizer.ForPlayer(player, "case.loading"), (p, o) => { }, true);
        screenMenu.Display();

        player.ExecuteClientCommand("play sounds/ui/csgo_ui_crate_open.vsnd_c");
        StartCaseAnimation(player, screenMenu, storeCase);
    }

    private void StartCaseAnimation(CCSPlayerController player, CS2ScreenMenuAPI.Menu menu, Store_Case storeCase)
    {
        if (_caseTimers.ContainsKey(player))
        {
            _caseTimers[player].Kill();
            _caseTimers.Remove(player);
        }

        int animationTicks = 0;
        float animationInterval = 0.3f;
        int totalAnimationTicks = (int)(Config.AnimationDuration / animationInterval);

        var timer = AddTimer(animationInterval, () =>
        {
            if (!player.IsValid || !player.Pawn.IsValid)
            {
                CleanUpCaseOpening(player, menu, _caseTimers.ContainsKey(player) ? _caseTimers[player] : null, aborted: true);
                return;
            }

            animationTicks++;

            if (animationTicks < totalAnimationTicks)
            {
                var randomItem = storeCase.Items[_random.Next(storeCase.Items.Count)];

                if (menu.Options.Count > 0)
                {
                    var menuItem = menu.Options[0];
                    menuItem.Text = Localizer.ForPlayer(player, "case.opening", randomItem.Name);
                    menu.Refresh();
                }
                player.ExecuteClientCommand("play sounds/ui/csgo_ui_crate_item_scroll.vsnd_c");
            }
            else
            {
                CleanUpCaseOpening(player, menu, _caseTimers.ContainsKey(player) ? _caseTimers[player] : null, aborted: false, storeCase);
            }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

        _caseTimers[player] = timer;
    }

    private void CleanUpCaseOpening(CCSPlayerController player, CS2ScreenMenuAPI.Menu menu, Timer? animationTimer, bool aborted, Store_Case? storeCase = null)
    {
        if (animationTimer != null)
        {
            animationTimer.Kill();
        }
        _caseTimers.Remove(player);

        if (!player.IsValid)
        {
            _playerOpeningCase.Remove(player);
            return;
        }

        if (aborted || storeCase == null)
        {
            AddTimer(0.1f, () =>
            {
                if (player.IsValid) menu.Close(player);
                _playerOpeningCase.Remove(player);
            });
            return;
        }

        var wonItem = GetRandomItemFromCase(storeCase);

        if (menu.Options.Count > 0)
        {
            var menuItem = menu.Options[0];
            if (wonItem != null)
            {
                menuItem.Text = Localizer.ForPlayer(player, "case.opening.finished", wonItem.Name);
            }
            else
            {
                menuItem.Text = Localizer.ForPlayer(player, "case.error.no_item_won");
                Logger.LogWarning($"[Cases] Player {player.PlayerName} won no item from {storeCase.Name}. Case items count: {storeCase.Items.Count}. Check config.");
                player.PrintToChat(Localizer["prefix"] + Localizer["case.error.no_item_won_user"]);
            }
            menu.Refresh();
        }


        if (wonItem != null)
        {
            ProcessWonItem(player, wonItem, storeCase.Name);

            if (Config.AnnounceRareItems && wonItem.Rarity <= Config.RareItemThreshold)
            {
                Server.PrintToChatAll(Localizer["prefix"] + Localizer["case.opened.all", player.PlayerName, storeCase.Name, wonItem.Name]);
                player.ExecuteClientCommand($"play {Config.SoundRareItem}");
            }
            else
            {
                player.PrintToChat(Localizer["prefix"] + Localizer["case.opened", wonItem.Name, storeCase.Name]);
                player.ExecuteClientCommand($"play {Config.SoundCommonItem}");
            }
        }

        AddTimer(Config.ResultDisplayDuration > 0 ? Config.ResultDisplayDuration : 3.0f, () =>
        {
            if (player.IsValid) menu.Close(player);
            _playerOpeningCase.Remove(player);
        });
    }

    private Case_Item? GetRandomItemFromCase(Store_Case storeCase)
    {
        if (storeCase.Items == null || !storeCase.Items.Any()) return null;

        int totalWeight = storeCase.Items.Sum(item => item.Chance);
        if (totalWeight <= 0) return null;

        int randomValue = _random.Next(1, totalWeight + 1);
        int currentWeight = 0;
        foreach (var item in storeCase.Items)
        {
            if (item.Chance <= 0) continue;
            currentWeight += item.Chance;
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }
        return storeCase.Items.FirstOrDefault(i => i.Chance > 0);
    }

    private void ProcessWonItem(CCSPlayerController player, Case_Item item, string caseName)
    {
        if (StoreApi == null || !player.IsValid) return;


        switch (item.Type.ToLowerInvariant())
        {
            case "credits":
                if (int.TryParse(item.Value, out int credits))
                {
                    StoreApi.AddClientCredits(player, credits);
                }
                else Logger.LogWarning($"[Cases] Invalid credit amount '{item.Value}' for item '{item.Name}'.");
                break;

            case "model":
                if (player.PlayerPawn.IsValid && player.PlayerPawn.Value != null)
                {
                    player.PlayerPawn.Value.SetModel(item.Value);
                }
                else Logger.LogWarning($"[Cases] Could not set model for {player.PlayerName}. PlayerPawn invalid.");
                break;

            case "command":
                string command = item.Value
                    .Replace("{PLAYERNAME}", player.PlayerName ?? "Player")
                    .Replace("{STEAMID}", player.SteamID.ToString())
                    .Replace("{STEAMID64}", player.SteamID.ToString())
                    .Replace("{USERID}", player.UserId.HasValue ? player.UserId.Value.ToString() : "N/A");
                Server.ExecuteCommand(command);
                break;

            default:
                Logger.LogWarning($"[Cases] Unknown item type: '{item.Type}' for item '{item.Name}'.");
                break;
        }
    }
}

public class PluginConfig
{
    public string Category { get; set; } = "Cases";
    public bool AnnounceRareItems { get; set; } = true;
    public int RareItemThreshold { get; set; } = 10;
    public float AnimationDuration { get; set; } = 5.0f;
    public float ResultDisplayDuration { get; set; } = 3.0f;
    public string SoundRareItem { get; set; } = "sounds/ui/item_drop_card_reward.vsnd_c";
    public string SoundCommonItem { get; set; } = "sounds/ui/item_drop1_common.vsnd_c";
    public List<Store_Case> Cases { get; set; } = new List<Store_Case>()
    {
        new Store_Case
        {
            Id = "starter_case",
            Name = "Starter Case",
            Description = "A basic case with common items.",
            Price = 1000,
            Flags = "",
            Items = new List<Case_Item>()
            {
                new Case_Item { Name = "100 Credits", Type = "credits", Value = "100", Chance = 50, Rarity = 50 },
                new Case_Item { Name = "500 Credits", Type = "credits", Value = "500", Chance = 30, Rarity = 30 },
                new Case_Item { Name = "VIP for 1 day", Type = "command", Value = "css_vip_adduser VIP {STEAMID} 86400", Chance = 15, Rarity = 15 },
                new Case_Item { Name = "Chicken Model", Type = "model", Value = "models/chicken/chicken.vmdl", Chance = 5, Rarity = 5 }
            }
        },
    };
}

public class Store_Case
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public string Flags { get; set; } = string.Empty;
    public List<Case_Item> Items { get; set; } = new List<Case_Item>();
}

public class Case_Item
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Chance { get; set; } = 0;
    public int Rarity { get; set; } = 0;
}