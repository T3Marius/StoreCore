using static CounterStrikeSharp.API.Modules.Commands.Targeting.Target;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using static StoreCore.StoreCore;

namespace StoreCore;

public static class Lib
{
    public static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
            return Instance.Localizer["duration.permanent"];

        TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

        if (timeSpan.TotalDays >= 30)
        {
            int months = (int)(timeSpan.TotalDays / 30);
            return Instance.Localizer["duration.months", months];
        }
        if (timeSpan.TotalDays >= 1)
        {
            int days = (int)timeSpan.TotalDays;
            return Instance.Localizer["duration.days", days];
        }
        if (timeSpan.TotalHours >= 1)
        {
            int hours = (int)timeSpan.TotalHours;
            return Instance.Localizer["duration.hours", hours];
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            int minutes = (int)timeSpan.TotalMinutes;
            return Instance.Localizer["duration.minutes", minutes];
        }

        return Instance.Localizer["duration.seconds", seconds];
    }
    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 30)
        {
            int months = (int)(timeSpan.TotalDays / 30);
            return Instance.Localizer["duration.months", months];
        }
        if (timeSpan.TotalDays >= 1)
        {
            int days = (int)timeSpan.TotalDays;
            return Instance.Localizer["duration.days", days];
        }
        if (timeSpan.TotalHours >= 1)
        {
            int hours = (int)timeSpan.TotalHours;
            return Instance.Localizer["duration.hours", hours];
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            int minutes = (int)timeSpan.TotalMinutes;
            return Instance.Localizer["duration.minutes", minutes];
        }

        return Instance.Localizer["duration.seconds", (int)timeSpan.TotalSeconds];
    }
    public static bool ProcessTargetString(
        CCSPlayerController? player,
        CommandInfo info,
        string targetstr,
        bool singletarget,
        bool immunitycheck,
        out List<CCSPlayerController> players,
        out string adminname,
        out string targetname)
    {
        players = [];
        adminname = string.Empty;
        targetname = string.Empty;

        TargetResult targetResult = new Target(targetstr).GetTarget(player);

        if (targetResult.Players.Count == 0)
        {
            info.ReplyToCommand("No matching client");
            return false;
        }
        else if (targetResult.Players.Count > 1)
        {
            if (singletarget || !TargetTypeMap.ContainsKey(targetstr))
            {
                info.ReplyToCommand("More than one client matched");
                return false;
            }
        }

        if (immunitycheck)
        {
            targetResult.Players.RemoveAll(target => !AdminManager.CanPlayerTarget(player, target));

            if (targetResult.Players.Count == 0)
            {
                info.ReplyToCommand("You cannot target");
                return false;
            }
        }

        if (targetResult.Players.Count == 1)
        {
            targetname = targetResult.Players[0].PlayerName;
        }
        else
        {
            TargetTypeMap.TryGetValue(targetstr, out TargetType type);

            targetname = type switch
            {
                TargetType.GroupAll => Instance.Localizer["all"],
                TargetType.GroupBots => Instance.Localizer["bots"],
                TargetType.GroupHumans => Instance.Localizer["humans"],
                TargetType.GroupAlive => Instance.Localizer["alive"],
                TargetType.GroupDead => Instance.Localizer["dead"],
                TargetType.GroupNotMe => Instance.Localizer["notme"],
                TargetType.PlayerMe => targetResult.Players.First().PlayerName,
                TargetType.TeamCt => Instance.Localizer["ct"],
                TargetType.TeamT => Instance.Localizer["t"],
                TargetType.TeamSpec => Instance.Localizer["spec"],
                _ => targetResult.Players.First().PlayerName
            };
        }

        adminname = player?.PlayerName ?? Instance.Localizer["Console"];
        players = targetResult.Players;
        return true;
    }
}
