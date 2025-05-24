using CounterStrikeSharp.API.Core;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Timers;
using CS2ScreenMenuAPI;
using StoreAPI;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Menu;


namespace StoreCore;

public class Cases : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Cases";
    public override string ModuleVersion => "1.0.0";

    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();

    private Dictionary<CCSPlayerController, Timer> CaseTimers = new Dictionary<CCSPlayerController, Timer>();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Cases");

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
                true,
                true,
                false
                );
        }

        StoreApi.OnPlayerPurchaseItem += OnPlayerPurchaseItem;
        StoreApi.OnItemPreview += OnItemPreview;

        RegisterListener<OnServerPrecacheResources>((manifest) =>
        {
            foreach (var storeCase in Config.Cases)
            {
                foreach (var item in storeCase.Items)
                {
                    if (item.Type == "model")
                    {
                        manifest.AddResource(item.Value);
                    }
                }
            }
        });
    }
    public override void Unload(bool hotReload)
    {
        foreach (var timer in CaseTimers.Values)
        {
            timer.Kill();
        }
        CaseTimers.Clear();
    }
    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        var storeCase = Config.Cases.FirstOrDefault(c => c.Id == uniqueId);

        if (storeCase != null)
        {
            ShowCasePreview(player, storeCase);
        }
    }
    private void ShowCasePreview(CCSPlayerController player, Store_Case storeCase)
    {
        if (player == null)
            return;

        int totalWeight = storeCase.Items.Sum(item => item.Chance);
        if (totalWeight <= 0)
            return;

        int place = 1;

        player.PrintToChat(Localizer["case.content.title", storeCase.Name]);
        player.PrintToChat(Localizer["phrase.line.1"]);
        foreach (var item in storeCase.Items)
        {
            double percentage = (double)item.Chance / totalWeight * 100;
            string itemText = Localizer.ForPlayer(player, "case.content", place, item.Name, percentage);
            place++;
            player.PrintToChat(itemText);
        }
        player.PrintToChat(Localizer["phrase.line.2"]);
    }
    public void OnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        var storeCase = Config.Cases.FirstOrDefault(c => c.Id == item["uniqueid"]);

        if (storeCase != null)
        {
            OpenCaseMenu(player, storeCase);
            player.ExecuteClientCommand("play sounds/ui/csgo_ui_crate_open.vsnd_c");
        }
    }
    private void OpenCaseMenu(CCSPlayerController player, Store_Case storeCase)
    {
        if (player == null)
            return;

        Menu menu = new Menu(player, this)
        {
            Title = Localizer.ForPlayer(player, "case<menu>", storeCase.Name),
            ShowDisabledOptionNum = false,
            HasExitButon = false,
        };

        menu.AddItem(Localizer.ForPlayer(player, "case.loading"), (p, o) => { }, true);
        menu.Display();

        StartCaseAnimation(player, menu, storeCase);
    }
    private void StartCaseAnimation(CCSPlayerController player, Menu menu, Store_Case storeCase)
    {
        if (CaseTimers.ContainsKey(player))
        {
            CaseTimers[player].Kill();
            CaseTimers.Remove(player);
        }

        int animationTicks = 0;
        Random random = new Random();

        var timer = AddTimer(0.5f, () =>
        {
            animationTicks++;

            if (animationTicks < Config.AnimationDuration)
            {
                var randomItem = storeCase.Items[random.Next(storeCase.Items.Count)];
                var menuItem = menu.Options.FirstOrDefault();

                if (menuItem != null)
                {
                    menuItem.Text = Localizer.ForPlayer(player, "case.opening", randomItem.Name);
                    player.ExecuteClientCommand("play sounds/ui/csgo_ui_crate_item_scroll.vsnd_c");
                    menu.Refresh();
                }
            }
            else
            {
                var wonItem = GetRandomItemFromCase(storeCase);

                if (wonItem != null)
                {
                    var menuItem = menu.Options.FirstOrDefault();
                    if (menuItem != null)
                    {
                        menuItem.Text = Localizer.ForPlayer(player, "case.opening.finished", wonItem.Name);
                        menu.Refresh();
                    }

                    ProcessWonItem(player, wonItem, storeCase.Name);

                    if (Config.AnnounceRareItems && wonItem.Chance <= Config.RareItemThreshold)
                    {
                        Server.PrintToChatAll(Localizer["prefix"] + Localizer["case.opened.all", player.PlayerName, storeCase.Name, wonItem.Name]);
                        player.ExecuteClientCommand("play sounds/ui/item_drop3_rare.vsnd_c");
                    }
                    else
                    {
                        player.ExecuteClientCommand("play sounds/ui/item_drop1_common.vsnd_c");
                        player.PrintToChat(Localizer["prefix"] + Localizer["case.opened", wonItem.Name, storeCase.Name]);
                    }

                    AddTimer(3.0f, () =>
                    {
                        menu.Close(player);
                        if (CaseTimers.ContainsKey(player))
                        {
                            CaseTimers.Remove(player);
                        }
                    });
                }

                if (CaseTimers.ContainsKey(player))
                {
                    CaseTimers[player].Kill();
                    CaseTimers.Remove(player);
                }
            }
        }, TimerFlags.REPEAT);

        CaseTimers[player] = timer;
    }
    private Case_Item? GetRandomItemFromCase(Store_Case storeCase)
    {
        if (storeCase.Items.Count == 0)
            return null;

        int totalWeight = storeCase.Items.Sum(item => item.Chance);
        if (totalWeight <= 0)
            return null;

        Random random = new Random();
        int randomValue = random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var item in storeCase.Items)
        {
            currentWeight += item.Chance;
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }

        return storeCase.Items.First();
    }

    private void ProcessWonItem(CCSPlayerController player, Case_Item item, string caseName)
    {
        if (StoreApi == null)
            return;

        switch (item.Type.ToLower())
        {
            case "credits":
                if (int.TryParse(item.Value, out int credits))
                {
                    StoreApi.AddClientCredits(player, credits);
                }
                break;

            case "model":
                player.PlayerPawn.Value?.SetModel(item.Value);
                break;

            case "command":
                string command = item.Value
                    .Replace("{PLAYERNAME}", player.PlayerName)
                    .Replace("{STEAMID}", player.SteamID.ToString())
                    .Replace("{USERID}", player.UserId.ToString());

                Server.ExecuteCommand(command);
                break;

            default:
                Logger.LogWarning("Unknown item type: {itemType}", item.Type);
                break;
        }
    }
}
public class PluginConfig
{
    public string Category { get; set; } = "Cases";
    public bool AnnounceRareItems { get; set; } = true;
    public int RareItemThreshold { get; set; } = 10;
    public int AnimationDuration { get; set; } = 5;
    public List<Store_Case> Cases { get; set; } = new List<Store_Case>()
    {
        new Store_Case
        {
            Id = "starter_case",
            Name = "Starter Case",
            Description = "A basic case with common items",
            Price = 1000,
            Items = new List<Case_Item>()
            {
                new Case_Item
                {
                    Name = "100 Credits",
                    Type = "credits",
                    Value = "100",
                    Chance = 50
                },
                new Case_Item
                {
                    Name = "500 Credits",
                    Type = "credits",
                    Value = "500",
                    Chance = 30
                },
                new Case_Item
                {
                    Name = "VIP for 1 day",
                    Type = "command",
                    Value = "css_vip_adduser VIP {STEAMID} 86400",
                    Chance = 15
                },
                new Case_Item
                {
                    Name = "Chicken Model",
                    Type = "model",
                    Value = "models/chicken/chicken.vmdl",
                    Chance = 5
                }
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
}