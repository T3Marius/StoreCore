using CounterStrikeSharp.API.Core;
using static StoreCore.Tags;
using CS2ScreenMenuAPI;
using CounterStrikeSharp.API.Core.Translations;

namespace StoreCore;

public static class TagsMenu
{
    private const int MAX_EQUIPPED_TAGS = 2;
    public static void DisplayScreenMenu(CCSPlayerController player)
    {
        Menu menu = new Menu(player, Instance)
        {
            Title = Instance.Localizer.ForPlayer(player, "tags.menu.title")
        };

        var allAvailableTags = GetAllAvailableTags(player);

        Instance._playerEquippedTags.TryGetValue(player.SteamID, out var equippedList);
        equippedList ??= new List<Tag_Item>();

        if (allAvailableTags.Any())
        {
            foreach (var tag in allAvailableTags)
            {
                bool isEquipped = equippedList.Any(t => t.Id == tag.Id);
                string displayName = isEquipped ? $"{tag.Name} [✔]" : tag.Name; // Manual checkmark
                bool canEquipMore = equippedList.Count < MAX_EQUIPPED_TAGS;
                bool isDisabled = !isEquipped && !canEquipMore;

                menu.AddItem(displayName, (p, o) =>
                {
                    HandleTagToggle(p, tag, isEquipped, canEquipMore);
                    DisplayScreenMenu(p);
                }, isDisabled);
            }
        }
        else
        {
            menu.AddItem(Instance.Localizer.ForPlayer(player, "no.tags"), (p, o) => { }, true);
        }
        menu.Display();
    }

    public static void DisplayT3Menu(CCSPlayerController player)
    {
        var manager = Instance.GetMenuManager();
        var menu = manager.CreateMenu(Instance.Localizer.ForPlayer(player, "tags.menu.title"));

        var allAvailableTags = GetAllAvailableTags(player);

        Instance._playerEquippedTags.TryGetValue(player.SteamID, out var equippedList);
        equippedList ??= new List<Tag_Item>();

        if (allAvailableTags.Any())
        {
            foreach (var tag in allAvailableTags)
            {
                bool isEquipped = equippedList.Any(t => t.Id == tag.Id);
                string displayName = isEquipped ? $"{tag.Name} [✔]" : tag.Name;
                bool canEquipMore = equippedList.Count < MAX_EQUIPPED_TAGS;
                bool isDisabled = !isEquipped && !canEquipMore;

                menu.AddOption(displayName, (p, o) =>
                {
                    HandleTagToggle(p, tag, isEquipped, canEquipMore);
                    DisplayT3Menu(p);
                }, isDisabled);
            }
        }
        else
        {
            menu.AddOption(Instance.Localizer.ForPlayer(player, "no.tags"), (p, o) => { }, true);
        }
        manager.OpenMainMenu(player, menu);
    }
    private static List<Tag_Item> GetAllAvailableTags(CCSPlayerController player)
    {
        var allTags = new List<Tag_Item>();

        allTags.AddRange(Instance.Config.Tags.StaticTags
            .Where(entry => Lib.CheckPermission(player, entry.Key))
            .Select(entry => Lib.ConvertStaticToStandardTag(entry.Key, entry.Value)));


        allTags.AddRange(Instance.ModuleConfig.Tags.Values
            .Where(tag => Instance.StoreApi.IsItemEquipped(player.SteamID, tag.Id, player.TeamNum)));

        return allTags.DistinctBy(t => t.Id).ToList();
    }

    private static void HandleTagToggle(CCSPlayerController player, Tag_Item tag, bool isCurrentlyEquipped, bool canEquipMore)
    {
        Instance._playerEquippedTags.TryGetValue(player.SteamID, out var equippedList);
        equippedList ??= new List<Tag_Item>();

        if (isCurrentlyEquipped)
        {
            equippedList.RemoveAll(t => t.Id == tag.Id);
            player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["tag.unequipped", tag.Name]);
        }
        else
        {
            if (canEquipMore)
            {
                equippedList.Add(tag);
                player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["tag.equipped", tag.Name]);
            }
            else
            {
                player.PrintToChat(Instance.Localizer["prefix"] + Instance.Localizer["tag.max_equipped", MAX_EQUIPPED_TAGS]);
            }
        }
        Lib.UpdatePlayerTags(player);
    }
}