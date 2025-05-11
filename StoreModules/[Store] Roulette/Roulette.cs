using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using StoreAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using T3MenuSharedApi;

namespace StoreCore_Roulette
{
    public partial class Roulette : BasePlugin, IPluginConfig<RouletteConfig>
    {
        public override string ModuleName => "StoreCore Roulette";
        public override string ModuleVersion => "0.0.3";
        public override string ModuleAuthor => "varkit & thanks for the jinn";
        public RouletteConfig Config { get; set; }
        public IStoreAPI? StoreApi { get; set; }
        public IT3MenuManager? MenuManager;
        public string prefix { get; set; }
        public Random random = new Random();

        public record Bet(CCSPlayerController player, Color color, int betamount);
        public List<Bet> ActiveBets = new List<Bet>();

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found");
            foreach (var item in Config.CommandsForRoulette)
            {
                AddCommand(item, "", RouletteCommand);
            }
        }

        public IT3MenuManager? GetMenuManager()
        {
            if (MenuManager == null)
            {
                MenuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get();
            }
            return MenuManager;
        }

        public void OnConfigParsed(RouletteConfig config)
        {
            Config = config;
            prefix = config.Prefix.ReplaceColorTags();
        }

        public void RouletteCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (StoreApi == null) return;
            if (!caller.IsValid || caller.IsBot || caller.IsHLTV) return;
            var playercredit = GetPlayerCredit(caller);
            if (!int.TryParse(command.GetArg(1), out int betamount) || betamount < Config.MinimumBet || betamount > Config.MaximumBet)
            {
                reply(caller, Localizer["Roulette_BetAmountError", Config.MinimumBet, Config.MaximumBet]);
                return;
            }

            if (ActiveBets.Any(b => b.player == caller))
            {
                reply(caller, Localizer["Roulette_AlreadyBet"]);
                return;
            }

            if (GetPlayerCredit(caller) < betamount)
            {
                reply(caller, Localizer["Roulette_NotEnoughMoney"]);
                return;
            }

            RouletteMenu(caller, betamount);
        }

        public void RouletteMenu(CCSPlayerController player, int betamount)
        {
            var manager = GetMenuManager();
            var LocalizedTitle = Localizer["RouletteTitle"];
            var L_Red = Localizer["Red"];
            var L_Blue = Localizer["Blue"];
            var L_Green = Localizer["Green"];

            if (manager == null)
                return;

            IT3Menu menu = manager.CreateMenu($"<img src='https://raw.githubusercontent.com/vulikit/varkit-resources/refs/heads/main/rulet1.gif'> <font color='#4bd932'>{LocalizedTitle}</font> <font color='#FFFFFF'>|</font> <font color='#3250d9'>{betamount}</font> <img src='https://raw.githubusercontent.com/vulikit/varkit-resources/refs/heads/main/rulet1.gif'>", isSubMenu: false);

            menu.AddOption($"<font color='red'>{L_Red}</font> <font color='#d9d632'>(x{Config.Red["multiplier"]})</font>", (p, i) =>
            {
                BetOnColor(Color.Red, p, betamount);
            });

            menu.AddOption($"<font color='blue'>{L_Blue}</font> <font color='#d9d632'>(x{Config.Blue["multiplier"]})</font>", (p, i) =>
            {
                BetOnColor(Color.Blue, p, betamount);
            });

            menu.AddOption($"<font color='green'>{L_Green}</font> <font color='#d9d632'>(x{Config.Green["multiplier"]})</font>", (p, i) =>
            {
                BetOnColor(Color.Green, p, betamount);
            });

            manager.OpenMainMenu(player, menu);
        }

        public void BetOnColor(Color color, CCSPlayerController player, int amount)
        {
            var manager = GetMenuManager();
            if (manager == null)
                return;
            var L_Red = Localizer["Red"];
            var L_Blue = Localizer["Blue"];
            var L_Green = Localizer["Green"];
            if (StoreApi == null) return;

            GivePlayerCredit(player, -amount); //removes credit

            ActiveBets.Add(new Bet(player, color, amount));
            string colorName = color == Color.Red ? "{red}" + $"{L_Red}" :
                              color == Color.Blue ? "{blue}" + $"{L_Blue}" :
                              "{green}" + $"{L_Green}";

            if (Config.AccounceEveryone)
            {
                Server.PrintToChatAll(prefix + Localizer["Roulette_Announce", player.PlayerName, colorName, amount]);
            }

            reply(player, Localizer["Roulette_BetOnColor", colorName, amount]);
            manager.CloseMenu(player);
        }

        public int GetPlayerCredit(CCSPlayerController player)
        {
            if (StoreApi == null) return 0;
            return StoreApi.GetClientCredits(player);
        }

        public void GivePlayerCredit(CCSPlayerController player, int amount)
        {
            if (StoreApi == null) return;
            StoreApi.AddClientCredits(player, amount);
        }

        public void reply(CCSPlayerController player, string m)
        {
            player.PrintToChat(prefix + m);
        }

        [GameEventHandler]
        public HookResult RoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (ActiveBets.Count == 0) return HookResult.Continue;

            var L_Red = Localizer["Red"];
            var L_Blue = Localizer["Blue"];
            var L_Green = Localizer["Green"];
            Color winningColor = RoundEndWinner();
            string colorName = winningColor == Color.Red ? "{red}" + $"{L_Red}" :
                              winningColor == Color.Blue ? "{blue}" + $"{L_Blue}" :
                              "{green}" + $"{L_Green}";

            Server.PrintToChatAll(prefix + Localizer["Roulette_Winner", colorName]);
            GiveCreditsToWinners(winningColor);
            ActiveBets.Clear();
            return HookResult.Continue;
        }

        private Color RoundEndWinner()
        {
            int totalChance = Config.Red["chance"] + Config.Blue["chance"] + Config.Green["chance"];
            int roll = random.Next(1, totalChance + 1);

            if (roll <= Config.Red["chance"]) return Color.Red;
            if (roll <= Config.Red["chance"] + Config.Blue["chance"]) return Color.Blue;
            return Color.Green;
        }

        private void GiveCreditsToWinners(Color color)
        {
            if (StoreApi == null) return;

            int multiplier = color == Color.Red ? Config.Red["multiplier"] :
                            color == Color.Blue ? Config.Blue["multiplier"] :
                            Config.Green["multiplier"];

            foreach (var bet in ActiveBets.Where(b => b.color == color))
            {
                if (!bet.player.IsValid) continue;

                int winnings = bet.betamount * multiplier;
                GivePlayerCredit(bet.player, winnings);
                reply(bet.player, Localizer["Roulette_YouWon", winnings]);
            }
        }
    }
}