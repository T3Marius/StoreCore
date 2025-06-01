using CounterStrikeSharp.API.Core;

namespace StoreCore;

public class StoreConfig : BasePluginConfig
{
    public Database_Config Database { get; set; } = new Database_Config();
    public Main_Config MainConfig { get; set; } = new Main_Config();
    public Credits_Multiplier Multiplier { get; set; } = new Credits_Multiplier();
    public Commands_Config Commands { get; set; } = new Commands_Config();
    public Permission_Config Permissions { get; set; } = new Permission_Config();

}
public class Permission_Config
{
    public List<string> StoreCommand { get; set; } = [];
    public List<string> InventoryCommand { get; set; } = [];
    public List<string> AddCredits { get; set; } = ["@css/root"];
    public List<string> RemoveCredits { get; set; } = ["@css/root"];
    public List<string> SetCredits { get; set; } = ["@css/rcon"];
    public List<string> ResetCredits { get; set; } = ["@css/root"];
}
public class Main_Config
{
    public string MenuType { get; set; } = "t3";
    public int StartCredits { get; set; } = 0;
    public float PlaytimeInterval { get; set; } = 60.0f;
    public int CreditsPerInterval { get; set; } = 10;
    public int CreditsPerKill { get; set; } = 5;
    public int CreditsPerRoundWin { get; set; } = 20;
    public bool ShowCreditsOnRoundEnd { get; set; } = true;
    public bool IgnoreWarmup { get; set; } = true;
}
public class Credits_Multiplier
{
    public Dictionary<string, int> CreditsPerInterval { get; set; } = new Dictionary<string, int>()
    {
        {
            "@css/vip", 2
        }
    };
    public Dictionary<string, int> CreditsPerKill { get; set; } = new Dictionary<string, int>()
    {
        {
            "@css/vip", 2
        }
    };
    public Dictionary<string, int> CreditsPerRoundWin { get; set; } = new Dictionary<string, int>()
    {
        {
            "@css/vip", 2
        }
    };

}
public class Commands_Config
{
    public List<string> OpenStore { get; set; } = ["store", "shop"];
    public List<string> OpenInventoy { get; set; } = ["inventory", "inv"];
    public List<string> ShowCredits { get; set; } = ["credits", "mycredits", "balance"];
    public List<string> AddCredits { get; set; } = ["addcredits", "givecredits"];
    public List<string> SetCredits { get; set; } = ["setcredits"];
    public List<string> RemoveCredits { get; set; } = ["removecredits", "takecredits"];
    public List<string> GiftCredits { get; set; } = ["gift", "giftcredits"];
    public List<string> ResetCredits { get; set; } = ["resetcredits", "rc"];
}
public class Database_Config
{
    public string Host { get; set; } = "host";
    public string Name { get; set; } = "name";
    public string User { get; set; } = "user";
    public string Pass { get; set; } = "pass";
    public uint Port { get; set; } = 3306;
}
