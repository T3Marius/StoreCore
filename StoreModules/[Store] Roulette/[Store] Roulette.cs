using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CS2ScreenMenuAPI;
using StoreAPI;
using System.Collections.Concurrent;
using T3MenuSharedApi;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace StoreCore;

public class Roulette : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Roulette";
    public override string ModuleVersion => "1.0.0";

    private readonly ConcurrentDictionary<ulong, RoulettePlayer> _activeBets = new();
    private readonly ConcurrentDictionary<ulong, Timer> _animationTimers = new();
    private readonly Random _random = new();
    private IT3MenuManager MenuManager = null!;
    private PluginConfig Config { get; set; } = new();
    private IStoreAPI StoreApi = null!;

    private IT3MenuManager GetMenuManager()
    {
        return new PluginCapability<IT3MenuManager>("t3menu:manager").Get() ?? throw new Exception("T3MenuAPI not found!");
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found! Please check plugin.");
        Config = StoreApi.GetModuleConfig<PluginConfig>("Roulette");

        foreach (var cmd in Config.Commands)
        {
            AddCommand($"css_{cmd}", "Bet for roulette", Command_Bet);
        }

        switch (StoreApi.GetMenuType())
        {
            case MenuType.T3Menu:
                GetMenuManager();
                break;
        }
    }

    [CommandHelper(minArgs: 1, usage: "<betamount>")]
    public void Command_Bet(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (_activeBets.ContainsKey(player.SteamID))
        {
            info.ReplyToCommand(Localizer["prefix"] + Localizer["already.bet"]);
            return;
        }

        if (!int.TryParse(info.GetArg(1), out int betAmount))
        {
            info.ReplyToCommand(Localizer["prefix"] + Localizer["invalid.bet.amount"]);
            return;
        }

        int credits = StoreApi.GetClientCredits(player);
        if (credits < betAmount)
        {
            info.ReplyToCommand(Localizer["prefix"] + Localizer["no.credits"]);
            return;
        }

        if (betAmount < Config.MinBet)
        {
            info.ReplyToCommand(Localizer["prefix"] + Localizer["min.bet", Config.MinBet]);
            return;
        }

        if (betAmount > Config.MaxBet)
        {
            info.ReplyToCommand(Localizer["prefix"] + Localizer["max.bet", Config.MaxBet]);
            return;
        }

        var menuType = StoreApi.GetMenuType();

        if (menuType == MenuType.ScreenMenu)
        {
            OpenColorChoiceMenu_Screen(player, betAmount);
        }
        else
        {
            OpenColorChoiceMenu_T3(player, betAmount);
        }
    }

    private void PlaceBet(CCSPlayerController player, string color, int amount)
    {
        StoreApi.RemoveClientCredits(player, amount);

        var bet = new RoulettePlayer
        {
            Addicted = player,
            BetAmount = amount,
            Color = color
        };

        _activeBets[player.SteamID] = bet;
        player.PrintToChat(Localizer["prefix"] + Localizer["bet.placed", amount, GetChatColorText(color)]);

        string winningColor = DetermineRouletteResult();
        var menuType = StoreApi.GetMenuType();

        if (menuType == MenuType.ScreenMenu)
        {
            StartRouletteAnimation_Screen(player, bet, winningColor);
        }
        else
        {
            StartRouletteAnimation_T3(player, bet, winningColor);
        }
    }

    #region T3Menu Implementation
    private void OpenColorChoiceMenu_T3(CCSPlayerController player, int betAmount)
    {
        IT3Menu menu = MenuManager.CreateMenu(Localizer.ForPlayer(player, "betting.menu", betAmount));
        menu.AddOption(Localizer.ForPlayer(player, "red.option"), (p, o) => PlaceBet(p, "Red", betAmount));
        menu.AddOption(Localizer.ForPlayer(player, "black.option"), (p, o) => PlaceBet(p, "Black", betAmount));
        menu.AddOption(Localizer.ForPlayer(player, "green.option"), (p, o) => PlaceBet(p, "Green", betAmount));
        MenuManager.OpenMainMenu(player, menu);
    }

    private void StartRouletteAnimation_T3(CCSPlayerController player, RoulettePlayer bet, string winningColor)
    {
        IT3Menu animationMenu = MenuManager.CreateMenu(Localizer.ForPlayer(player, "spinning.menu"));
        animationMenu.IsExitable = false;
        animationMenu.AddOption(Localizer.ForPlayer(player, "your.bet", bet.BetAmount, GetMenuColorText(bet.Color)), (p, o) => { }, true);
        MenuManager.OpenMainMenu(player, animationMenu);

        int animationSteps = 0;
        string[] spinStates = { "Red", "Black", "Green" };
        Timer? animationTimer = null;

        animationTimer = AddTimer(0.15f, () =>
        {
            if (!player.IsValid || !_activeBets.ContainsKey(player.SteamID))
            {
                animationTimer?.Kill();
                _animationTimers.TryRemove(player.SteamID, out _);
                return;
            }

            animationSteps++;
            if (animationSteps >= 20)
            {
                animationTimer?.Kill();
                _animationTimers.TryRemove(player.SteamID, out _);
                animationMenu.Title = Localizer.ForPlayer(player, "spinning.menu") + $" {GetMenuColorText(winningColor)}";
                MenuManager.Refresh();
                AddTimer(1.5f, () => ShowRouletteResult(player, bet, winningColor));
            }
            else
            {
                string currentSpin = spinStates[_random.Next(spinStates.Length)];
                animationMenu.Title = Localizer.ForPlayer(player, "spinning.menu") + $" {GetMenuColorText(currentSpin)}";
                MenuManager.Refresh();
            }
        }, TimerFlags.REPEAT);
        _animationTimers[player.SteamID] = animationTimer;
    }
    #endregion

    #region ScreenMenu Implementation
    private void OpenColorChoiceMenu_Screen(CCSPlayerController player, int betAmount)
    {
        var menu = new Menu(player, this)
        {
            Title = Localizer.ForPlayer(player, "betting.menu", betAmount)
        };
        menu.AddItem(Localizer.ForPlayer(player, "red.option.screen"), (p, o) => PlaceBet(p, "Red", betAmount));
        menu.AddItem(Localizer.ForPlayer(player, "black.option.screen"), (p, o) => PlaceBet(p, "Black", betAmount));
        menu.AddItem(Localizer.ForPlayer(player, "green.option.screen"), (p, o) => PlaceBet(p, "Green", betAmount));
        menu.Display();
    }

    private void StartRouletteAnimation_Screen(CCSPlayerController player, RoulettePlayer bet, string winningColor)
    {
        var animationMenu = new Menu(player, this)
        {
            Title = Localizer.ForPlayer(player, "spinning.menu")
        };
        animationMenu.AddItem(Localizer.ForPlayer(player, "your.bet", bet.BetAmount, bet.Color), (p, o) => { }, true);
        animationMenu.Display();

        int animationSteps = 0;
        string[] spinStates = { "Red", "Black", "Green" };
        Timer? animationTimer = null;

        animationTimer = AddTimer(0.15f, () =>
        {
            if (!player.IsValid || !_activeBets.ContainsKey(player.SteamID))
            {
                animationTimer?.Kill();
                _animationTimers.TryRemove(player.SteamID, out _);
                return;
            }

            animationSteps++;
            if (animationSteps >= 20)
            {
                animationTimer?.Kill();
                _animationTimers.TryRemove(player.SteamID, out _);
                animationMenu.Title = Localizer.ForPlayer(player, "spinning.menu") + $" {winningColor}";
                animationMenu.Refresh();
                AddTimer(1.5f, () =>
                {
                    animationMenu.Close(player);
                    ShowRouletteResult(player, bet, winningColor);
                });
            }
            else
            {
                string currentSpin = spinStates[_random.Next(spinStates.Length)];
                animationMenu.Title = Localizer.ForPlayer(player, "spinning.menu") + $" {currentSpin}";
                animationMenu.Refresh();
            }
        }, TimerFlags.REPEAT);
        _animationTimers[player.SteamID] = animationTimer;
    }
    #endregion

    #region Shared Logic
    private void ShowRouletteResult(CCSPlayerController player, RoulettePlayer bet, string winningColor)
    {
        if (StoreApi.GetMenuType() == MenuType.T3Menu)
        {
            AddTimer(2.0f, () => MenuManager.CloseMenu(player));
        }

        string result = winningColor;
        bool won = result == bet.Color;
        int payout = won ? (int)CalculatePayout(bet.BetAmount, result) : 0;

        if (won)
        {
            StoreApi.AddClientCredits(player, payout);
            player.PrintToChat(Localizer["prefix"] + Localizer["you.won", GetChatColorText(result), payout]);
        }
        else
        {
            player.PrintToChat(Localizer["prefix"] + Localizer["you.lost", GetChatColorText(result)]);
        }
        _activeBets.TryRemove(player.SteamID, out _);
    }

    private string DetermineRouletteResult()
    {
        double roll = _random.NextDouble() * 100;
        if (roll < Config.RedChance) return "Red";
        if (roll < Config.RedChance + Config.BlackChance) return "Black";
        return "Green";
    }

    private double CalculatePayout(int betAmount, string result)
    {
        return result switch
        {
            "Red" => betAmount * Config.RedMultiplier,
            "Black" => betAmount * Config.BlackMultiplier,
            "Green" => betAmount * Config.GreenMultiplier,
            _ => 0
        };
    }

    private string GetMenuColorText(string color)
    {
        return color switch
        {
            "Red" => $"<font color='red'>{Localizer["red"]}</font>",
            "Black" => $"<font color='#606060'>{Localizer["black"]}</font>",
            "Green" => $"<font color='green'>{Localizer["green"]}</font>",
            _ => color
        };
    }

    private string GetChatColorText(string color)
    {
        return color switch
        {
            "Red" => $" {ChatColors.Red}{Localizer["red"]}{ChatColors.Default}",
            "Black" => $" {ChatColors.Silver}{Localizer["black"]}{ChatColors.Default}",
            "Green" => $" {ChatColors.Green}{Localizer["green"]}{ChatColors.Default}",
            _ => color
        };
    }
    #endregion
}

public class RoulettePlayer
{
    public required CCSPlayerController Addicted { get; init; }
    public required int BetAmount { get; init; }
    public required string Color { get; init; }
}

public class PluginConfig
{
    public int MinBet { get; set; } = 10;
    public int MaxBet { get; set; } = 500;
    public List<string> Commands { get; set; } = ["roulette"];
    public double RedChance { get; set; } = 45.0;
    public double BlackChance { get; set; } = 45.0;
    public double GreenChance { get; set; } = 10.0;
    public double RedMultiplier { get; set; } = 2;
    public double BlackMultiplier { get; set; } = 2;
    public double GreenMultiplier { get; set; } = 6;
}