using CounterStrikeSharp.API;
using static StoreCore.StoreCore;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core;

namespace StoreCore;

public static class StorePlayer
{
    public static void StartCreditsAward()
    {
        if (Instance.Config.MainConfig.CreditsPerInterval > 0)
        {
            Instance.AddTimer(Instance.Config.MainConfig.PlaytimeInterval, () =>
            {
                var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()!.GameRules;
                if (gameRules == null)
                    return;
                if (Instance.Config.MainConfig.IgnoreWarmup)
                {
                    if (gameRules.WarmupPeriod)
                        return;

                    foreach (var p in Utilities.GetPlayers())
                    {
                        STORE_API.AddClientCredits(p, Instance.Config.MainConfig.CreditsPerInterval);
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["activity.reward", Instance.Config.MainConfig.CreditsPerInterval]);
                    }
                }
                else
                {
                    foreach (var p in Utilities.GetPlayers())
                    {
                        STORE_API.AddClientCredits(p, Instance.Config.MainConfig.CreditsPerInterval);
                        p.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["activity.reward", Instance.Config.MainConfig.CreditsPerInterval]);
                    }
                }

            }, TimerFlags.REPEAT);
        }
    }
    public static void Load()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV))
        {
            var currentCredits = STORE_API.GetClientCredits(player);
            STORE_API.SetClientCredits(player, currentCredits);

            Instance.PlayerCredits[player.SteamID] = currentCredits;
        }
    }
}
