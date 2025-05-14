# [StoreCore]

- StoreCore, as the name says it's designed to enhances your server gameplay and have a credit system that can be used to buy items from the store or create mini-games for players.

- Since it's a core when you first add it to your server it won't have any items in it, just the menu and the credits system. To have items in the store you need to instal the modules from release too.

- After installing a module, it's config it's automaticly created in configs/plugins/StoreCore/Modules/ModuleName.json (Of course that will be from modules created by me. You can put your own module config anywhere you want.)

- If you need example of how to use the StoreApi, you have all examples you need in the StoreModules folder.

- More modules will be created by me in the future, but if you want feel free to create a module and open a PR, i'll gladly accept it. For the repository, add your module and open the pr.

- I don't plan on giving up on this project anytime soon, so there will be updates weekly on this. Maybe even sooner.

# Avalible Modules

- [PlayerModels] | Allows you to add how many models you want from the module config.

- [Abilities] | Speed/Gravity/God Mode. You can add more of them with each one having a different Duration, etc.

- [Flags] | Players can buy custom flags from the store with duration. For example you could add vip flag, premium flag, acces flag.

- [SpawnEffects] | SpawnEffects on player spawn, a bomb like explosion.

- [Hit-Sounds] | As the name says, you can add how much hit sounds you want with different prices/durations/etc. They are able to preview the hit sounds too.

- [Killscreen] | This one is also previewable, players gains killscreen with the healtshot like effect.

- [SmokeColor] | You can add how many smoke colors you want in config.

- [Tracers] | You can add how many tracers colors you want in config + random tracer or team tracer.

- [Trails] | Allows players to buy trails, you can set all colors u want.
  
- [VIPShop] | You need VIPCore for this. You can allow players to buy vip groups with durations.

- [VIPBhop] | Allows players to buy BHOP from shop, speed aplicable

- [Roulette] | Made by @varkit | Allows players to bet on red/blue/green configurable from config.

- [Parachute] | Made by @Mesharsky | Allows players to fall slowly by holding E key.

- All modules will have a default config automaticly created when added, so no worry about how to use them!.

# Menu Requirements

- **[** [**T3Menu**](https://github.com/T3Marius/T3Menu-API) **]**
- **[** [**CS2ScreenMenu**](https://github.com/T3Marius/CS2ScreenMenuAPI) **]**

- I know it's a bit annoying to have 2 more API's installed with this one, but i can't do it other way at the moment, SORRY!

**NOTE**: There's a special option in store menu only for t3menu named functions. Which allows an admin to send credits from the menu to players (@css/root). It might be added in screen menu in the future.

# Config 
```toml
ConfigVersion = 1

[Database] # database connection
Host = ""
Name = ""
User = ""
Pass = ""
Port = 3306

[MainConfig]
MenuType = "t3"           # there are only 2 menus, screen or t3.
StartCredits = 0          # How much credits a new player will have at start. 
PlaytimeInterval = 60     # Each second player will be awarded with credits for activity. Put 0 to disabled it. 
CreditsPerInterval = 0    # How many credits player gets per activity interval. Put 0 to disable it.
CreditsPerKill = 0        # How many credits player gets from a kill. Put 0 to disable it.
CreditsPerRoundWin = 0    # How many credits player gets after winning a round. Put 0 to disable it.
IgnoreWarmup = true       # Give credits during warmup? true to not or false to do.

[Multiplier]
CreditsPerInterval = { "@css/vip" = 2, "@css/root" = 4 }        # credits per interval multiplier for these flags.
CreditsPerKill = { "@css/vip" = 2, "@css/root" = 4 }
CreditsPerRoundWin = { "@css/vip" = 2, "@css/root" = 4 }


[Commands]
OpenStore = ["store", "shop", "market"]          # commands to open the main store.
ShowCredits = ["credits", "mycredits"]           # commands to show your current credits.
AddCredits = ["addcredits", "givecredits"]       # commands to give credits, as an admin.
SetCredits = ["setcredits"]                      # command to set credits, as an admin.
RemoveCredits = ["removecredits"]                # command to remove an amount of credits from a player.
GiftCredits = ["giftcredits", "gift"]            # commands to gift someone credits (for normal players)
ResetCredits = ["rc", "resetcredits"]            # commands to reset all players credits

[Permissions]		# set permission foreach command.
StoreCommand = []
AddCredits = ["@css/root"]
RemoveCredits = ["@css/root"]
SetCredits = ["@css/root"]
ResetCredits = ["@css/root"]
```

# Module Example
**.csproj** file
```c#
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>_StoreCore__Killscreen</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CounterStrikeSharp.API" Version="1.0.316" />
  </ItemGroup>

	<ItemGroup> // add the store api dll reference, need to be in your project.
		<Reference Include="StoreAPI">
			<HintPath>..\..\StoreAPI.dll</HintPath>
		</Reference>
	</ItemGroup>

</Project>
```

```c#
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using StoreAPI;

namespace StoreCore;

public class Killscreen : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[StoreCore] Killscreen";
    public override string ModuleVersion => "1.0.0";
    public IStoreAPI? StoreApi; // get the api
    public PluginConfig Config { get; set; } = new PluginConfig(); // get the plugin config
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreAPI.Capability.Get() ?? throw new Exception("StoreApi not found!"); // get the store core capability.
        Config = StoreApi.GetModuleConfig<PluginConfig>("Killscreen"); // this will automaticly create the config in configs/plugins/StoreCore/Modules/.

        if (!hotReload)
        {
            foreach (var kvp in Config.Killscreens)
            {
                var killScreen = kvp.Value;

                StoreApi.RegisterItem(
                    killScreen.Id,
                    killScreen.Name,
                    Config.Category,
                    killScreen.Type,
                    killScreen.Price,
                    killScreen.Description,
                    duration: killScreen.Duration);
            }
        }
        StoreApi.OnItemPreview += OnItemPreview; // register the event when player selects preview button
    }
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;
        
        if (victim == null || attacker == null || victim == attacker)
            return HookResult.Continue;

        CCSPlayerPawn? attackerPawn = attacker.PlayerPawn.Value;

        if (StoreApi == null || attackerPawn == null)
            return HookResult.Continue;

        foreach (var kvp in Config.Killscreens)
        {
            var killScreen = kvp.Value;
            if (StoreApi.IsItemEquipped(attacker.SteamID, killScreen.Id, attacker.TeamNum))
            {
                attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                break;
            }
        }

        return HookResult.Continue;
    }
    public void OnItemPreview(CCSPlayerController player, string uniqueId)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        foreach (var kvp in Config.Killscreens)
        {
            var killScreen = kvp.Value;
            if (uniqueId == killScreen.Id) // if item unique id is equals to the one he selects it then create the code...
            {
                pawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                break;
            }
        }
    }
}
public class PluginConfig // if you want the config to be created automaticly in configs/plugins/StoreCore/Modules/ModuleName.json
{
    public string Category { get; set; } = "Special Effects";
    public Dictionary<string, Killscreen_Item> Killscreens { get; set; } = new Dictionary<string, Killscreen_Item>()
    {
        {
            "1", new Killscreen_Item
            {
                Id = "killscreen_2_minutes",
                Name = "Killscreen (2) minutes",
                Price = 500,
                Duration = 120,
                Type = "Visual",
                Description = ""
            }
        },
    };
}
public class Killscreen_Item
{
    public string Id { get; set; } = "store_killscreen"; // item id, very imporatant to not have them duplicated.
    public string Name { get; set; } = "Killscreen";     // the item name that will be shown in the store menu.
    public int Price { get; set; } = 1500;               // item price, sell price is automaticly for now.
    public int Duration { get; set; } = 240;             // item duration, in seconds. This will work only if IsEquipable is set to true when you register an item.
    public string Type { get; set; } = "Visual";         // item type, this is not that important.
    public string Description { get; set; } = "";        // item description, if it's not empty it will be shown when they try to purchase the item.
}
```

If you willing to donate/support me you can do that here **[** [**Donation**](https://revolut.me/dumitrqxrj) **]**

if you need help or have trouble with the plugin dm me on discord mariust3 or send a message on the thread from CounterStrikeSharp server.
