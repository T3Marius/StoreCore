
using CounterStrikeSharp.API.Core;

namespace StoreCore;

public class PluginConfig : BasePluginConfig
{
    public Database_Config Database { get; set; } = new Database_Config();
    public Commands_Config Commands { get; set; } = new Commands_Config();
    public Tags_Config Tags { get; set; } = new Tags_Config();
}
public class Tags_Config
{
    public Dictionary<string, StaticTagItem> StaticTags { get; set; } = new();
}
public class Commands_Config
{
    public List<string> TagsMenu { get; set; } = ["tags", "tag"];
}

public class Database_Config
{
    public string Host { get; set; } = "host";
    public string Name { get; set; } = "name";
    public string User { get; set; } = "user";
    public string Pass { get; set; } = "pass";
    public uint Port { get; set; } = 3306;
}
public class StaticTagItem
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string ScoreboardTag { get; set; } = string.Empty;
    public string ChatColor { get; set; } = string.Empty;
    public string NameColor { get; set; } = string.Empty;
}

