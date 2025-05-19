using CounterStrikeSharp.API.Core;
using StoreAPI;
using CS2ScreenMenuAPI;
using T3MenuSharedApi;
using CounterStrikeSharp.API.Core.Capabilities;
using MySqlConnector;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;

namespace StoreCore;

public class TopCredits : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] TopCredits";
    public override string ModuleVersion => "1.0.0";

    public IStoreAPI? StoreApi;
    public PluginConfig Config { get; set; } = new PluginConfig();
    public IT3MenuManager? MenuManager;
    public IT3MenuManager? GetMenuManager()
    {
        if (MenuManager == null)
        {
            MenuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get();
        }

        return MenuManager;
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!");
        Config = StoreApi.GetModuleConfig<PluginConfig>("TopCredits");

        foreach (var cmd in Config.Commands)
        {
            AddCommand($"css_{cmd}", "Shows top players", Command_TopCredits);
        }
    }
    public void Command_TopCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        switch (Config.MenuType.ToLower())
        {
            case "screen":
                ShowScreenMenu(player);
                break;

            case "t3":
            case "t3menu":
                ShowT3Menu(player);
                break;
        }
    }
    public void ShowScreenMenu(CCSPlayerController player)
    {
        var topPlayers = GetTopCredits(Config.TopLimit);

        Menu menu = new Menu(player, this)
        {
            Title = Localizer.ForPlayer(player, "topcredits<title>", Config.TopLimit),
            ShowDisabledOptionNum = false
        };

        if (topPlayers.Count > 0)
        {
            int rank = 1;

            foreach (var (playerName, credits) in topPlayers)
            {
                string display = Localizer.ForPlayer(player, "topcredits.model", rank, playerName, credits);
                menu.AddItem(display, (p, o) => { rank++; }, true);
                rank++;
            }
        }
        else
        {
            menu.AddItem(Localizer.ForPlayer(player, "no.data"), (p, o) =>
            {

            }, true);
        }

        menu.Display();
    }
    public void ShowT3Menu(CCSPlayerController player)
    {
        var topPlayers = GetTopCredits(Config.TopLimit);
        IT3MenuManager? manager = GetMenuManager() ?? throw new Exception("T3MenuAPI not found");

        IT3Menu menu = manager.CreateMenu(Localizer.ForPlayer(player, "topcredits<title>", Config.TopLimit));

        if (topPlayers.Count > 0)
        {
            int rank = 1;
            foreach (var (playerName, credits) in topPlayers)
            {
                string display = Localizer.ForPlayer(player, "topcredits.model", rank, playerName, credits);

                menu.AddTextOption(display, true);
                rank++;
            }
        }
        manager.OpenMainMenu(player, menu);
    }
    public List<(string playerName, int credits)> GetTopCredits(int limit)
    {
        var referrals = new List<(string playerName, int credits)>();

        using (var connection = new MySqlConnection(GetDatabaseString()))
        {
            connection.Open();

            string query = $@"
                SELECT PlayerName, Credits
                FROM store_players
                ORDER BY Credits DESC
                LIMIT {Config.TopLimit}";

            using (var command = new MySqlCommand(query, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string playerName = reader.GetString("PlayerName");
                    int credits = reader.GetInt32("Credits");
                    referrals.Add((playerName, credits));
                }
            }
        }
        return referrals;
    }
    public string GetDatabaseString()
    {
        if (StoreApi == null)
            throw new Exception("StoreApi not found!");

        return StoreApi.GetDatabaseString();
    }
}
public class PluginConfig
{
    public string MenuType { get; set; } = "screen";
    public int TopLimit { get; set; } = 10;
    public List<string> Commands { get; set; } = ["topcredits", "topc"];
}