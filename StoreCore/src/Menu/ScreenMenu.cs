using static StoreCore.StoreCore;
using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI;
using CounterStrikeSharp.API.Core.Translations;
using static StoreCore.Lib;
using StoreAPI;

namespace StoreCore;

public static class ScreenMenu
{
    public static void Display(CCSPlayerController player)
    {
        if (player == null)
            return;
        int credits = STORE_API.GetClientCredits(player);

        Menu mainMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "store<mainmenu>", credits)
        };

        mainMenu.AddItem(Instance.Localizer.ForPlayer(player, "buy<option>"), (p, o) =>
        {
            DisplayCategories(p, mainMenu);
        });
        mainMenu.AddItem(Instance.Localizer.ForPlayer(player, "inventory<option>"), (p, o) =>
        {
            DisplayInventory(p, mainMenu);
        });
        mainMenu.Display();
    }
    private static void DisplayCategories(CCSPlayerController player, Menu prevMenu)
    {
        var categories = Item.GetCategories();

        Menu categoryMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "categories<title>"),
            IsSubMenu = true,
            PrevMenu = prevMenu,
        };

        if (categories.Count == 0)
        {
            categoryMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                categoryMenu.AddItem(category, (p, option) =>
                {
                    DisplayCategoryItems(p, category, categoryMenu);
                });
            }
        }
        categoryMenu.Display();
    }
    private static void DisplayCategoryItems(CCSPlayerController player, string category, Menu prevMenu)
    {
        if (player == null)
            return;

        int playerCredits = STORE_API.GetClientCredits(player);
        var items = Item.GetCategoryItems(category);

        Menu itemsMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "items<title>", category),
            IsSubMenu = true,
            ShowDisabledOptionNum = true,
            PrevMenu = prevMenu,
        };

        if (items.Count == 0)
        {
            itemsMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.items"), (p, o) => { }, true);
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

                itemsMenu.AddItem(itemDisplay, (p, o) =>
                {
                    DisplayConfirmMenu(p, item.UniqueId, category, itemsMenu);
                }, !canBuy);
            }
        }

        itemsMenu.Display();
    }
    private static void DisplayConfirmMenu(CCSPlayerController player, string uniqueId, string category, Menu prevMenu)
    {
        if (player == null)
            return;

        var item = Item.GetCategoryItems(category).FirstOrDefault(i => i.UniqueId == uniqueId);
        if (item == null)
            return;

        int playerCredits = STORE_API.GetClientCredits(player);

        Menu confirmMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "confirm.purchase<title>", item.Name),
            IsSubMenu = true,
            ShowDisabledOptionNum = false,
            PrevMenu = prevMenu,
        };

        if (item.Duration > 0 && item.IsEquipable)
        {
            string durationText = FormatDuration(item.Duration);
            confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.duration", durationText), (p, o) => { }, true);
        }
        else if (item.IsEquipable && item.Duration <= 0)
        {
            confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.permanent"), (p, o) => { }, true);
        }

        if (!string.IsNullOrEmpty(item.Description))
        {
            confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.description", item.Description), (p, o) => { }, true);
        }
        confirmMenu.AddItem("", (p, o) => { }, true);
        confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "confirm.yes", item.Price), (p, o) =>
        {
            bool purchased = Item.PurchaseItem(p, uniqueId);
            if (purchased)
            {
                string purchaseMessage = Instance.Localizer["item.bought", item.Name, item.Price];
                p.PrintToChat(Instance.Localizer["prefix"] + purchaseMessage);
            }
            else if (playerCredits < item.Price)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.not.enough", item.Name, item.Price]);
            }
            confirmMenu.Close(p);
        });

        confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "confirm.no"), (p, o) =>
        {
            confirmMenu.Close(p);
            DisplayCategoryItems(player, category, prevMenu);
        });
        confirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.preview"), (p, o) =>
        {
            STORE_API.InvokeOnItemPreview(player, uniqueId);
        });

        confirmMenu.Display();
    }
    public static void DisplayInventory(CCSPlayerController player)
    {
        var categories = Item.GetCategories();

        Menu inventoryCategoryMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "inventory<title>"),
        };

        if (categories.Count == 0)
        {
            inventoryCategoryMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                var playerItems = Item.GetPlayerItems(player.SteamID, category);
                if (playerItems.Count > 0)
                {
                    inventoryCategoryMenu.AddItem(category, (p, option) =>
                    {
                        DisplayInventoryItems(p, category, inventoryCategoryMenu);
                    });
                }
            }

            if (inventoryCategoryMenu.Options.Count == 0)
            {
                inventoryCategoryMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
            }
        }
        inventoryCategoryMenu.Display();
    }
    private static void DisplayInventory(CCSPlayerController player, Menu prevMenu)
    {
        var categories = Item.GetCategories();

        Menu inventoryCategoryMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "inventory<title>"),
            IsSubMenu = true,
            PrevMenu = prevMenu,
        };

        if (categories.Count == 0)
        {
            inventoryCategoryMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.categories"), (p, o) => { }, true);
        }
        else
        {
            foreach (var category in categories)
            {
                var playerItems = Item.GetPlayerItems(player.SteamID, category);
                if (playerItems.Count > 0)
                {
                    inventoryCategoryMenu.AddItem(category, (p, option) =>
                    {
                        DisplayInventoryItems(p, category, inventoryCategoryMenu);
                    });
                }
            }

            if (inventoryCategoryMenu.Options.Count == 0)
            {
                inventoryCategoryMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
            }
        }
        inventoryCategoryMenu.Display();
    }

    private static void DisplayInventoryItems(CCSPlayerController player, string category, Menu prevMenu)
    {
        if (player == null)
            return;

        var playerItems = Item.GetPlayerItems(player.SteamID, category).Where(i => i.IsEquipable).ToList();

        Menu inventoryItemsMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "inventory.items<title>", category),
            IsSubMenu = true,
            PrevMenu = prevMenu,
        };

        if (playerItems.Count == 0)
        {
            inventoryItemsMenu.AddItem(Instance.Localizer.ForPlayer(player, "no.owned.items"), (p, o) => { }, true);
        }
        else
        {
            foreach (var item in playerItems)
            {
                inventoryItemsMenu.AddItem($"{item.Name}", (p, o) =>
                {
                    DisplayItemManageMenu(p, item, inventoryItemsMenu);
                });
            }
        }

        inventoryItemsMenu.Display();
    }

    private static void DisplayItemManageMenu(CCSPlayerController player, Store.Store_Item item, Menu prevMenu)
    {
        if (player == null)
            return;

        bool equippedT = Item.IsItemEquipped(player.SteamID, item.UniqueId, 2);
        bool equippedCT = Item.IsItemEquipped(player.SteamID, item.UniqueId, 3);

        Menu manageMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "manage.item<title>", item.Name),
            IsSubMenu = true,
            PrevMenu = prevMenu,
        };

        if (item.DateOfExpiration.HasValue)
        {
            TimeSpan timeLeft = item.DateOfExpiration.Value - DateTime.UtcNow;
            if (timeLeft.TotalSeconds > 0)
            {
                string expiresIn = FormatTimeSpan(timeLeft);
                manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.expires.in", expiresIn), (p, o) => { }, true);
            }
            else
            {
                manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.expired"), (p, o) => { }, true);
            }
        }
        else
        {
            manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.permanent"), (p, o) => { }, true);
        }

        if (!string.IsNullOrEmpty(item.Description))
        {
            manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.description", item.Description), (p, o) => { }, true);
        }
        if (item.IsSellable)
        {
            int refundAmount = (int)(item.Price * 0.7);
            manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.sell<option>", refundAmount), (p, o) =>
            {
                DisplaySellConfirmMenu(p, item, manageMenu);
            });
        }
        if (item.IsEquipable)
        {
            manageMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.equip<option>"), (p, o) =>
            {
                Menu equipMenu = new Menu(player, Instance)
                {
                    Title = Instance.Localizer.ForPlayer(player, "equipmenu<title>"),
                    IsSubMenu = true,
                    ParentMenu = manageMenu
                };
                string checkboxT = equippedT ? "✓" : "✘";
                equipMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.equip.t", checkboxT), (p, o) =>
                {
                    if (equippedT)
                    {
                        Item.UnequipItem(p, item.UniqueId, 2);
                        equippedT = false;
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.unequipped.t", item.Name]);
                    }
                    else
                    {
                        Item.EquipItem(p, item.UniqueId, 2);
                        equippedT = true;
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.equipped.t", item.Name]);
                    }

                    checkboxT = equippedT ? "✓" : "✘";
                    o.Text = Instance.Localizer.ForPlayer(player, "item.equip.t", checkboxT);
                    equipMenu.Refresh();
                });

                string checkboxCT = equippedCT ? "✓" : "✘";
                equipMenu.AddItem(Instance.Localizer.ForPlayer(player, "item.equip.ct", checkboxCT), (p, o) =>
                {
                    if (equippedCT)
                    {
                        Item.UnequipItem(p, item.UniqueId, 3);
                        equippedCT = false;
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.unequipped.ct", item.Name]);
                    }
                    else
                    {
                        Item.EquipItem(p, item.UniqueId, 3);
                        equippedCT = true;
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.equipped.ct", item.Name]);
                    }

                    checkboxCT = equippedCT ? "✓" : "✘";
                    o.Text = Instance.Localizer.ForPlayer(player, "item.equip.ct", checkboxCT);
                    equipMenu.Refresh();
                });
                equipMenu.Display();
            });
        }

        manageMenu.Display();
    }
    private static void DisplaySellConfirmMenu(CCSPlayerController player, Store.Store_Item item, Menu prevMenu)
    {
        int refundAmount = (int)(item.Price * 0.7);

        Menu sellConfirmMenu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "confirm.sell<title>", item.Name),
            IsSubMenu = true,
            PrevMenu = prevMenu,
        };

        sellConfirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "confirm.yes.sell"), (p, o) =>
        {
            bool sold = Item.SellItem(p, item.UniqueId);
            if (sold)
            {
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["item.sold", item.Name, refundAmount]);
                sellConfirmMenu.Close(p);
                DisplayInventory(p, prevMenu);
            }
        });

        sellConfirmMenu.AddItem(Instance.Localizer.ForPlayer(player, "confirm.no.sell"), (p, o) =>
        {
            sellConfirmMenu.Close(p);
            DisplayItemManageMenu(player, item, prevMenu);
        });

        sellConfirmMenu.Display();
    }
}
