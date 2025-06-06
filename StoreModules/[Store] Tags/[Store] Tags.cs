using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using CS2ScreenMenuAPI;
using Microsoft.Extensions.Logging;
using StoreAPI;
using T3MenuSharedApi;

namespace StoreCore;

public class Tags : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store] Tags";
    public override string ModuleVersion => "1.0.0";
    public static Tags Instance { get; set; } = new Tags();

    public IStoreAPI StoreApi = null!;
    public ModuleConfig ModuleConfig { get; set; } = new ModuleConfig();

    public Dictionary<ulong, Tag_Item> _playerActiveTags { get; set; } = new();
    public Dictionary<ulong, List<Tag_Item>> _playerEquippedTags { get; set; } = new();

    public IT3MenuManager MenuManager = null!;
    public IT3MenuManager GetMenuManager()
    {
        if (MenuManager == null)
        {
            MenuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get() ?? throw new Exception("T3MenuManager not found!");
        }
        return MenuManager;
    }

    public PluginConfig Config { get; set; } = new PluginConfig();
    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreAPI not found!");
        ModuleConfig = StoreApi.GetModuleConfig<ModuleConfig>("Tags");

        RegisterItems();

        StoreApi.OnPlayerPurchaseItem += OnItemPurchase;
        StoreApi.OnPlayerEquipItem += OnItemEquip;
        StoreApi.OnPlayerUnequipItem += OnItemUnequip;
        StoreApi.OnPlayerItemExpired += OnItemExpired;
    }

    public override void Load(bool hotReload)
    {
        Instance = this;

        HookUserMessage(118, OnMessage, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);

        foreach (var cmd in Config.Commands.TagsMenu)
        {
            AddCommand($"css_{cmd}", "Opens the tag menu", Command_Menu);
        }

        if (hotReload)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p.IsValid && !p.IsBot)
                    _playerEquippedTags[p.SteamID] = new List<Tag_Item>();
            }
        }

        Database.Initialize();
    }
    public void Command_Menu(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;


        switch (StoreApi.GetMenuType())
        {
            case MenuType.ScreenMenu:
                TagsMenu.DisplayScreenMenu(player);
                break;

            case MenuType.T3Menu:
                TagsMenu.DisplayT3Menu(player);
                break;
        }

    }
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || player.IsBot)
            return HookResult.Continue;

        _playerEquippedTags[player.SteamID] = new List<Tag_Item>();

        AddTimer(2.0f, () =>
        {
            if (player.IsValid)
            {
                Lib.ApplyDefaultTag(player);
            }
        });

        return HookResult.Continue;
    }
    public void OnItemExpired(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (_playerEquippedTags.TryGetValue(player.SteamID, out var equippedList))
        {
            int removedCount = equippedList.RemoveAll(t => t.Id == item["uniqueid"]);

            if (removedCount > 0)
            {
                if (equippedList.Count == 0)
                {
                    Lib.ApplyDefaultTag(player);
                }
                else
                {
                    Lib.UpdatePlayerTags(player);
                }
            }
        }
    }
    public void OnItemUnequip(CCSPlayerController player, Dictionary<string, string> item) => OnItemExpired(player, item);

    public void OnItemEquip(CCSPlayerController player, Dictionary<string, string> item)
    {
        var tagInfo = ModuleConfig.Tags.Values.FirstOrDefault(t => t.Id == item["uniqueid"]);

        if (tagInfo == null) return;

        if (tagInfo.Id.Contains("custom", StringComparison.OrdinalIgnoreCase))
        {
            Database.SetCustomTag(player.SteamID, tagInfo.Id);
        }
        else
        {
            _playerEquippedTags.TryGetValue(player.SteamID, out var equippedList);
            equippedList ??= new List<Tag_Item>();

            if (equippedList.Any(t => t.Id == tagInfo.Id)) return;

            if (equippedList.Count >= 2)
            {
                player.PrintToChat(Localizer["prefix"] + Localizer["tag.max_equipped", 2]);
                return;
            }

            equippedList.Add(tagInfo);
            _playerEquippedTags[player.SteamID] = equippedList;

            Lib.UpdatePlayerTags(player);
        }
    }
    public void OnItemPurchase(CCSPlayerController player, Dictionary<string, string> item)
    {
        foreach (var tag in ModuleConfig.Tags.Values)
        {
            if (item["uniqueid"] == tag.Id)
            {
                if (tag.Id.Contains("custom"))
                {
                    OpenCustomTagMenu(player, tag);
                }
                else
                {
                    _playerActiveTags[player.SteamID] = tag;
                    UpdatePlayerScoreboardTag(player, tag);
                }

                break;
            }
        }
    }


    public HookResult OnMessage(UserMessage um)
    {
        if (Utilities.GetPlayerFromIndex(um.ReadInt("entityindex")) is not CCSPlayerController player || player.IsBot ||
            !_playerEquippedTags.TryGetValue(player.SteamID, out var equippedTags) || equippedTags.Count == 0)
        {
            return HookResult.Continue;
        }

        string originalMessage = um.ReadString("param2");
        string originalPlayerName = um.ReadString("param1");
        bool isTeamMessage = !um.ReadString("messagename").Contains("All");

        if (string.IsNullOrEmpty(originalMessage))
            return HookResult.Continue;

        var prefixBuilder = new StringBuilder();
        if (!player.PawnIsAlive)
        {
            prefixBuilder.Append(ParseLocalizedColors(Localizer["tag.Dead"]));
        }
        prefixBuilder.Append(isTeamMessage ? GetLocalizedTeamPrefix(player.TeamNum) : ParseLocalizedColors(Localizer["tag.All"]));

        // Part B: Build the combined tag string with '+' separator
        var formattedTags = equippedTags
            .Where(tag => !string.IsNullOrEmpty(tag.Tag))
            .Select(tag =>
            {
                var tagColor = ConvertStringToChatColor(tag.TagColor);
                var tagText = tag.Tag.Trim();
                return $"{tagColor}{tagText}";
            });

        string combinedTagsString = string.Join($"{ChatColors.White} + ", formattedTags);
        if (!string.IsNullOrEmpty(combinedTagsString))
        {
            combinedTagsString = $"{combinedTagsString} ";
        }

        var masterTag = equippedTags.Last();

        char nameColor = ConvertStringToChatColor(masterTag.NameColor, player.TeamNum);
        string coloredPlayerName = $"{nameColor}{originalPlayerName}";

        string coloredMessage = ApplyTagToMessage(originalMessage, masterTag);

        string finalMessage = $"{prefixBuilder.ToString()}{combinedTagsString}{coloredPlayerName}{ChatColors.White}: {coloredMessage}";

        um.SetString("messagename", finalMessage);
        return HookResult.Changed;
    }

    private Tag_Item? GetPlayerActiveTag(CCSPlayerController player)
    {
        if (_playerActiveTags.TryGetValue(player.SteamID, out Tag_Item? cachedTag))
            return cachedTag;

        Tag_Item? activeTag = null;

        foreach (var tag in ModuleConfig.Tags.Values)
        {
            if (StoreApi.IsItemEquipped(player.SteamID, tag.Id, player.TeamNum))
            {
                activeTag = tag;

                if (tag.Id.Contains("custom", StringComparison.OrdinalIgnoreCase))
                {
                    ulong steamId = player.SteamID;
                    string tagId = tag.Id;

                    Task.Run(async () =>
                    {
                        try
                        {
                            var customTag = await Database.GetCustomTagAsync(steamId);
                            if (customTag != null)
                            {
                                customTag.Id = tagId;
                                customTag.Name = tag.Name;
                                customTag.Price = tag.Price;
                                customTag.Duration = tag.Duration;
                                customTag.Flags = tag.Flags;


                                Server.NextFrame(() =>
                                {
                                    _playerActiveTags[steamId] = customTag;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Instance.Logger.LogError($"Failed to load custom tag for player {steamId}: {ex.Message}");
                        }
                    });
                }
                break;
            }
        }

        if (activeTag != null && !activeTag.Id.Contains("custom", StringComparison.OrdinalIgnoreCase))
        {
            _playerActiveTags[player.SteamID] = activeTag;
        }

        return activeTag;
    }

    private string ApplyTagToPlayerName(CCSPlayerController player, string playerName, Tag_Item masterTag, bool isTeamMessage)
    {
        var nameBuilder = new StringBuilder();

        if (!string.IsNullOrEmpty(masterTag.NameColor))
        {
            char nameColor = ConvertStringToChatColor(masterTag.NameColor, player.TeamNum);
            nameBuilder.Append($"{nameColor}{playerName}");
        }
        else
        {
            nameBuilder.Append(playerName);
        }

        var finalPlayerName = nameBuilder.ToString();

        if (!player.PawnIsAlive)
        {
            string deadPrefix = ParseLocalizedColors(Localizer["tag.Dead"]);
            finalPlayerName = $"{deadPrefix}{finalPlayerName}";
        }

        if (isTeamMessage)
        {
            string teamPrefix = GetLocalizedTeamPrefix(player.TeamNum);
            finalPlayerName = $"{teamPrefix}{finalPlayerName}";
        }
        else
        {
            string allPrefix = ParseLocalizedColors(Localizer["tag.All"]);
            finalPlayerName = $"{allPrefix}{finalPlayerName}";
        }

        return finalPlayerName;
    }
    private string ApplyTagToMessage(string message, Tag_Item tag)
    {
        if (!string.IsNullOrEmpty(tag.ChatColor))
        {
            char chatColor = ConvertStringToChatColor(tag.ChatColor);
            return $"{chatColor}{message}";
        }

        return message;
    }

    private string GetLocalizedTeamPrefix(int teamNum)
    {
        return teamNum switch
        {
            2 => ParseLocalizedColors(Localizer["tag.T"]),
            3 => ParseLocalizedColors(Localizer["tag.CT"]),
            _ => ParseLocalizedColors(Localizer["tag.SPEC"])
        };
    }

    private string ParseLocalizedColors(string localizedString)
    {
        if (string.IsNullOrEmpty(localizedString))
            return string.Empty;

        return localizedString
            .Replace("{white}", ChatColors.White.ToString())
            .Replace("{red}", ChatColors.Red.ToString())
            .Replace("{yellow}", ChatColors.Yellow.ToString())
            .Replace("{blue}", ChatColors.Blue.ToString())
            .Replace("{purple}", ChatColors.Purple.ToString())
            .Replace("{magenta}", ChatColors.Magenta.ToString())
            .Replace("{silver}", ChatColors.Silver.ToString())
            .Replace("{green}", ChatColors.Green.ToString())
            .Replace("{lime}", ChatColors.Lime.ToString())
            .Replace("{gold}", ChatColors.Gold.ToString())
            .Replace("{orange}", ChatColors.Orange.ToString())
            .Replace("{grey}", ChatColors.Grey.ToString())
            .Replace("{olive}", ChatColors.Olive.ToString())
            .Replace("{darkred}", ChatColors.DarkRed.ToString())
            .Replace("{default}", ChatColors.Default.ToString());
    }

    private char ConvertStringToChatColor(string colorString, int teamNum = 0)
    {
        if (string.IsNullOrEmpty(colorString))
            return ChatColors.Default;

        if (colorString.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            return teamNum switch
            {
                2 => ChatColors.Yellow,
                3 => ChatColors.Blue,
                _ => ChatColors.White
            };
        }

        string cleanColor = colorString.Trim('{', '}');

        return cleanColor.ToLower() switch
        {
            "default" => ChatColors.Default,
            "white" => ChatColors.White,
            "darkred" => ChatColors.DarkRed,
            "green" => ChatColors.Green,
            "olive" => ChatColors.Olive,
            "lime" => ChatColors.Lime,
            "red" => ChatColors.Red,
            "purple" => ChatColors.Purple,
            "grey" => ChatColors.Grey,
            "yellow" => ChatColors.Yellow,
            "gold" => ChatColors.Gold,
            "silver" => ChatColors.Silver,
            "blue" => ChatColors.Blue,
            "magenta" => ChatColors.Magenta,
            "orange" => ChatColors.Orange,
            _ => ChatColors.Default
        };
    }

    public static void UpdatePlayerScoreboardTag(CCSPlayerController player, Tag_Item tag)
    {
        if (player == null || player.IsBot)
            return;

        if (!string.IsNullOrEmpty(tag.ScoreboardTag))
        {
            player.Clan = tag.ScoreboardTag;
            player.ClanName = tag.ScoreboardTag;


            Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");

            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();

            if (gameRules is null)
            {
                return;
            }

            gameRules.GameRules!.NextUpdateTeamClanNamesTime = Server.CurrentTime - 0.01f;
        }

        new EventNextlevelChanged(false).FireEventToClient(player);
    }

    public static void RemovePlayerScoreboardTag(CCSPlayerController player)
    {
        if (player == null || player.IsBot)
            return;

        player.Clan = "";
        player.ClanName = "";

        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();

        if (gameRules is null)
        {
            return;
        }

        gameRules.GameRules!.NextUpdateTeamClanNamesTime = Server.CurrentTime - 0.01f;
        new EventNextlevelChanged(false).FireEventToClient(player);
    }

    public override void Unload(bool hotReload)
    {
        UnregisterItems();
        UnhookUserMessage(118, OnMessage, HookMode.Pre);
    }

    private void RegisterItems()
    {
        foreach (var tag in ModuleConfig.Tags.Values)
        {
            StoreApi.RegisterItem(
                tag.Id,
                tag.Name,
                ModuleConfig.Category,
                "tags",
                tag.Price,
                tag.Description,
                tag.Flags,
                duration: tag.Duration
            );
        }
    }
    public void OpenCustomTagMenu(CCSPlayerController player, Tag_Item tag)
    {
        var manager = GetMenuManager();
        var menu = manager.CreateMenu(Localizer.ForPlayer(player, "customtag.menu"));
        menu.IsExitable = false;

        string tagName = string.Empty;
        string tagColor = string.Empty;
        string nameColor = string.Empty;
        string chatColor = string.Empty;

        menu.AddInputOption(Localizer.ForPlayer(player, "tag.CustomTagOption"), Localizer.ForPlayer(player, "tag.CustomTagOption.PlaceHolder"), (p, o, input) =>
        {
            tagName = input.ToString();
        }, Localizer.ForPlayer(player, "prefix") + Localizer.ForPlayer(player, "tag.AnnounceChat"));

        menu.AddInputOption(Localizer.ForPlayer(player, "tag.CustomTagColorOption"), Localizer.ForPlayer(player, "tag.CustomTagColorOption.PlaceHolder"), (p, o, input) =>
        {
            tagColor = input.ToString();
        }, Localizer.ForPlayer(player, "prefix") + Localizer.ForPlayer(player, "tagColor.AnnounceChat"));

        menu.AddInputOption(Localizer.ForPlayer(player, "tag.CustomNameColorOption"), Localizer.ForPlayer(player, "tag.CustomNameColorOption.PlaceHolder"), (p, o, input) =>
        {
            nameColor = input.ToString();
        }, Localizer.ForPlayer(player, "prefix") + Localizer.ForPlayer(player, "name.AnnounceChat"));

        menu.AddInputOption(Localizer.ForPlayer(player, "tag.CustomChatColorOption"), Localizer.ForPlayer(player, "tag.CustomChatColorOption.PlaceHolder"), (p, o, input) =>
        {
            chatColor = input.ToString();
        }, Localizer.ForPlayer(player, "prefix") + Localizer.ForPlayer(player, "chatColor.AnnounceChat"));

        menu.AddOption(Localizer.ForPlayer(player, "tag.PreviewOption"), (p, o) =>
        {
            if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagColor) || string.IsNullOrEmpty(nameColor) || string.IsNullOrEmpty(chatColor))
            {
                player.PrintToChat(Localizer["prefix"] + Localizer["tag.Error"]);
                return;
            }

            string previewMessage = $" {ConvertStringToChatColor(tagColor)}{tagName} {ConvertStringToChatColor(nameColor)}{player.PlayerName}{ChatColors.White}: {ConvertStringToChatColor(chatColor)}Hey! This is my new tag!";
            player.PrintToChat(previewMessage);
        });
        menu.AddOption(Localizer.ForPlayer(player, "tag.ConfirmOption"), (p, o) =>
    {
        if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagColor) || string.IsNullOrEmpty(nameColor) || string.IsNullOrEmpty(chatColor))
        {
            player.PrintToChat(Localizer["prefix"] + Localizer["tag.Error"]);
            return;
        }

        var customTag = new Tag_Item
        {
            Id = tag.Id,
            Name = tag.Name,
            Tag = tagName,
            TagColor = tagColor,
            NameColor = nameColor,
            ChatColor = chatColor,
            ScoreboardTag = tagName,
            Price = tag.Price,
            Duration = tag.Duration,
            Flags = tag.Flags,
            Description = tag.Description
        };

        _playerEquippedTags.TryGetValue(p.SteamID, out var equippedList);
        equippedList ??= new List<Tag_Item>();

        equippedList.Clear();

        equippedList.Add(customTag);
        _playerEquippedTags[p.SteamID] = equippedList;

        Lib.UpdatePlayerTags(p);
        Database.SaveCustomTag(p.SteamID, customTag);

        p.PrintToChat(Localizer["prefix"] + Localizer["tag.CustomTagCreated", tagName]);
        manager.CloseMenu(p);
    });
        manager.OpenMainMenu(player, menu);
    }
    private void UnregisterItems()
    {
        foreach (var tag in ModuleConfig.Tags.Values)
        {
            StoreApi.UnregisterItem(tag.Id);
        }
    }
}

public class ModuleConfig
{
    public string Category { get; set; } = "Tags";
    public Dictionary<string, Tag_Item> Tags { get; set; } = new Dictionary<string, Tag_Item>()
    {
        {
            "1", new Tag_Item
            {
                Id = "premium_tag",
                Name = "Premium",
                ScoreboardTag = "[PREMIUM] ",
                Tag = "[PREMIUM] ",
                TagColor = "gold",
                Description = "Tag color: Gold | NameColor: Team | ChatColor: Lime",
                ChatColor = "lime",
                NameColor = "team",
                Price = 1000,
                Duration = 1500,
                Flags = ""
            }
        },
        {
            "2", new Tag_Item
            {
                Id = "custom_tag",
                Name = "Custom Tag",
                Tag = "",
                TagColor = "",
                Description = "",
                ChatColor = "",
                NameColor = "",
                Price = 1000,
                Duration = 2500,
                Flags = ""
            }
        }
    };
}

public class Tag_Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string ScoreboardTag { get; set; } = string.Empty;
    public string TagColor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChatColor { get; set; } = string.Empty;
    public string NameColor { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Duration { get; set; } = 0;
    public string Flags { get; set; } = string.Empty;
}