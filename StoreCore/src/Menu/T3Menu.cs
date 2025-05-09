using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using static StoreCore.StoreCore;
using static StoreCore.Lib;
using StoreAPI;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Admin;
namespace StoreCore;

public static class T3Menu
{
    public static void Display(CCSPlayerController player)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        int credits = STORE_API.GetClientCredits(player);
        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "store<mainmenu>", credits));

        menu.AddOption(Instance.Localizer.ForPlayer(player, "buy<option>"), (p, o) =>
        {
            DisplayCategories(p, menu);
        });
        menu.AddOption(Instance.Localizer.ForPlayer(player, "inventory<option>"), (p, o) =>
        {
            DisplayInventory(p, menu);
        });       
        menu.AddOption(Instance.Localizer.ForPlayer(player, "functions<option>"), (p, o) =>
        {
            DisplayFunctionsMenu(p, menu);
        });
        manager.OpenMainMenu(player, menu);
    }
    private static void DisplayFunctionsMenu(CCSPlayerController player, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager() ?? throw new Exception("T3Menu not found");
        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "functionsmenu<title>"), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "givecredits<option>"), (p, o) =>
            {
                DisplayGiveCreditsMenu(p, menu);
            });
        }
        menu.AddOption(Instance.Localizer.ForPlayer(player, "giftcredits<option>"), (p, o) =>
        {
            DisplayGiftCreditsMenu(p, menu);
        });
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayGiftCreditsMenu(CCSPlayerController player, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager() ?? throw new Exception("T3Menu not found");

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "giftcredits<title>"), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        foreach (var client in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p != player))
        {
            menu.AddOption(client.PlayerName, (p, o) =>
            {
                DisplayGiftCreditsToPlayerMenu(p, prevMenu, client.PlayerName, client);
            });
        }
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayGiveCreditsMenu(CCSPlayerController player, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager() ?? throw new Exception("T3Menu not found");

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "givecredits<title>"), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        foreach (var client in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p != player))
        {
            menu.AddOption(client.PlayerName, (p, o) =>
            {
                DisplayGiveCreditsToPlayerMenu(player, prevMenu, client.PlayerName, client);
            });
        }
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayGiftCreditsToPlayerMenu(CCSPlayerController player, IT3Menu prevMenu, string playerName, CCSPlayerController client)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager() ?? throw new Exception("T3Menu not found");
        IT3Menu menu = manager.CreateMenu(playerName, isSubMenu: true);
        menu.ParentMenu = prevMenu;

        menu.AddInputOption(Instance.Localizer.ForPlayer(player, "input.GiftCredits"), Instance.Localizer.ForPlayer(player, "input.GiftCredits.PlaceHolder"), (p, o, input) =>
        {
            int credits = int.Parse(input);
            int playerCredits = STORE_API.GetClientCredits(p);

            if (playerCredits < credits)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["not.enough.credits"]);
                return;
            }
            else
            {
                STORE_API.AddClientCredits(client, credits);
                STORE_API.RemoveClientCredits(player, credits);
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.gifted", client.PlayerName, credits]);
                client.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.recieved", p.PlayerName, credits]);
            }
        }, Instance.Localizer["prefix"] + Instance.Localizer["input.GiftCredits.message", playerName] + Instance.Localizer["prefix"] + Instance.Localizer["input.Cancel.message"]);
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayGiveCreditsToPlayerMenu(CCSPlayerController player, IT3Menu prevMenu, string playerName, CCSPlayerController client)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager() ?? throw new Exception("T3Menu not found");
        IT3Menu menu = manager.CreateMenu(playerName, isSubMenu: true);
        menu.ParentMenu = prevMenu;

        menu.AddInputOption(Instance.Localizer.ForPlayer(player, "input.GiveCredits"), Instance.Localizer.ForPlayer(player, "input.GiveCredits.PlaceHolder"), (p, o, input) =>
        {
            int credits = int.Parse(input);
            STORE_API.AddClientCredits(client, credits);
            player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.added", p.PlayerName, credits, client.PlayerName]);

        }, Instance.Localizer["prefix"] + Instance.Localizer["input.GiveCredits.message", playerName] + Instance.Localizer["prefix"] + Instance.Localizer["input.Cancel.message"]);
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayCategories(CCSPlayerController player, IT3Menu prevMenu)
    {
        var categories = Item.GetCategories();
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "categories<title>"), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        if (categories.Count == 0)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                menu.AddOption(category, (p, option) =>
                {
                    DisplayCategoryItems(p, category, menu);
                });
            }
        }
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayCategoryItems(CCSPlayerController player, string category, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        int playerCredits = STORE_API.GetClientCredits(player);
        var items = Item.GetCategoryItems(category);
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "items<title>", category), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        if (items.Count == 0)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.items"), (p, o) => { }, true);
        }
        else
        {
            foreach (var item in items)
            {
                bool hasEnoughCredits = playerCredits >= item.Price;

                bool canBuy = item.IsBuyable && hasEnoughCredits;

                if (item.IsEquipable)
                {
                    canBuy = canBuy && !Item.PlayerHasItem(player.SteamID, item.UniqueId);
                }

                string itemDisplay = $"{Instance.Localizer.ForPlayer(player, "item.display", item.Name, item.Price)}";

                if (Item.PlayerHasItem(player.SteamID, item.UniqueId) && item.IsEquipable)
                {
                    itemDisplay += " " + Instance.Localizer.ForPlayer(player, "item.owned");
                }

                if (!hasEnoughCredits)
                {
                    itemDisplay += " " + Instance.Localizer.ForPlayer(player, "item.cannot.afford");
                }

                menu.AddOption(itemDisplay, (p, o) =>
                {
                    DisplayConfirmMenu(p, item.UniqueId, category, menu);
                }, !canBuy);
            }
        }

        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayConfirmMenu(CCSPlayerController player, string uniqueId, string category, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var item = Item.GetCategoryItems(category).FirstOrDefault(i => i.UniqueId == uniqueId);
        if (item == null)
            return;
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        int playerCredits = STORE_API.GetClientCredits(player);

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "confirm.purchase<title>", item.Name), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        if (item.Duration > 0 && item.IsEquipable)
        {
            string durationText = FormatDuration(item.Duration);
            menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.duration", durationText));
        }
        else if (item.IsEquipable && item.Duration <= 0)
        {
            menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.permanent"));
        }
        if (!string.IsNullOrEmpty(item.Description))
        {
            menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.description", item.Description));
        }
        menu.AddOption(Instance.Localizer.ForPlayer(player, "item.preview"), (p, o) =>
        {
            STORE_API.InvokeOnItemPreview(player, uniqueId);
        });
        menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm.yes", item.Price), (p, o) =>
        {
            bool purchased = Item.PurchaseItem(p, uniqueId);
            if (purchased)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.bought", item.Name, item.Price]);
            }
            else if (playerCredits < item.Price)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.not.enough", item.Name, item.Price]);
            }
            manager.CloseMenu(p);
        });

        menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm.no"), (p, o) =>
        {
            manager.CloseMenu(p);
            DisplayCategoryItems(player, category, prevMenu);
        });

        manager.OpenSubMenu(player, menu);
    }
    public static void DisplayInventory(CCSPlayerController player)
    {
        var categories = Item.GetCategories();
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "inventory<title>"));


        if (categories.Count == 0)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                var playerItems = Item.GetPlayerItems(player.SteamID, category);
                if (playerItems.Count > 0)
                {
                    menu.AddOption(category, (p, option) =>
                    {
                        DisplayInventoryItems(p, category, menu);
                    });
                }
            }

            if (menu.Options.Count == 0)
            {
                menu.AddOption(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
            }
        }
        manager.OpenSubMenu(player, menu);
    }
    private static void DisplayInventory(CCSPlayerController player, IT3Menu prevMenu)
    {
        var categories = Item.GetCategories();
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "inventory<title>"), isSubMenu: true);
        menu.ParentMenu = prevMenu;


        if (categories.Count == 0)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                var playerItems = Item.GetPlayerItems(player.SteamID, category);
                if (playerItems.Count > 0)
                {
                    menu.AddOption(category, (p, option) =>
                    {
                        DisplayInventoryItems(p, category, menu);
                    });
                }
            }

            if (menu.Options.Count == 0)
            {
                menu.AddOption(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
            }
        }
        manager.OpenSubMenu(player, menu);
    }

    private static void DisplayInventoryItems(CCSPlayerController player, string category, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var playerItems = Item.GetPlayerItems(player.SteamID, category).Where(i => i.IsEquipable).ToList();
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "inventory.items<title>", category), isSubMenu: true);
        menu.ParentMenu = prevMenu;

        if (playerItems.Count == 0)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
        }
        else
        {
            foreach (var item in playerItems)
            {
                menu.AddOption($"{item.Name}", (p, o) =>
                {
                    DisplayItemManageMenu(p, item, menu);
                });
            }
        }

        manager.OpenSubMenu(player, menu);
    }

    private static void DisplayItemManageMenu(CCSPlayerController player, Store.Store_Item item, IT3Menu prevMenu)
    {
        if (player == null)
            return;

        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        bool equippedT = Item.IsItemEquipped(player.SteamID, item.UniqueId, 2);
        bool equippedCT = Item.IsItemEquipped(player.SteamID, item.UniqueId, 3);

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "manage.item<title>", item.Name), isSubMenu: true);
        menu.ParentMenu = prevMenu;
        if (item.DateOfExpiration.HasValue)
        {
            TimeSpan timeLeft = item.DateOfExpiration.Value - DateTime.UtcNow;
            if (timeLeft.TotalSeconds > 0)
            {
                string expiresIn = FormatTimeSpan(timeLeft);
                menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.expires.in", expiresIn));
            }
            else
            {
                menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.expired"));
            }
        }
        else
        {
            menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.permanent"));
        }

        if (!string.IsNullOrEmpty(item.Description))
        {
            menu.AddTextOption(Instance.Localizer.ForPlayer(player, "item.description", item.Description));
        }
        if (item.IsSellable)
        {
            int refundAmount = (int)(item.Price * 0.7);
            menu.AddOption(Instance.Localizer.ForPlayer(player, "item.sell<option>", refundAmount), (p, o) =>
            {
                DisplaySellConfirmMenu(p, item, menu);
            });
        }
        if (item.IsEquipable)
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "item.equip<option>"), (p, o) =>
            {
                IT3Menu equipMenu = manager.CreateMenu(Instance.Localizer.ForPlayer(p, "equipmenu<title>"), isSubMenu: true);
                equipMenu.ParentMenu = menu;

                equipMenu.AddBoolOption(Instance.Localizer.ForPlayer(player, "item.equip.t.t3menu"), defaultValue: equippedT, (p, o) =>
                {
                    if (o is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");

                        if (isEnabled)
                        {
                            Item.EquipItem(p, item.UniqueId, 2);
                            p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.equipped.t", item.Name]);
                        }
                        else
                        {
                            Item.UnequipItem(p, item.UniqueId, 2);
                            p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.unequipped.t", item.Name]);
                        }
                    }
                });

                equipMenu.AddBoolOption(Instance.Localizer.ForPlayer(player, "item.equip.ct.t3menu"), defaultValue: equippedCT, (p, o) =>
                {
                    if (o is IT3Option boolOption)
                    {
                        bool isEnabled = boolOption.OptionDisplay!.Contains("✔");

                        if (isEnabled)
                        {
                            Item.EquipItem(p, item.UniqueId, 3);
                            p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.equipped.ct", item.Name]);
                        }
                        else
                        {
                            Item.UnequipItem(p, item.UniqueId, 3);
                            p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.unequipped.ct", item.Name]);
                        }
                    }
                });
                manager.OpenSubMenu(player, equipMenu);
            });
        }

        manager.OpenSubMenu(player, menu);
    }
    private static void DisplaySellConfirmMenu(CCSPlayerController player, Store.Store_Item item, IT3Menu prevMenu)
    {
        int refundAmount = (int)(item.Price * 0.7);
        var manager = Instance.GetMenuManager();
        if (manager == null) return;

        IT3Menu menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "confirm.sell<title>", item.Name));
        menu.ParentMenu = prevMenu;

        menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm.yes.sell"), (p, o) =>
        {
            bool sold = Item.SellItem(p, item.UniqueId); 
            if (sold)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.sold", item.Name, refundAmount]);
                manager.CloseMenu(p);
                DisplayInventory(p, prevMenu);
            }
        });

        menu.AddOption(Instance.Localizer.ForPlayer(player, "confirm.no.sell"), (p, o) =>
        {
            manager.CloseMenu(p);
            DisplayItemManageMenu(player, item, prevMenu);
        });

        manager.OpenSubMenu(player, menu);
    }
}