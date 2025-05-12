using CounterStrikeSharp.API.Core;
using StoreAPI;
using static StoreAPI.Store;

namespace StoreCore;

public class StoreAPI : IStoreAPI
{
    private StoreModuleConfig? _configProvider;
    private string? _configPath;

    public StoreAPI()
    {
    }

    public StoreAPI(string configPath)
    {
        _configPath = configPath;
        _configProvider = new StoreModuleConfig(configPath);
    }

    public void SetConfigPath(string configPath)
    {
        _configPath = configPath;
        _configProvider = new StoreModuleConfig(configPath);
    }

    public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerPurchaseItem;
    public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerEquipItem;
    public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerUnequipItem;
    public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerSellItem;
    public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerItemExpired;
    public event Action<CCSPlayerController, string>? OnItemPreview;
    public void InvokeOnItemPreview(CCSPlayerController player, string uniqueId)
    {
        OnItemPreview?.Invoke(player, uniqueId);
    }
    public void InvokeOnPlayerPurchaseItem(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        OnPlayerPurchaseItem?.Invoke(player, itemData);
    }
    public void InvokeOnPlayerEquipItem(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        OnPlayerEquipItem?.Invoke(player, itemData);
    }
    public void InvokeOnPlayerUnequipItem(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        OnPlayerUnequipItem?.Invoke(player, itemData);
    }
    public void InvokeOnPlayerSellItem(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        OnPlayerSellItem?.Invoke(player, itemData);
    }
    public void InvokeOnPlayerItemExpired(CCSPlayerController player, Dictionary<string, string> itemData)
    {
        OnPlayerItemExpired?.Invoke(player, itemData);
    }
    public bool RegisterItem(string uniqueId, string name, string category, string type, int price, string description = "", string flags = "", bool isSellable = true, bool isBuyable = true, bool isEquipable = true, int duration = 0)
    {
        return Item.RegisterItem(uniqueId, name, category, type, price, description, flags, isSellable, isBuyable, isEquipable, duration);
    }
    public bool PurchaseItem(CCSPlayerController player, string uniqueId)
    {
        return Item.PurchaseItem(player, uniqueId);
    }
    public bool SellItem(CCSPlayerController player, string uniqueId)
    {
        return Item.SellItem(player, uniqueId);
    }
    public bool EquipItem(CCSPlayerController player, string uniqueId, int team)
    {
        return Item.EquipItem(player, uniqueId, team);
    }
    public bool UnequipItem(CCSPlayerController player, string uniqueId, int? team = null)
    {
        return Item.UnequipItem(player, uniqueId, team);
    }
    public bool PlayerHasItem(ulong steamId, string uniqueId)
    {
        return Item.PlayerHasItem(steamId, uniqueId);
    }
    public bool IsItemEquipped(ulong steamId, string uniqueId, int? team = null)
    {
        return Item.IsItemEquipped(steamId, uniqueId, team);
    }
    public List<Store_Item> GetPlayerItems(ulong steamId, string? category = null)
    {
        return Item.GetPlayerItems(steamId, category);
    }
    public void AddClientCredits(CCSPlayerController player, int credits)
    {
        if (player != null)
        {
            Credits.Add(player, credits);
        }
    }
    public void RemoveClientCredits(CCSPlayerController player, int credits)
    {
        if (player != null)
        {
            Credits.Remove(player, credits);
        }
    }
    public void SetClientCredits(CCSPlayerController player, int credits)
    {
        if (player != null)
        {
            Credits.Set(player, credits);
        }
    }
    public int GetClientCredits(CCSPlayerController player)
    {
        if (player != null)
        {
            return Credits.Get(player);
        }
        return 0;
    }
    public string GetDatabaseString()
    {
        return Database.GlobalDatabaseConnectionString;
    }
    public T GetModuleConfig<T>(string moduleName) where T : class, new()
    {
        return _configProvider!.LoadConfig<T>(moduleName);
    }

    public void SaveModuleConfig<T>(string moduleName, T config) where T : class, new()
    {
        _configProvider?.SaveConfig(moduleName, config);
    }
}