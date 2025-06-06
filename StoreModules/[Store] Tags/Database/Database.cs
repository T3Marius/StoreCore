using CounterStrikeSharp.API;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static StoreCore.Tags;

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
                CREATE TABLE IF NOT EXISTS store_custom_tags (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    SteamID BIGINT UNSIGNED NOT NULL,
                    Tag VARCHAR(255) NOT NULL,
                    TagColor VARCHAR(50) NOT NULL,
                    NameColor VARCHAR(50) NOT NULL,
                    ChatColor VARCHAR(50) NOT NULL,
                    ScoreboardTag VARCHAR(255) NULL,
                    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    DateUpdated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY unique_player_tag (SteamID)
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

    public static async Task SaveCustomTagAsync(ulong steamId, Tag_Item tag)
    {
        if (!IsInitialized)
        {
            Instance.Logger.LogWarning("Attempted to save custom tag before database initialization");
            return;
        }

        const string query = @"
        INSERT INTO store_custom_tags (SteamID, Tag, TagColor, NameColor, ChatColor, ScoreboardTag)
        VALUES (@SteamID, @Tag, @TagColor, @NameColor, @ChatColor, @ScoreboardTag)
        ON DUPLICATE KEY UPDATE
            Tag = VALUES(Tag),
            TagColor = VALUES(TagColor),
            NameColor = VALUES(NameColor),
            ChatColor = VALUES(ChatColor),
            ScoreboardTag = VALUES(ScoreboardTag),
            DateUpdated = CURRENT_TIMESTAMP";

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            await connection.ExecuteAsync(query, new
            {
                SteamID = steamId,
                Tag = tag.Tag,
                TagColor = tag.TagColor,
                NameColor = tag.NameColor,
                ChatColor = tag.ChatColor,
                ScoreboardTag = tag.ScoreboardTag ?? tag.Tag
            });
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to save custom tag for player {steamId}: {ex.Message}");
            throw;
        }
    }

    public static async Task<Tag_Item?> GetCustomTagAsync(ulong steamId)
    {
        if (!IsInitialized)
        {
            Instance.Logger.LogWarning("Attempted to get custom tag before database initialization");
            return null;
        }

        const string query = @"
        SELECT Tag, TagColor, NameColor, ChatColor, ScoreboardTag
        FROM store_custom_tags 
        WHERE SteamID = @SteamID";

        try
        {
            using MySqlConnection connection = await ConnectAsync();
            var result = await connection.QueryFirstOrDefaultAsync(query, new { SteamID = steamId });

            if (result == null)
                return null;

            return new Tag_Item
            {
                Id = "custom_tag",
                Name = "Custom Tag",
                Tag = result.Tag,
                TagColor = result.TagColor,
                NameColor = result.NameColor,
                ChatColor = result.ChatColor,
                ScoreboardTag = result.ScoreboardTag
            };
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Failed to get custom tag for player {steamId}: {ex.Message}");
            return null;
        }
    }

    public static void SaveCustomTag(ulong steamId, Tag_Item tag)
    {
        Task.Run(async () =>
        {
            try
            {
                await SaveCustomTagAsync(steamId, tag);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Failed to save custom tag for player {steamId}: {ex.Message}");
            }
        });
    }

    public static void SetCustomTag(ulong steamId, string customTagId)
    {
        Task.Run(async () =>
        {
            try
            {
                var customTag = await GetCustomTagAsync(steamId);
                if (customTag != null)
                {
                    customTag.Id = customTagId;
                    var baseTag = Instance.ModuleConfig.Tags.Values.FirstOrDefault(t => t.Id == customTagId);
                    if (baseTag != null)
                    {
                        customTag.Name = baseTag.Name;
                        customTag.Price = baseTag.Price;
                        customTag.Duration = baseTag.Duration;
                        customTag.Flags = baseTag.Flags;
                        customTag.Description = baseTag.Description;
                    }

                    Server.NextFrame(() =>
                    {
                        try
                        {
                            var equippedList = new List<Tag_Item> { customTag };
                            Instance._playerEquippedTags[steamId] = equippedList;

                            var player = Utilities.GetPlayers().FirstOrDefault(p => p?.SteamID == steamId);
                            if (player != null && player.IsValid)
                            {
                                Lib.UpdatePlayerTags(player);
                            }
                        }
                        catch (Exception ex)
                        {
                            Instance.Logger.LogError($"Failed to update player UI for custom tag {steamId}: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Failed to set custom tag for player {steamId}: {ex.Message}");
            }
        });
    }

    public static void RemoveCustomTag(ulong steamId)
    {
        if (!IsInitialized)
        {
            Instance.Logger.LogWarning("Attempted to remove custom tag before database initialization");
            return;
        }

        const string query = "DELETE FROM store_custom_tags WHERE SteamID = @SteamID";

        Task.Run(async () =>
        {
            try
            {
                using MySqlConnection connection = await ConnectAsync();
                var rowsAffected = await connection.ExecuteAsync(query, new { SteamID = steamId });

                if (rowsAffected > 0)
                {
                    Server.NextFrame(() =>
                    {
                        try
                        {
                            Instance._playerActiveTags.Remove(steamId);

                            var player = Utilities.GetPlayers().FirstOrDefault(p => p?.SteamID == steamId);
                            if (player != null && player.IsValid)
                            {
                                RemovePlayerScoreboardTag(player);
                            }
                        }
                        catch (Exception ex)
                        {
                            Instance.Logger.LogError($"Failed to update player UI for removing custom tag {steamId}: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Failed to remove custom tag for player {steamId}: {ex.Message}");
            }
        });
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