using CounterStrikeSharp.API.Core;

namespace StoreCore;

public class StoreConfig : BasePluginConfig
{
    public Main_Config MainConfig { get; set; } = new Main_Config();
    public Commands_Config Commands { get; set; } = new Commands_Config();
    public Database_Config Database { get; set; } = new Database_Config();

}
public class Main_Config
{
    public string MenuType { get; set; } = "t3";
    public int StartCredits { get; set; } = 0;
    public float PlaytimeInterval { get; set; } = 60.0f;
    public int CreditsPerInterval { get; set; } = 10;
    public int CreditsPerKill { get; set; } = 5;
    public int CreditsPerRoundWin { get; set; } = 20;
    public bool IgnoreWarmup { get; set; } = true;
}
public class Commands_Config
{
    public List<string> OpenStore { get; set; } = ["store", "shop", "inventory"];
    public List<string> ShowCredits { get; set; } = ["credits", "mycredits", "balance"];
    public List<string> AddCredits { get; set; } = ["addcredits", "givecredits"];
    public List<string> SetCredits { get; set; } = ["setcredits"];
    public List<string> RemoveCredits { get; set; } = ["removecredits", "takecredits"];
    public List<string> GiftCredits { get; set; } = ["gift", "giftcredits"];
    public List<string> ResetDatabase { get; set; } = ["resetdb", "resetdatabase"];
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
