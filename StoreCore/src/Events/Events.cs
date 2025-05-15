using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using static CounterStrikeSharp.API.Core.Listeners;
using static StoreCore.StoreCore;
using static StoreCore.Lib;

namespace StoreCore;

public static class Events
{
    public static void Initialize()
    {
        Instance.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        Instance.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        Instance.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        Instance.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        Instance.RegisterListener<OnMapEnd>(OnMapEnd);
    }
    public static HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        if (attacker == null || victim == null || attacker == victim || attacker.IsBot || !attacker.IsValid)
            return HookResult.Continue;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null)
            return HookResult.Continue;

        if (Instance.Config.MainConfig.IgnoreWarmup && gameRules.WarmupPeriod)
            return HookResult.Continue;

        int baseCredits = Instance.Config.MainConfig.CreditsPerKill;
        if (baseCredits > 0)
        {
            bool multiplierApplied = false;

            foreach (var kvp in Instance.Config.Multiplier.CreditsPerKill)
            {
                string flag = kvp.Key;
                int multiplierValue = kvp.Value;

                if (AdminManager.PlayerHasPermissions(attacker, flag))
                {
                    STORE_API.AddClientCredits(attacker, baseCredits * multiplierValue);
                    if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                    {
                        attacker.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["kill.reward", baseCredits * multiplierValue]);
                    }
                    multiplierApplied = true;
                    AddToCreditsCount(attacker, baseCredits * multiplierValue);
                    break;
                }
            }

            if (!multiplierApplied)
            {
                STORE_API.AddClientCredits(attacker, baseCredits);
                if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                {
                    attacker.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["kill.reward", baseCredits]);
                }
                AddToCreditsCount(attacker, baseCredits);
            }
        }
        return HookResult.Continue;
    }
    public static HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules != null && Instance.Config.MainConfig.IgnoreWarmup && gameRules.WarmupPeriod)
            return HookResult.Continue;

        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV))
        {
            if (Instance.Config.MainConfig.CreditsPerRoundWin > 0)
            {
                if (p.TeamNum == @event.Winner)
                {
                    int baseCredits = Instance.Config.MainConfig.CreditsPerRoundWin;
                    bool multiplierApplied = false;

                    foreach (var kvp in Instance.Config.Multiplier.CreditsPerRoundWin)
                    {
                        string flag = kvp.Key;
                        int multiplierValue = kvp.Value;

                        if (AdminManager.PlayerHasPermissions(p, flag))
                        {
                            STORE_API.AddClientCredits(p, baseCredits * multiplierValue);
                            if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                            {
                                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["round.won", baseCredits * multiplierValue]);
                            }
                            multiplierApplied = true;
                            AddToCreditsCount(p, baseCredits * multiplierValue);
                            break;
                        }
                    }

                    if (!multiplierApplied)
                    {
                        STORE_API.AddClientCredits(p, baseCredits);
                        if (!Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
                        {
                            p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["round.won", baseCredits]);
                        }
                        AddToCreditsCount(p, baseCredits);
                    }
                }
            }
        }
        if (Instance.Config.MainConfig.ShowCreditsOnRoundEnd)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                int roundCredits = GetCreditsCount(p);
                p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["credits.round", roundCredits]);
                ResetCreditsCount(p);
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
                await StorePlayer.LoadPlayerDataAsync(playerName, steamId);
            });
        }
        return HookResult.Continue;
    }

    public static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            StorePlayer.SavePlayerData(player);
        }
        return HookResult.Continue;
    }
    public static void OnMapEnd()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p.IsValid))
        {
            StorePlayer.SavePlayerData(player);
        }
    }
}
