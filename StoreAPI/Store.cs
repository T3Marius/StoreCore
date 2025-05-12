namespace StoreAPI
{
    public abstract class Store
    {
        public class Store_Item
        {
            public int Id { get; set; }
            public ulong SteamID { get; set; }
            public string UniqueId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public int Price { get; set; }
            public string Description { get; set; } = string.Empty;
            public string Flags { get; set; } = string.Empty;
            public bool IsSellable { get; set; }
            public bool IsBuyable { get; set; }
            public bool IsEquipable { get; set; }
            public int Duration { get; set; }
            public DateTime DateOfPurchase { get; set; }
            public DateTime? DateOfExpiration { get; set; }
        }

        public class Store_Equipment
        {
            public int Id { get; set; }
            public ulong SteamID { get; set; }
            public string UniqueId { get; set; } = string.Empty;
            public int Team { get; set; }
        }
        public class Store_Player
        {
            public int id { get; set; }
            public ulong SteamID { get; set; }
            public string? PlayerName { get; set; }
            public int Credits { get; set; }
            public DateTime DateOfJoin { get; set; }
            public DateTime DateOfLastJoin { get; set; }
            public bool Vip { get; set; }
        }
    }
}