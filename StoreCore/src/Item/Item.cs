using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using StoreAPI;
using static StoreCore.StoreCore;
using CounterStrikeSharp.API.Modules.Timers;


namespace StoreCore;

public static class Item
{
    private static readonly Dictionary<ulong, List<Store.Store_Item>> _playerItems = new();
    private static readonly Dictionary<ulong, List<Store.Store_Equipment>> _playerEquipment = new();
    private static readonly Dictionary<string, Store.Store_Item> _availableItems = new();
    private static readonly Dictionary<string, List<string>> _categories = new();
    private static bool _itemsLoaded = false;
    private static readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

    public static void Initialize()
    {
        Instance.AddTimer(2.0f, async () =>
        {
            if (!Database.IsInitialized)
                return;


            await LoadItemsFromDatabase();

            int itemCount = GetTotalItemCount();
            var categories = GetCategories();
            int categoryCount = categories.Count;

            Instance.Logger.LogInformation("Successfully loaded with {itemCount} Items and {categoryCount} Categories!", itemCount, categoryCount);
        });

        Instance.AddTimer(10, () =>
        {
            foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p.IsValid))
            {
                CheckExpiredItems(player);
            }
        }, TimerFlags.REPEAT);
    }
    private static int GetTotalItemCount()
    {
        int count = 0;
        var categories = GetCategories();

        foreach (var category in categories)
        {
            count += GetCategoryItems(category).Count;
        }

        return count;
    }

    public static async Task LoadItemsFromDatabase()
    {
        await _loadLock.WaitAsync();

        try
        {
            if (!Database.IsInitialized)
            {
                return;
            }

            Instance.Logger.LogInformation("Loading items from database...");
            var items = await Database.GetAllItemsAsync();

            _availableItems.Clear();
            _categories.Clear();

            foreach (var item in items)
            {
                _availableItems[item.UniqueId] = item;

                if (!_categories.ContainsKey(item.Category))
                    _categories[item.Category] = new List<string>();

                if (!_categories[item.Category].Contains(item.UniqueId))
                    _categories[item.Category].Add(item.UniqueId);
            }

            _itemsLoaded = true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to load items from database: {ex.Message}");
        }
        finally
        {
            _loadLock.Release();
        }
    }


    public static async Task LoadPlayerItems(ulong steamId)
    {
        if (!Database.IsInitialized)
        {
            return;
        }

        if (!_itemsLoaded)
        {
            await LoadItemsFromDatabase();
        }

        try
        {
            var items = await Database.GetPlayerItemsAsync(steamId);
            _playerItems[steamId] = items;

            var equipment = await Database.GetPlayerEquipmentAsync(steamId);
            _playerEquipment[steamId] = equipment;

        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to load player items: {ex.Message}");
        }
    }
    public static bool RegisterItem(string uniqueId, string name, string category, string type, int price, string description = "", string flags = "", bool isSellable = true, bool isBuyable = true, bool isEquipable = true, int duration = 0)
    {
        if (!Database.IsInitialized)
        {
            return false;
        }
        var item = new Store.Store_Item
        {
            UniqueId = uniqueId,
            Name = name,
            Category = category,
            Type = type,
            Price = price,
            Description = description,
            IsSellable = isSellable,
            IsBuyable = isBuyable,
            IsEquipable = isEquipable,
            Duration = duration,
            Flags = flags
        };

        _availableItems[uniqueId] = item;

        if (!_categories.ContainsKey(category))
            _categories[category] = new List<string>();

        if (!_categories[category].Contains(uniqueId))
            _categories[category].Add(uniqueId);

        Task.Run(async () =>
        {
            await Database.RegisterItemAsync(item);
        });

        return true;
    }


    public static bool PurchaseItem(CCSPlayerController player, string uniqueId)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return false;

        ulong steamId = player.SteamID;

        if (!_availableItems.ContainsKey(uniqueId))
        {
            return false;
        }

        var item = _availableItems[uniqueId];

        if (!item.IsBuyable)
        {
            return false;
        }

        if (item.IsEquipable && _playerItems.ContainsKey(steamId) && _playerItems[steamId].Any(i => i.UniqueId == uniqueId))
        {
            return false;
        }

        int credits = STORE_API.GetClientCredits(player);
        if (credits < item.Price)
        {
            return false;
        }

        STORE_API.RemoveClientCredits(player, item.Price);

        if (!_playerItems.ContainsKey(steamId))
            _playerItems[steamId] = new List<Store.Store_Item>();

        DateTime? expirationDate = null;
        if (item.Duration > 0)
        {
            expirationDate = DateTime.UtcNow.AddSeconds(item.Duration);
        }

        var playerItem = new Store.Store_Item
        {
            SteamID = steamId,
            UniqueId = item.UniqueId,
            Name = item.Name,
            Category = item.Category,
            Type = item.Type,
            Price = item.Price,
            Description = item.Description,
            IsSellable = item.IsSellable,
            IsBuyable = item.IsBuyable,
            IsEquipable = item.IsEquipable,
            Duration = item.Duration,
            DateOfPurchase = DateTime.UtcNow,
            DateOfExpiration = expirationDate
        };

        _playerItems[steamId].Add(playerItem);

        Task.Run(async () =>
        {
            await Database.PlayerPurchaseItemAsync(steamId, uniqueId, expirationDate);
        });

        var itemData = new Dictionary<string, string>
        {
            { "uniqueid", item.UniqueId },
            { "name", item.Name },
            { "category", item.Category },
            { "type", item.Type },
            { "price", item.Price.ToString() },
            { "description", item.Description },
            { "is_equipable", item.IsEquipable.ToString() },
            { "duration", item.Duration.ToString() },
            { "expiration_date", expirationDate?.ToString() ?? "never" }
        };

        STORE_API.InvokeOnPlayerPurchaseItem(player, itemData);

        if (item.IsEquipable)
        {
            EquipItem(player, uniqueId, 2);
            EquipItem(player, uniqueId, 3);
        }

        return true;
    }

    public static bool EquipItem(CCSPlayerController player, string uniqueId, int team)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return false;

        ulong steamId = player.SteamID;

        if (!_playerItems.ContainsKey(steamId) || !_playerItems[steamId].Any(i => i.UniqueId == uniqueId))
        {
            return false;
        }

        var item = _playerItems[steamId].First(i => i.UniqueId == uniqueId);
        if (!item.IsEquipable)
        {
            return false;
        }

        if (_playerEquipment.ContainsKey(steamId) &&
            _playerEquipment[steamId].Any(e => e.UniqueId == uniqueId && e.Team == team))
        {
            return true;
        }

        if (!_playerEquipment.ContainsKey(steamId))
            _playerEquipment[steamId] = new List<Store.Store_Equipment>();

        var equipment = new Store.Store_Equipment
        {
            SteamID = steamId,
            UniqueId = uniqueId,
            Team = team
        };

        _playerEquipment[steamId].Add(equipment);

        Task.Run(async () =>
        {
            await Database.EquipItemAsync(steamId, uniqueId, team);
        });

        var itemData = new Dictionary<string, string>
            {
                { "uniqueid", item.UniqueId },
                { "name", item.Name },
                { "category", item.Category },
                { "type", item.Type },
                { "team", team.ToString() }
            };

        STORE_API.InvokeOnPlayerEquipItem(player, itemData);
        return true;
    }
    public static bool UnequipItem(CCSPlayerController player, string uniqueId, int? team = null)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return false;

        ulong steamId = player.SteamID;

        if (!_playerEquipment.ContainsKey(steamId))
            return false;

        List<Store.Store_Equipment> toRemove;
        if (team.HasValue)
        {
            toRemove = _playerEquipment[steamId]
                .Where(e => e.UniqueId == uniqueId && e.Team == team.Value)
                .ToList();
        }
        else
        {
            toRemove = _playerEquipment[steamId]
                .Where(e => e.UniqueId == uniqueId)
                .ToList();
        }

        if (!toRemove.Any())
            return false;

        var item = _playerItems[steamId].FirstOrDefault(i => i.UniqueId == uniqueId);

        foreach (var eq in toRemove)
        {
            _playerEquipment[steamId].Remove(eq);

            int equipTeam = eq.Team;
            Task.Run(async () =>
            {
                await Database.UnequipItemAsync(steamId, uniqueId, equipTeam);
            });

            if (item != null)
            {
                var itemData = new Dictionary<string, string>
                {
                    { "uniqueid", item.UniqueId },
                    { "name", item.Name },
                    { "category", item.Category },
                    { "type", item.Type },
                    { "team", eq.Team.ToString() }
                };

                STORE_API.InvokeOnPlayerUnequipItem(player, itemData);
            }
        }

        return true;
    }
    public static bool SellItem(CCSPlayerController player, string uniqueId)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return false;

        ulong steamId = player.SteamID;

        if (!_playerItems.ContainsKey(steamId) || !_playerItems[steamId].Any(i => i.UniqueId == uniqueId))
        {
            return false;
        }

        var item = _playerItems[steamId].First(i => i.UniqueId == uniqueId);

        if (!item.IsSellable)
        {
            return false;
        }

        int refund = (int)(item.Price * 0.7);

        STORE_API.AddClientCredits(player, refund);

        UnequipItem(player, uniqueId);

        _playerItems[steamId].Remove(item);

        Task.Run(async () =>
        {
            await Database.PlayerSellItemAsync(steamId, uniqueId);
        });

        var itemData = new Dictionary<string, string>
        {
            { "uniqueid", item.UniqueId },
            { "name", item.Name },
            { "category", item.Category },
            { "type", item.Type },
            { "refund", refund.ToString() }
        };

        STORE_API.InvokeOnPlayerSellItem(player, itemData);

        return true;
    }
    public static bool PlayerHasItem(ulong steamId, string uniqueId)
    {
        if (_playerItems.ContainsKey(steamId) && _playerItems[steamId].Any(i => i.UniqueId == uniqueId))
            return true;

        Task.Run(async () =>
        {
            bool hasItem = await Database.PlayerHasItemAsync(steamId, uniqueId);
            if (hasItem && !_playerItems.ContainsKey(steamId))
            {
                await LoadPlayerItems(steamId);
            }
        });

        return false;
    }

    public static bool IsItemEquipped(ulong steamId, string uniqueId, int? team = null)
    {
        if (!_playerEquipment.ContainsKey(steamId))
            return false;

        if (team.HasValue)
        {
            return _playerEquipment[steamId].Any(e => e.UniqueId == uniqueId && e.Team == team.Value);
        }
        else
        {
            return _playerEquipment[steamId].Any(e => e.UniqueId == uniqueId);
        }
    }

    public static List<Store.Store_Item> GetPlayerItems(ulong steamId, string? category = null)
    {
        if (!_playerItems.ContainsKey(steamId))
            return new List<Store.Store_Item>();

        if (string.IsNullOrEmpty(category))
            return _playerItems[steamId];

        return _playerItems[steamId].Where(i => i.Category == category).ToList();
    }
    public static void CheckExpiredItems(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        ulong steamId = player.SteamID;

        if (!_playerItems.ContainsKey(steamId))
            return;

        DateTime now = DateTime.UtcNow;
        var expiredItems = _playerItems[steamId]
            .Where(i => i.DateOfExpiration.HasValue && i.DateOfExpiration.Value <= now)
            .ToList();

        foreach (var item in expiredItems)
        {
            UnequipItem(player, item.UniqueId);

            _playerItems[steamId].Remove(item);

            Task.Run(async () =>
            {
                await Database.PlayerSellItemAsync(steamId, item.UniqueId);
            });

            var itemData = new Dictionary<string, string>
            {
                { "uniqueid", item.UniqueId },
                { "name", item.Name },
                { "category", item.Category },
                { "type", item.Type }
            };

            STORE_API.InvokeOnPlayerItemExpired(player, itemData);
        }
    }
    public static List<string> GetCategories()
    {
        return _categories.Keys.ToList();
    }

    public static List<Store.Store_Item> GetCategoryItems(string category)
    {
        if (!_categories.ContainsKey(category))
            return new List<Store.Store_Item>();

        return _categories[category]
            .Select(id => _availableItems[id])
            .ToList();
    }
}