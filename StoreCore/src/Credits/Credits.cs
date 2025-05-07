using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using static StoreCore.StoreCore;

namespace StoreCore;

public static class Credits
{
    public static void Add(CCSPlayerController player, int credits)
    {
        if (player == null || !player.IsValid || credits <= 0)
            return;

        ulong steamId = player.SteamID;

        Task.Run(async () =>
        {
            await Database.AddCreditsAsync(steamId, credits);

            Server.NextFrame(() =>
            {
                if (Instance.PlayerCredits.ContainsKey(steamId))
                {
                    Instance.PlayerCredits[steamId] += credits;
                }
                else
                {
                    Get(player);
                    if (Instance.PlayerCredits.ContainsKey(steamId))
                    {
                        Instance.PlayerCredits[steamId] += credits;
                    }
                }
            });
        });
    }

    public static void Remove(CCSPlayerController player, int credits)
    {
        if (player == null || !player.IsValid || credits <= 0)
            return;

        ulong steamId = player.SteamID;

        Task.Run(async () =>
        {
            await Database.RemoveCreditsAsync(steamId, credits);

            Server.NextFrame(() =>
            {
                if (Instance.PlayerCredits.ContainsKey(steamId))
                {
                    Instance.PlayerCredits[steamId] = Math.Max(0, Instance.PlayerCredits[steamId] - credits);
                }
                else
                {
                    Get(player);
                }
            });
        });
    }

    public static void Set(CCSPlayerController player, int credits)
    {
        if (player == null || !player.IsValid || credits < 0)
            return;

        ulong steamId = player.SteamID;

        Task.Run(async () =>
        {
            await Database.SetCreditsAsync(steamId, credits);

            Server.NextFrame(() =>
            {
                Instance.PlayerCredits[steamId] = credits;
            });
        });
    }

    public static int Get(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
            return 0;

        ulong steamId = player.SteamID;
        string playerName = player.PlayerName;

        if (Instance.PlayerCredits.TryGetValue(steamId, out int cachedCredits))
        {
            return cachedCredits;
        }

        var dbPlayer = Task.Run(async () => await Database.LoadPlayerAsync(steamId)).Result;
        if (dbPlayer != null)
        {
            Instance.PlayerCredits[steamId] = dbPlayer.Credits;
            return dbPlayer.Credits;
        }
        else
        {
            Task.Run(async () => await Database.CreatePlayerAsync(steamId, playerName)).Wait();
            Instance.PlayerCredits[steamId] = Instance.Config.MainConfig.StartCredits;
            return Instance.Config.MainConfig.StartCredits;
        }
    }
}