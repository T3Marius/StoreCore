using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using static StoreCore.StoreCore;

namespace StoreCore;

public static class Events
{
    public static void Initialize()
    {
        Instance.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        Instance.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        Instance.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        Instance.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
    }
    public static HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        if (attacker == null || victim == null)
            return HookResult.Continue;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()!.GameRules;
        if (gameRules == null)
            return HookResult.Continue;

        if (Instance.Config.MainConfig.IgnoreWarmup)
        {
            if (gameRules.WarmupPeriod)
                return HookResult.Continue;

            if (attacker != victim)
            {
                int credits = Instance.Config.MainConfig.CreditsPerKill;
                if (credits > 0)
                {
                    STORE_API.AddClientCredits(attacker, credits);
                    attacker.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["kill.reward", credits]);
                }
            }
        }
        else
        {
            if (attacker != victim)
            {
                int credits = Instance.Config.MainConfig.CreditsPerKill;
                if (credits > 0)
                {
                    STORE_API.AddClientCredits(attacker, credits);
                    attacker.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["kill.reward", credits]);
                }
            }
        }

        return HookResult.Continue;
    }
    public static HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV))
        {
            if (Instance.Config.MainConfig.CreditsPerRoundWin > 0)
            {
                if (p.TeamNum == @event.Winner)
                {
                    STORE_API.AddClientCredits(p, Instance.Config.MainConfig.CreditsPerRoundWin);
                    p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["round.won", Instance.Config.MainConfig.CreditsPerRoundWin]);
                }
            }
        }

        return HookResult.Continue;
    }
    public static HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            string playerName = player.PlayerName;
            ulong steamId = player.SteamID;


            Task.Run(async () =>
            {
                await Item.LoadPlayerItems(steamId);
                await Database.CreatePlayerAsync(steamId, playerName);
                await Database.GetPlayerItemsAsync(steamId, playerName);
            });
        }
        return HookResult.Continue;
    }
    public static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            string playerName = player.PlayerName;
            ulong steamId = player.SteamID;

            int credits = 0;
            if (Instance.PlayerCredits.TryGetValue(steamId, out int cachedCredits))
            {
                credits = cachedCredits;
            }

            Task.Run(async () =>
            {
                await Database.SavePlayerAsync(steamId, playerName, credits);
            });

            Instance.PlayerCredits.Remove(steamId);
        }

        return HookResult.Continue;
    }
}
