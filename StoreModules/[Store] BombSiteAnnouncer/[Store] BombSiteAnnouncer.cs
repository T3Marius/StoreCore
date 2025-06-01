using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using StoreAPI;

namespace BombsiteAnnouncer;

public class Config : BasePluginConfig
{
    [JsonPropertyName("store-category")]
    public string StoreCategory { get; set; } = "BombsiteAnnouncer";

    [JsonPropertyName("store-item-id")]
    public string StoreItemId { get; set; } = "bombsite_announcer_access_30";

    [JsonPropertyName("store-item-name")]
    public string StoreItemName { get; set; } = "Bombsite Announcer Access";

    [JsonPropertyName("store-item-type")]
    public string StoreItemType { get; set; } = "Visual";

    [JsonPropertyName("store-item-price")]
    public int StoreItemPrice { get; set; } = 10000;

    [JsonPropertyName("store-item-description")]
    public string StoreItemDescription { get; set; } = "Grants access to the bombsite announcer messages for 30 minutes.";

    [JsonPropertyName("store-item-duration")]
    public int StoreItemDuration { get; set; } = 1800;

    [JsonPropertyName("show-announcer-delay")]
    public float ShowAnnouncerDelay { get; set; } = 0.1f;

    [JsonPropertyName("announcer-visible-for-time")]
    public float AnnouncerVisibleForTime { get; set; } = 10.0f;

    [JsonPropertyName("remove-bomb-planted-message")]
    public bool RemoveDefaultMsg { get; set; } = false;

    [JsonPropertyName("bombsite-A-img")]
    public string BombsiteAimg { get; set; } = "https://raw.githubusercontent.com/audiomaster99/CS2BombsiteAnnouncer/main/img/Site-A.png";

    [JsonPropertyName("bombsite-B-img")]
    public string BombsiteBimg { get; set; } = "https://raw.githubusercontent.com/audiomaster99/CS2BombsiteAnnouncer/main/img/Site-B.png";

    [JsonPropertyName("show-site-info-text")]
    public bool SiteText { get; set; } = true;

    [JsonPropertyName("show-site-info-image")]
    public bool SiteImage { get; set; } = true;

    [JsonPropertyName("show-player-counter")]
    public bool PlayerCounter { get; set; } = true;

    [JsonPropertyName("show-announcer-flags")]
    public List<string> ShowAnnouncerFlags { get; set; } = new List<string>
    {
        "@css/vip",
        "leave empty array []; to show announcer to everyone"
    };

    [JsonPropertyName("ConfigVersion")]
    public override int Version { get; set; } = 3;
}


public class BombsiteAnnouncer : BasePlugin
{
    public override string ModuleName => "BombsiteAnnouncer";
    public override string ModuleAuthor => "itsAudio and GSM-RO";
    public override string ModuleDescription => "Simple bombsite announcer";
    public override string ModuleVersion => "1.0.1";

    public required Config Config { get; set; }
    public IStoreAPI? StoreApi;

    private bool bombsiteAnnouncer;
    private bool isRetakesEnabled;
    private string? bombsite;
    private string? message;
    private string? color;
    private string? siteTextString;
    private string? siteImageString;
    private string? playerCounterString;
    private string? siteImage;
    private string? breakLine;
    private int ctNum;
    private int ttNum;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("BombsiteAnnouncer Plugin has started!");

        //AddTimer(0.1f, () => { IsRetakesPluginInstalled(); });

        RegisterListener<Listeners.OnTick>(() =>
        {
            if (bombsiteAnnouncer)
            {
                Utilities.GetPlayers()
                    .Where(player => IsValid(player) && IsConnected(player) && PlayerHasPermissions(player) && HasAccessToAnnouncer(player))
                    .ToList()
                    .ForEach(p => OnTick(p));
            }
        });
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get()
            ?? throw new Exception("StoreAPI not found! Please make sure StoreCore is installed.");

        Config = StoreApi.GetModuleConfig<Config>(ModuleName) ?? Config;
        RegisterItem();
    }

    private bool HasAccessToAnnouncer(CCSPlayerController player)
    {
        if (StoreApi == null)
            return false;

        return StoreApi.IsItemEquipped(player.SteamID, Config.StoreItemId, player.TeamNum);
    }

    private void OnTick(CCSPlayerController player)
    {
        ctNum = GetCurrentNumPlayers(CsTeam.CounterTerrorist);
        ttNum = GetCurrentNumPlayers(CsTeam.Terrorist);

        if (player.Team == CsTeam.CounterTerrorist)
        {
            color = "green";
            message = Localizer["phrases.retake"];
        }
        else
        {
            color = "red";
            message = Localizer["phrases.defend"];
        }
        HandleCustomizedMessage();
        player.PrintToCenterHtml(siteTextString + siteImageString + playerCounterString);
    }

    public void ShowAnnouncer()
    {
        AddTimer(Config.ShowAnnouncerDelay, () =>
        {
            bombsiteAnnouncer = true;
            AddTimer(Config.AnnouncerVisibleForTime, () => { bombsiteAnnouncer = false; });
        });
    }

    public void HandleCustomizedMessage()
    {
        siteTextString = Config.SiteText ? $"<font class='fontSize-l' color='{color}'>{message} <font color='white'>{Localizer["phrases.site"]}</font> <font color='{color}'>{bombsite}</font>{breakLine}" : "";
        siteImageString = Config.SiteImage ? $"<img src='{siteImage}'>  {breakLine}" : "";
        playerCounterString = Config.PlayerCounter ? $"<font class='fontSize-m' color='white'>{ttNum}</font> <font class='fontSize-m'color='red'>{Localizer["phrases.terrorist"]}   </font><font class='fontSize-m' color='white'> {Localizer["phrases.versus"]}</font>   <font class='fontSize-m' color='white'> {ctNum}   </font><font class='fontSize-m' color='blue'>{Localizer["phrases.cterrorist"]}</font>" : "";

        //fix bad looking message if some lines are not displayed
        //caused by line-break
        if (!Config.SiteText && !Config.PlayerCounter && Config.SiteImage) { breakLine = ""; }
        else if (Config.SiteText && !Config.PlayerCounter && !Config.SiteImage) { breakLine = ""; }
        else { breakLine = "<br>"; }
    }

    public void GetSiteImage()
    {
        siteImage = bombsite == "B" ? Config.BombsiteBimg : bombsite == "A" ? Config.BombsiteAimg : "";
        if (siteImage == "")
        {
            Logger.LogWarning($"Unknown bombsite value: {bombsite}");
        }
    }

    //---- P L U G I N - H O O O K S ----
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {

        CCSPlayerController? player = @event.Userid;
        CBombTarget site = new CBombTarget(NativeAPI.GetEntityFromIndex(@event.Site));

        if (isRetakesEnabled == true)
        {
            bombsite = (@event.Site == 1) ? "B" : "A";
        }
        else bombsite = site.IsBombSiteB ? "B" : "A";

        GetSiteImage();
        ShowAnnouncer();
        Logger.LogInformation($"Bomb Planted on [{bombsite}]");

        // remove bomb planted message
        if (Config.RemoveDefaultMsg && @event != null)
        {
            return HookResult.Handled;
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        bombsiteAnnouncer = false;
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombDetonate(EventBombExploded @event, GameEventInfo info)
    {
        bombsiteAnnouncer = false;
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        bombsiteAnnouncer = false;
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        bombsiteAnnouncer = false;
        return HookResult.Continue;
    }

    //---- P L U G I N - H E L P E R S ----
    static bool IsValid(CCSPlayerController? player)
    {
        return player?.IsValid == true && player.PlayerPawn?.IsValid == true && !player.IsBot && !player.IsHLTV;
    }

    static bool IsConnected(CCSPlayerController? player)
    {
        return player?.Connected == PlayerConnectedState.PlayerConnected;
    }

    static bool IsAlive(CCSPlayerController player)
    {
        return player.PawnIsAlive;
    }

    public static int GetCurrentNumPlayers(CsTeam? csTeam = null)
    {
        return Utilities.GetPlayers().Count(player => IsAlive(player) && IsConnected(player) && (csTeam == null || player.Team == csTeam));
    }

    public void IsRetakesPluginInstalled()
    {
        string? path = Directory.GetParent(ModuleDirectory)?.FullName;
        if (Directory.Exists(path + "/RetakesPlugin"))
        {
            Logger.LogInformation("RETAKES MODE ENABLED");
            isRetakesEnabled = true;
        }
        else isRetakesEnabled = false;
    }

    public bool PlayerHasPermissions(CCSPlayerController player)
    {
        if (Config.ShowAnnouncerFlags.Count == 0)
            return true;

        foreach (string checkPermission in Config.ShowAnnouncerFlags)
        {
            switch (checkPermission[0])
            {
                case '@':
                    if (AdminManager.PlayerHasPermissions(player, checkPermission))
                        return true;
                    break;
                case '#':
                    if (AdminManager.PlayerInGroup(player, checkPermission))
                        return true;
                    break;
                default:
                    if (AdminManager.PlayerHasCommandOverride(player, checkPermission))
                        return true;
                    break;
            }
        }
        return false;
    }
    private void RegisterItem()
    {
        if (StoreApi == null)
            return;

        StoreApi.RegisterItem(
            Config.StoreItemId,
            Config.StoreItemName,
            Config.StoreCategory,
            Config.StoreItemType,
            Config.StoreItemPrice,
            Config.StoreItemDescription,
            duration: Config.StoreItemDuration
        );
    }
    private void UnregisterItem()
    {
        if (StoreApi == null)
            return;

        StoreApi.UnregisterItem(Config.StoreItemId);
    }
    public override void Unload(bool hotReload)
    {
        UnregisterItem();
    }
}