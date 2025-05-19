using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static StoreCore.StoreCore;
using static StoreCore.Lib;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API;
using CS2ScreenMenuAPI;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Extensions;

namespace StoreCore;

public static class Commands
{
    public static void Initialize()
    {
        var AddCmd = Instance.AddCommand;
        var Commands = Instance.Config.Commands;

        foreach (var cmd in Commands.AddCredits)
        {
            AddCmd($"css_{cmd}", "Add Player Credits", Command_AddCredits);
        }
        foreach (var cmd in Commands.RemoveCredits)
        {
            AddCmd($"css_{cmd}", "Remove Player Credits", Command_RemoveCredits);
        }
        foreach (var cmd in Commands.SetCredits)
        {
            AddCmd($"css_{cmd}", "Set Player Credits", Command_SetCredits);
        }
        foreach (var cmd in Commands.ShowCredits)
        {
            AddCmd($"css_{cmd}", "Show player his credits", Command_ShowCredits);
        }
        foreach (var cmd in Commands.GiftCredits)
        {
            AddCmd($"css_{cmd}", "Gift someone credits", Command_GiftCredits);
        }
        foreach (var cmd in Commands.ResetCredits)
        {
            AddCmd($"css_{cmd}", "Reset the all players credits", Command_ResetCredits);
        }
        foreach (var cmd in Commands.OpenStore)
        {
            AddCmd($"css_{cmd}", "Opens the store menu", Command_Store);
        }
    }
    public static void Command_Store(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (Instance.Config.Permissions.StoreCommand.Count > 0 &&
             !Instance.Config.Permissions.StoreCommand.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["no.permission"]);
            return;
        }

        Instance.Config.Reload();

        switch (Instance.Config.MainConfig.MenuType)
        {
            case "screen":
                ScreenMenu.Display(player);
                break;
            case "t3":
                T3Menu.Display(player);
                break;
        }

    }
    [RequiresPermissions("@css/root")]
    public static void Command_ResetCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (Instance.Config.Permissions.ResetCredits.Count > 0 &&
            !Instance.Config.Permissions.ResetCredits.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["no.permission"]);
            return;
        }

        switch (Instance.Config.MainConfig.MenuType)
        {
            case "t3":
            case "t3menu":
                var manager = Instance.GetMenuManager();
                if (manager == null)
                    return;

                var t3menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "resetcredits<confirm>"));
                t3menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm<yes>"), (p, option) =>
                {
                    List<CCSPlayerController> players = Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV).ToList();
                    foreach (var player in players)
                    {
                        STORE_API.SetClientCredits(player, 0);
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.reset", players.Count]);
                        manager.CloseMenu(player);
                    }
                });
                t3menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm<no>"), (p, option) =>
                {
                    manager.CloseMenu(player);
                });
                manager.OpenMainMenu(player, t3menu);
                break;
            case "screen":
            case "worldtext":
                Menu menu = new Menu(player, Instance)
                {
                    Title = Instance.Localizer.ForPlayer(player, "resetcredits<confirm>"),
                    HasExitButon = false,
                };

                menu.AddItem(Instance.Localizer.ForPlayer(player, "confirm<yes>"), (p, option) =>
                {
                    List<CCSPlayerController> players = Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV).ToList();
                    foreach (var player in players)
                    {
                        STORE_API.SetClientCredits(player, 0);
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.reset", players.Count]);
                        menu.Close(p);
                    }
                });
                menu.AddItem(Instance.Localizer.ForPlayer(player, "confirm<no>"), (p, option) =>
                {
                    menu.Close(p);
                });
                menu.Display();
                break;
        }
    }
    public static void Command_ShowCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        int credits = STORE_API.GetClientCredits(player);
        info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["credits.shown", credits]);
    }
    [CommandHelper(minArgs: 2, usage: "<playername> <credits>")]
    public static void Command_GiftCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.IsBot || player.IsHLTV)
            return;

        string targetString = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), out int credits))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }
        if (credits <= 0)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }
        int gifterCredits = STORE_API.GetClientCredits(player);
        if (gifterCredits < credits)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["not.enough.credits"]);
            return;
        }

        if (!ProcessTargetString(player, info, targetString, false, false, out List<CCSPlayerController> targetPlayers, out string senderName, out string targetName))
            return;

        var targetPlayer = targetPlayers[0];

        if (targetPlayer == player)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["cant.gift.yourself"]);
            return;
        }

        STORE_API.RemoveClientCredits(player, credits);

        STORE_API.AddClientCredits(targetPlayer, credits);

        info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["credits.gifted", credits, targetName]);
        targetPlayer.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.received", credits]);
    }
    [CommandHelper(minArgs: 2, usage: "<playername> | @all <credits>")]
    public static void Command_AddCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (Instance.Config.Permissions.ResetCredits.Count > 0 &&
            !Instance.Config.Permissions.ResetCredits.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["no.permission"]);
            return;
        }

        string targetString = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), out int credits))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }

        if (credits <= 0)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }

        if (!ProcessTargetString(player, info, targetString, false, true, out List<CCSPlayerController> targetPlayers, out string adminName, out string targetName))
            return;

        foreach (var targetPlayer in targetPlayers)
        {
            STORE_API.AddClientCredits(targetPlayer, credits);
        }

        if (targetPlayers.Count == 1)
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.added", adminName, credits, targetName]);
        }
        else
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.added.multiple", adminName, credits, targetPlayers.Count]);
        }
    }
    [CommandHelper(minArgs: 2, usage: "<playername> | @all <credits>")]
    public static void Command_RemoveCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (Instance.Config.Permissions.ResetCredits.Count > 0 &&
            !Instance.Config.Permissions.ResetCredits.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["no.permission"]);
            return;
        }

        string targetString = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), out int credits))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
        }

        if (credits <= 0)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }

        if (!ProcessTargetString(player, info, targetString, false, true, out List<CCSPlayerController> targetPlayers, out string adminName, out string targetName))
            return;

        foreach (var targetPlayer in targetPlayers)
        {
            STORE_API.RemoveClientCredits(targetPlayer, credits);
        }

        if (targetPlayers.Count == 1)
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.removed", adminName, credits, targetName]);
        }
        else
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.removed.multiple", adminName, credits, targetPlayers.Count]);
        }
    }
    [CommandHelper(minArgs: 2, usage: "<playername> | @all <credits>")]
    public static void Command_SetCredits(CCSPlayerController? player, CommandInfo info)
    {
        if (Instance.Config.Permissions.ResetCredits.Count > 0 &&
            !Instance.Config.Permissions.ResetCredits.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["no.permission"]);
            return;
        }

        string targetString = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), out int credits))
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
        }

        if (credits <= 0)
        {
            info.ReplyToCommand(Instance.Localizer["prefix"] + Instance.Localizer["invalid.credits"]);
            return;
        }

        if (!ProcessTargetString(player, info, targetString, false, true, out List<CCSPlayerController> targetPlayers, out string adminName, out string targetName))
            return;

        foreach (var targetPlayer in targetPlayers)
        {
            STORE_API.SetClientCredits(targetPlayer, credits);
        }

        if (targetPlayers.Count == 1)
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.set", adminName, credits, targetName]);
        }
        else
        {
            Server.PrintToChatAll(Instance.Localizer["prefix"] + Instance.Localizer["credits.set.multiple", adminName, credits, targetPlayers.Count]);
        }
    }
}