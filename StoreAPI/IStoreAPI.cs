using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace StoreAPI
{
    public interface IStoreAPI
    {
        public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerPurchaseItem;
        public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerEquipItem;
        public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerUnequipItem;
        public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerSellItem;
        public event Action<CCSPlayerController, Dictionary<string, string>>? OnPlayerItemExpired;
        public event Action<CCSPlayerController, string>? OnItemPreview;

        /// <summary>
        /// Gets the Capability to use StoreAPI.
        /// </summary>
        public static readonly PluginCapability<IStoreAPI> Capability = new("storecore:api");

        /// <summary>
        /// Acces the database string of store db table.
        /// </summary>
        public string GetDatabaseString();

        /// <summary>
        /// Add an amount of credits to a player.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="credits"></param>
        public void AddClientCredits(CCSPlayerController player, int credits);

        /// <summary>
        /// Remove an amount of credits from a player.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="credits"></param>
        public void RemoveClientCredits(CCSPlayerController player, int credits);

        /// <summary>
        /// Set an amount of credits to a player. Not that the credits you set that much he will have.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="credits"></param>
        public void SetClientCredits(CCSPlayerController player, int credits);

        /// <summary>
        /// Gets player current credits
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public int GetClientCredits(CCSPlayerController player);

        /// <summary>
        /// Register a new store item.
        /// </summary>
        public bool RegisterItem(string uniqueId, string name, string category, string type, int price, string description = "", bool isSellable = true, bool isBuyable = true, bool isEquipable = true, int duration = 0);

        /// <summary>
        /// Force Purchase item for player.
        /// </summary>
        public bool PurchaseItem(CCSPlayerController player, string uniqueId);

        /// <summary>
        /// Force Sell player an item.
        /// </summary>
        public bool SellItem(CCSPlayerController player, string uniqueId);

        /// <summary>
        /// Force player to equip an item
        /// </summary>
        public bool EquipItem(CCSPlayerController player, string uniqueId, int team);

        /// <summary>
        /// Force player to unequip an item
        /// </summary>
        public bool UnequipItem(CCSPlayerController player, string uniqueId, int? team = null);

        /// <summary>
        /// Check if player has item.
        /// </summary>
        public bool PlayerHasItem(ulong steamId, string uniqueId);

        /// <summary>
        /// Check if item is equipped.
        /// </summary>
        public bool IsItemEquipped(ulong steamId, string uniqueId, int? team = null);

        /// <summary>
        /// Get all player items.
        /// </summary>
        public List<StoreAPI.Store.Store_Item> GetPlayerItems(ulong steamId, string? category = null);

        /// <summary>
        /// Load a module's configuration (creates it if it doesn't exist)
        /// </summary>
        T GetModuleConfig<T>(string moduleName) where T : class, new();

        /// <summary>
        /// Save a module's configuration
        /// </summary>
        void SaveModuleConfig<T>(string moduleName, T config) where T : class, new();
    }
}
