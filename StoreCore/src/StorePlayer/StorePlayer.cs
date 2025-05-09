using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using static StoreCore.StoreCore;

namespace StoreCore;

public static class StorePlayer
{
    private static bool _isCreditsAwardRunning = false;

    public static void Load()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV))
        {
            ulong steamId = player.SteamID;
            string playerName = player.PlayerName;

            Task.Run(async () =>
            {
                try
                {
                    Instance.Logger.LogDebug($"Loading player data for {playerName} (SteamID: {steamId})");
                    var playerData = await Database.LoadPlayerAsync(steamId);

                    if (playerData != null)
                    {
                        Instance.PlayerCredits[steamId] = playerData.Credits;
                        Instance.Logger.LogDebug($"Loaded {playerData.Credits} credits for {playerName}");
                    }
                    else
                    {
                        await Database.CreatePlayerAsync(steamId, playerName);
                        Instance.PlayerCredits[steamId] = Instance.Config.MainConfig.StartCredits;
                        Instance.Logger.LogDebug($"Created new player with {Instance.Config.MainConfig.StartCredits} credits for {playerName}");
                    }

                    await Item.LoadPlayerItems(steamId);
                }
                catch (Exception ex)
                {
                    Instance.Logger.LogError($"Error loading player data: {ex.Message}");
                }
            });
        }
    }

    public static void StartCreditsAward()
    {
        if (_isCreditsAwardRunning)
            return;

        _isCreditsAwardRunning = true;

        Instance.AddTimer(Instance.Config.MainConfig.PlaytimeInterval, () =>
        {
            foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p.IsValid))
            {
                STORE_API.AddClientCredits(player, Instance.Config.MainConfig.CreditsPerInterval);
            }
        }, TimerFlags.REPEAT);
    }

    public static void LoadPlayerData(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return;

        string playerName = player.PlayerName;
        ulong steamId = player.SteamID;

        Task.Run(async () =>
        {
            try
            {
                Instance.Logger.LogDebug($"Loading player data for {playerName} (SteamID: {steamId})");

                var playerData = await Database.LoadPlayerAsync(steamId);

                if (playerData != null)
                {
                    Instance.PlayerCredits[steamId] = playerData.Credits;

                    await Database.UpdatePlayerLastJoinAsync(steamId, playerName);
                }
                else
                {
                    await Database.CreatePlayerAsync(steamId, playerName);
                    Instance.PlayerCredits[steamId] = Instance.Config.MainConfig.StartCredits;
                    Instance.Logger.LogDebug($"Created new player with {Instance.Config.MainConfig.StartCredits} credits for {playerName}");
                }

                await Item.LoadPlayerItems(steamId);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Error loading player data: {ex.Message}");
            }
        });
    }

    public static void SavePlayerData(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return;

        string playerName = player.PlayerName;
        ulong steamId = player.SteamID;

        if (Instance.PlayerCredits.TryGetValue(steamId, out int credits))
        {

            Task.Run(async () =>
            {
                try
                {
                    await Database.SetCreditsAsync(steamId, credits);
                    Instance.Logger.LogDebug($"Saved {credits} credits for {playerName}");
                }
                catch (Exception ex)
                {
                    Instance.Logger.LogError($"Error saving player data: {ex.Message}");
                }
            });
            Instance.PlayerCredits.Remove(steamId);
        }
    }
}