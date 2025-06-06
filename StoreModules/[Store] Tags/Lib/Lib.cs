using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using static StoreCore.Tags;

namespace StoreCore;

public static class Lib
{
    public static bool CheckPermission(CCSPlayerController? player, string permissionKey)
    {
        if (player == null || !player.IsValid || string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        if (permissionKey.StartsWith('#'))
        {
            return AdminManager.PlayerInGroup(player, permissionKey);
        }

        if (permissionKey.StartsWith('@'))
        {
            return AdminManager.PlayerHasPermissions(player, permissionKey);
        }

        if (ulong.TryParse(permissionKey, out ulong keySteamId64))
        {
            return player.SteamID == keySteamId64;
        }

        try
        {
            var communityId = new SteamID(permissionKey).SteamId64;
            return player.SteamID == communityId;
        }
        catch
        {
            return false;
        }
    }
    public static void ApplyHighestPriorityTag(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
            return;

        foreach (var tag in Instance.ModuleConfig.Tags.Values)
        {
            if (Instance.StoreApi.IsItemEquipped(player.SteamID, tag.Id, player.TeamNum))
            {
                if (tag.Id.Contains("custom", StringComparison.OrdinalIgnoreCase))
                {
                    Database.SetCustomTag(player.SteamID, tag.Id);
                }
                else
                {
                    Instance._playerActiveTags[player.SteamID] = tag;
                    UpdatePlayerScoreboardTag(player, tag);
                }
                return;
            }
        }

        foreach (var staticTagEntry in Instance.Config.Tags.StaticTags)
        {
            string permissionKey = staticTagEntry.Key;
            StaticTagItem staticTagData = staticTagEntry.Value;

            if (CheckPermission(player, permissionKey))
            {
                var (tagColor, tagName) = ParseColoredTagString(staticTagData.Tag);

                var activeTag = new Tag_Item
                {
                    Id = $"static_{permissionKey}",
                    Name = "Static Tag",
                    Tag = tagName,
                    TagColor = tagColor!,
                    ChatColor = staticTagData.ChatColor,
                    NameColor = staticTagData.NameColor,
                    ScoreboardTag = staticTagData.ScoreboardTag
                };

                Instance._playerActiveTags[player.SteamID] = activeTag;
                UpdatePlayerScoreboardTag(player, activeTag);
                return;
            }
        }


        Instance._playerActiveTags.Remove(player.SteamID);
        RemovePlayerScoreboardTag(player);
    }
    public static void ApplyDefaultTag(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
            return;

        Instance._playerEquippedTags[player.SteamID].Clear();

        Tag_Item? defaultTag = FindHighestPriorityTag(player);

        if (defaultTag != null)
        {
            Instance._playerEquippedTags[player.SteamID].Add(defaultTag);
        }

        UpdatePlayerTags(player);
    }
    public static Tag_Item ConvertStaticToStandardTag(string permissionKey, StaticTagItem tagData)
    {
        var (tagColor, tagName) = ParseColoredTagString(tagData.Tag);
        return new Tag_Item
        {
            Id = $"static_{permissionKey}",
            Name = tagData.Name,
            Tag = tagName,
            TagColor = tagColor!,
            NameColor = tagData.NameColor,
            ChatColor = tagData.ChatColor,
            ScoreboardTag = tagData.ScoreboardTag
        };
    }
    public static void UpdatePlayerTags(CCSPlayerController player)
    {
        if (!Instance._playerEquippedTags.TryGetValue(player.SteamID, out var equippedTags) || equippedTags.Count == 0)
        {
            RemovePlayerScoreboardTag(player);
            return;
        }

        var scoreboardTagBuilder = new StringBuilder();
        foreach (var tag in equippedTags)
        {
            if (!string.IsNullOrEmpty(tag.ScoreboardTag))
            {
                scoreboardTagBuilder.Append(tag.ScoreboardTag);
            }
        }
        var masterTag = new Tag_Item
        {
            ScoreboardTag = scoreboardTagBuilder.ToString(),
            NameColor = equippedTags.Last().NameColor,
            ChatColor = equippedTags.Last().ChatColor
        };

        UpdatePlayerScoreboardTag(player, masterTag);
    }
    private static Tag_Item? FindHighestPriorityTag(CCSPlayerController player)
    {
        foreach (var tag in Instance.ModuleConfig.Tags.Values)
        {
            if (Instance.StoreApi.IsItemEquipped(player.SteamID, tag.Id, player.TeamNum))
            {
                if (tag.Id.Contains("custom", StringComparison.OrdinalIgnoreCase))
                {
                    Database.SetCustomTag(player.SteamID, tag.Id);
                    return null;
                }
                return tag;
            }
        }

        foreach (var staticTagEntry in Instance.Config.Tags.StaticTags)
        {
            if (CheckPermission(player, staticTagEntry.Key))
            {
                var tagData = staticTagEntry.Value;
                var (tagColor, tagName) = ParseColoredTagString(tagData.Tag);
                return new Tag_Item
                {
                    Id = $"static_{staticTagEntry.Key}",
                    Name = tagData.Name,
                    Tag = tagName,
                    TagColor = tagColor!,
                    NameColor = tagData.NameColor,
                    ChatColor = tagData.ChatColor,
                    ScoreboardTag = tagData.ScoreboardTag
                };
            }
        }
        return null;
    }
    public static (string? Color, string Tag) ParseColoredTagString(string coloredTag)
    {
        if (string.IsNullOrEmpty(coloredTag))
        {
            return (null, string.Empty);
        }

        var match = Regex.Match(coloredTag, @"^\{([a-zA-Z]+)\}");
        if (match.Success)
        {
            string color = match.Groups[1].Value;
            string tag = coloredTag.Substring(match.Length);
            return (color, tag);
        }

        return (null, coloredTag);
    }
}