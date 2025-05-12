using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static StoreCore.StoreCore;
using StoreAPI;
using Store_Player = StoreAPI.Store.Store_Player;

namespace StoreCore;

public static class Database
{
    public static string GlobalDatabaseConnectionString { get; set; } = string.Empty;
    public static bool IsInitialized { get; private set; } = false;
    private static readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);

    public static async Task<MySqlConnection> ConnectAsync()
    {
        if (string.IsNullOrEmpty(GlobalDatabaseConnectionString))
        {
            throw new InvalidOperationException("Database connection string not initialized. Call Initialize() first.");
        }

        try
        {
            MySqlConnection connection = new(GlobalDatabaseConnectionString);
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to connect to database: {ex.Message}");
            throw;
        }
    }

    public static void ExecuteAsync(string query, object? parameters)
    {
        if (!IsInitialized)
        {
            Instance.Logger.LogWarning("Attempted to execute query before database initialization");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                using MySqlConnection connection = await ConnectAsync();
                await connection.ExecuteAsync(query, parameters);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Failed to execute query: {ex.Message}");
            }
        });
    }
    public static async Task InitializeAsync(Database_Config config)
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (IsInitialized)
            {
                return;
            }

            MySqlConnectionStringBuilder builder = new()
            {
                Server = config.Host,
                Database = config.Name,
                UserID = config.User,
                Password = config.Pass,
                Port = config.Port,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 640,
                ConnectionIdleTimeout = 30,
                AllowZeroDateTime = true,
                ConnectionTimeout = 15,
                DefaultCommandTimeout = 30
            };

            GlobalDatabaseConnectionString = builder.ConnectionString;

            await CreateTablesAsync();
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to initialize database: {ex.Message}");
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
    private static async Task CreateTablesAsync()
    {
        using MySqlConnection connection = await ConnectAsync();
        using MySqlTransaction transaction = await connection.BeginTransactionAsync();

        try
        {
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS store_players (
                    id INT NOT NULL AUTO_INCREMENT,
                    SteamID BIGINT UNSIGNED NOT NULL,
                    PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
                    Credits INT NOT NULL,
                    DateOfJoin DATETIME NOT NULL,
                    DateOfLastJoin DATETIME NOT NULL,
                    Vip BOOLEAN NOT NULL,
                    PRIMARY KEY (id),
                    UNIQUE KEY id (id),
                    UNIQUE KEY SteamID (SteamID)
                );", transaction: transaction);

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS store_items (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    UniqueId VARCHAR(255) NOT NULL,
                    Name VARCHAR(255) NOT NULL,
                    Category VARCHAR(255) NOT NULL,
                    Type VARCHAR(255) NOT NULL,
                    Price INT NOT NULL,
                    IsSellable BOOLEAN NOT NULL,
                    IsBuyable BOOLEAN NOT NULL,
                    IsEquipable BOOLEAN NOT NULL,
                    Duration INT NOT NULL DEFAULT 0,
                    Description TEXT,
                    Flags TEXT,
                    UNIQUE KEY (UniqueId)
                );", transaction: transaction);

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS store_player_items (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    SteamID BIGINT UNSIGNED NOT NULL,
                    UniqueId VARCHAR(255) NOT NULL,
                    DateOfPurchase DATETIME NOT NULL,
                    DateOfExpiration DATETIME NULL,
                    UNIQUE KEY (SteamID, UniqueId)
                );", transaction: transaction);

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS store_equipment (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    SteamID BIGINT UNSIGNED NOT NULL,
                    UniqueId VARCHAR(255) NOT NULL,
                    Team INT NOT NULL,
                    UNIQUE KEY (SteamID, UniqueId, Team)
                );", transaction: transaction);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Instance.Logger.LogError($"Error creating database tables: {ex.Message}");
            throw;
        }
    }
    public static async Task<Store_Player?> LoadPlayerAsync(ulong steamId)
    {
        if (!IsInitialized)
        {
            return null;
        }

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            return await connection.QueryFirstOrDefaultAsync<Store_Player>(
                "SELECT * FROM store_players WHERE SteamID = @SteamID",
                new { SteamID = steamId });
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to load player (SteamID: {steamId}): {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> AddCreditsAsync(ulong steamId, int amount)
    {
        if (!IsInitialized || amount <= 0) return false;

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            int rowsAffected = await connection.ExecuteAsync(
                "UPDATE store_players SET Credits = Credits + @Amount WHERE SteamID = @SteamID",
                new { SteamID = steamId, Amount = amount });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to add credits: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> RemoveCreditsAsync(ulong steamId, int amount)
    {
        if (!IsInitialized || amount <= 0) return false;

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            int rowsAffected = await connection.ExecuteAsync(
                "UPDATE store_players SET Credits = GREATEST(0, Credits - @Amount) WHERE SteamID = @SteamID",
                new { SteamID = steamId, Amount = amount });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to remove credits: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetCreditsAsync(ulong steamId, int amount)
    {
        if (!IsInitialized || amount < 0) return false;

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            int rowsAffected = await connection.ExecuteAsync(
                "UPDATE store_players SET Credits = @Amount WHERE SteamID = @SteamID",
                new { SteamID = steamId, Amount = amount });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to set credits: {ex.Message}");
            return false;
        }
    }
    public static async Task<bool> UpdatePlayerLastJoinAsync(ulong steamId, string playerName)
    {
        if (!IsInitialized) return false;

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            int rowsAffected = await connection.ExecuteAsync(
                "UPDATE store_players SET PlayerName = @PlayerName, DateOfLastJoin = @Now WHERE SteamID = @SteamID",
                new { SteamID = steamId, PlayerName = playerName, Now = DateTime.UtcNow });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to update player's last join: {ex.Message}");
            return false;
        }
    }
    public static async Task<bool> CreatePlayerAsync(ulong steamId, string playerName)
    {
        if (!IsInitialized) return false;

        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var existingPlayer = await connection.QueryFirstOrDefaultAsync<Store_Player>(
                "SELECT * FROM store_players WHERE SteamID = @SteamID",
                new { SteamID = steamId });

            if (existingPlayer != null)
            {
                await connection.ExecuteAsync(
                    "UPDATE store_players SET PlayerName = @PlayerName, DateOfLastJoin = @Now WHERE SteamID = @SteamID",
                    new
                    {
                        SteamID = steamId,
                        PlayerName = playerName,
                        Now = DateTime.UtcNow
                    });
                return true;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO store_players (SteamID, PlayerName, Credits, DateOfJoin, DateOfLastJoin, Vip) 
                VALUES (@SteamID, @PlayerName, @Credits, @Now, @Now, @Vip)",
                new
                {
                    SteamID = steamId,
                    PlayerName = playerName,
                    Credits = Instance.Config.MainConfig.StartCredits,
                    Now = DateTime.UtcNow,
                    Vip = false
                });
            return true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to create player: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SavePlayerAsync(ulong steamId, string playerName, int credits, bool vip = false)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var existingPlayer = await connection.QueryFirstOrDefaultAsync<Store_Player>(
                "SELECT * FROM store_players WHERE SteamID = @SteamID",
                new { SteamID = steamId });

            if (existingPlayer != null)
            {
                await connection.ExecuteAsync(@"
                UPDATE store_players 
                SET PlayerName = @PlayerName, 
                    Credits = @Credits, 
                    DateOfLastJoin = @Now, 
                    Vip = @Vip 
                WHERE SteamID = @SteamID",
                    new
                    {
                        SteamID = steamId,
                        PlayerName = playerName,
                        Credits = credits,
                        Now = DateTime.UtcNow,
                        Vip = vip
                    });
                return true;
            }
            else
            {
                await connection.ExecuteAsync(@"
                INSERT INTO store_players (SteamID, PlayerName, Credits, DateOfJoin, DateOfLastJoin, Vip) 
                VALUES (@SteamID, @PlayerName, @Credits, @Now, @Now, @Vip)",
                    new
                    {
                        SteamID = steamId,
                        PlayerName = playerName,
                        Credits = credits,
                        Now = DateTime.UtcNow,
                        Vip = vip
                    });

                return true;
            }
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to save player data: {ex.Message}");
            return false;
        }
    }
    public static async Task<bool> RegisterItemAsync(Store.Store_Item item)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var existingItem = await connection.QueryFirstOrDefaultAsync<Store.Store_Item>(
                "SELECT * FROM store_items WHERE UniqueId = @UniqueId",
                new { item.UniqueId });

            if (existingItem != null)
            {
                await connection.ExecuteAsync(@"
                UPDATE store_items 
                SET Name = @Name, 
                    Category = @Category, 
                    Type = @Type, 
                    Price = @Price, 
                    IsSellable = @IsSellable, 
                    IsBuyable = @IsBuyable,
                    IsEquipable = @IsEquipable,
                    Duration = @Duration,
                    Description = @Description,
                    Flags = @Flags
                WHERE UniqueId = @UniqueId",
                            item);
            }
            else
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO store_items (UniqueId, Name, Category, Type, Price, IsSellable, IsBuyable, IsEquipable, Duration, Description, Flags)
                    VALUES (@UniqueId, @Name, @Category, @Type, @Price, @IsSellable, @IsBuyable, @IsEquipable, @Duration, @Description, @Flags)",
                    item);

            }
            return true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to register item: {ex.Message}");
            return false;
        }
    }

    public static async Task<List<Store.Store_Item>> GetAllItemsAsync()
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();
            var items = await connection.QueryAsync<Store.Store_Item>("SELECT * FROM store_items");
            return items.ToList();
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to get items: {ex.Message}");
            return new List<Store.Store_Item>();
        }
    }

    public static async Task<bool> PlayerPurchaseItemAsync(ulong steamId, string uniqueId, DateTime? expiration = null)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var existingItem = await connection.QueryFirstOrDefaultAsync(
                "SELECT * FROM store_player_items WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                new { SteamID = steamId, UniqueId = uniqueId });

            if (existingItem != null)
            {
                if (expiration.HasValue)
                {
                    await connection.ExecuteAsync(
                        "UPDATE store_player_items SET DateOfExpiration = @Expiration WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                        new { SteamID = steamId, UniqueId = uniqueId, Expiration = expiration });
                }
                return false;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO store_player_items (SteamID, UniqueId, DateOfPurchase, DateOfExpiration)
                VALUES (@SteamID, @UniqueId, @PurchaseDate, @ExpirationDate)",
                new
                {
                    SteamID = steamId,
                    UniqueId = uniqueId,
                    PurchaseDate = DateTime.UtcNow,
                    ExpirationDate = expiration
                });

            return true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to record item purchase: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> PlayerSellItemAsync(ulong steamId, string uniqueId)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            await connection.ExecuteAsync(
                "DELETE FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                new { SteamID = steamId, UniqueId = uniqueId });

            int rowsAffected = await connection.ExecuteAsync(
                "DELETE FROM store_player_items WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                new { SteamID = steamId, UniqueId = uniqueId });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to sell item: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> EquipItemAsync(ulong steamId, string uniqueId, int team)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var existingEquip = await connection.QueryFirstOrDefaultAsync(
                "SELECT * FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId AND Team = @Team",
                new { SteamID = steamId, UniqueId = uniqueId, Team = team });

            if (existingEquip != null)
                return true;

            await connection.ExecuteAsync(@"
                INSERT INTO store_equipment (SteamID, UniqueId, Team)
                VALUES (@SteamID, @UniqueId, @Team)",
                new { SteamID = steamId, UniqueId = uniqueId, Team = team });

            return true;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to equip item: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> UnequipItemAsync(ulong steamId, string uniqueId, int? team = null)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            int rowsAffected;
            if (team.HasValue)
            {
                rowsAffected = await connection.ExecuteAsync(
                    "DELETE FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId AND Team = @Team",
                    new { SteamID = steamId, UniqueId = uniqueId, Team = team.Value });
            }
            else
            {
                rowsAffected = await connection.ExecuteAsync(
                    "DELETE FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                    new { SteamID = steamId, UniqueId = uniqueId });
            }

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to unequip item: {ex.Message}");
            return false;
        }
    }
    public static async Task<List<Store.Store_Item>> GetPlayerItemsAsync(ulong steamId, string? category = null)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();
            var items = new List<Store.Store_Item>();

            string query;
            object parameters;

            if (string.IsNullOrEmpty(category))
            {
                query = @"
            SELECT 
                i.Id, 
                @SteamID AS SteamID, 
                i.UniqueId, 
                i.Name, 
                i.Category, 
                i.Type, 
                i.Price, 
                i.IsSellable, 
                i.IsBuyable, 
                i.IsEquipable, 
                i.Duration, 
                i.Description,
                pi.DateOfPurchase, 
                pi.DateOfExpiration
            FROM store_player_items pi
            JOIN store_items i ON pi.UniqueId = i.UniqueId
            WHERE pi.SteamID = @SteamID";
                parameters = new { SteamID = steamId };
            }
            else
            {
                query = @"
            SELECT 
                i.Id, 
                @SteamID AS SteamID, 
                i.UniqueId, 
                i.Name, 
                i.Category, 
                i.Type, 
                i.Price, 
                i.IsSellable, 
                i.IsBuyable, 
                i.IsEquipable, 
                i.Duration,
                i.Description, 
                pi.DateOfPurchase, 
                pi.DateOfExpiration
            FROM store_player_items pi
            JOIN store_items i ON pi.UniqueId = i.UniqueId
            WHERE pi.SteamID = @SteamID AND i.Category = @Category";
                parameters = new { SteamID = steamId, Category = category };
            }

            var result = await connection.QueryAsync(query, parameters);

            foreach (var row in result)
            {
                var item = new Store.Store_Item
                {
                    Id = row.Id,
                    SteamID = steamId,
                    UniqueId = row.UniqueId,
                    Name = row.Name,
                    Category = row.Category,
                    Type = row.Type,
                    Price = row.Price,
                    IsSellable = row.IsSellable,
                    IsBuyable = row.IsBuyable,
                    IsEquipable = row.IsEquipable,
                    Duration = row.Duration,
                    Description = row.Description
                };

                var purchaseDate = row.DateOfPurchase;
                if (purchaseDate != null)
                {
                    item.DateOfPurchase = Convert.ToDateTime(purchaseDate);
                }

                var expirationDate = row.DateOfExpiration;
                if (expirationDate != null)
                {
                    item.DateOfExpiration = Convert.ToDateTime(expirationDate);
                }

                items.Add(item);
            }

            return items;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to get player items: {ex.Message}");
            return new List<Store.Store_Item>();
        }
    }

    public static async Task<List<Store.Store_Equipment>> GetPlayerEquipmentAsync(ulong steamId)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var equipment = await connection.QueryAsync<Store.Store_Equipment>(
                "SELECT * FROM store_equipment WHERE SteamID = @SteamID",
                new { SteamID = steamId });

            return equipment.ToList();
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to get player equipment: {ex.Message}");
            return new List<Store.Store_Equipment>();
        }
    }

    public static async Task<List<string>> GetCategoriesAsync()
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var categories = await connection.QueryAsync<string>(
                "SELECT DISTINCT Category FROM store_items");

            return categories.ToList();
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to get categories: {ex.Message}");
            return new List<string>();
        }
    }

    public static async Task<bool> PlayerHasItemAsync(ulong steamId, string uniqueId)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            var item = await connection.QueryFirstOrDefaultAsync(
                "SELECT * FROM store_player_items WHERE SteamID = @SteamID AND UniqueId = @UniqueId",
                new { SteamID = steamId, UniqueId = uniqueId });

            return item != null;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to check if player has item: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> IsItemEquippedAsync(ulong steamId, string uniqueId, int? team = null)
    {
        try
        {
            using MySqlConnection connection = await ConnectAsync();

            string query;
            object parameters;

            if (team.HasValue)
            {
                query = "SELECT * FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId AND Team = @Team";
                parameters = new { SteamID = steamId, UniqueId = uniqueId, Team = team.Value };
            }
            else
            {
                query = "SELECT * FROM store_equipment WHERE SteamID = @SteamID AND UniqueId = @UniqueId";
                parameters = new { SteamID = steamId, UniqueId = uniqueId };
            }

            var equipment = await connection.QueryFirstOrDefaultAsync(query, parameters);
            return equipment != null;
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to check if item is equipped: {ex.Message}");
            return false;
        }
    }

    public static void Initialize()
    {
        var config = Instance.Config.Database;

        Task.Run(async () =>
        {
            try
            {
                await InitializeAsync(config);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Failed to initialize database: {ex.Message}");
            }
        });
    }
}