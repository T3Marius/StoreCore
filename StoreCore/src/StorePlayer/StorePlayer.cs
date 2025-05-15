using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using static StoreCore.StoreCore;
using static StoreCore.Lib;

namespace StoreCore;

public static class StorePlayer
{
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
                    var playerData = await Database.LoadPlayerAsync(steamId);

                    if (playerData != null)
                    {
                        Instance.PlayerCredits[steamId] = playerData.Credits;
                    }
                    else
                    {
                        await Database.CreatePlayerAsync(steamId, playerName);
                        Instance.PlayerCredits[steamId] = Instance.Config.MainConfig.StartCredits;
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
        if (Instance.Config.MainConfig.CreditsPerInterval > 0)
        {
            Instance.AddTimer(Instance.Config.MainConfig.PlaytimeInterval, () =>
            {
                foreach (var player in Utilities.GetPlayers().Where(p => p != null && !p.IsBot && !p.IsHLTV && p.IsValid && p.PawnIsAlive))
                {
                    int baseCredits = Instance.Config.MainConfig.CreditsPerInterval;
                    bool multiplierApplied = false;

                    foreach (var kvp in Instance.Config.Multiplier.CreditsPerInterval)
                    {
                        string flag = kvp.Key;
                        int multiplierValue = kvp.Value;

                        if (AdminManager.PlayerHasPermissions(player, flag))
                        {
                            STORE_API.AddClientCredits(player, baseCredits * multiplierValue);
                            if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                            {
                                player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["activty.reward", baseCredits * multiplierValue]);
                            }
                            multiplierApplied = true;
                            AddToCreditsCount(player, baseCredits * multiplierValue);
                            break;
                        }
                    }

                    if (!multiplierApplied)
                    {
                        STORE_API.AddClientCredits(player, baseCredits);
                        if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                        {
                            player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["activty.reward", baseCredits]);
                        }
                        AddToCreditsCount(player, baseCredits);
                    }
                }
            }, TimerFlags.REPEAT);
        }
    }

    public static async Task LoadPlayerDataAsync(string playerName, ulong steamId)
    {

        try
        {
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
            }
            await Item.LoadPlayerItems(steamId);
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError($"Error loading player data: {ex.Message}");
        }
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
                    await Database.SetCreditsAsync(steamId, credits); ;
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