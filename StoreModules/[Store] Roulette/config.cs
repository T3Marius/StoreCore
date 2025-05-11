using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StoreCore_Roulette
{
    public class RouletteConfig : BasePluginConfig
    {
        [JsonPropertyName("Prefix")]
        public string Prefix { get; set; } = "{blue}⌈ Roulette ⌋";

        [JsonPropertyName("AccounceEveryone")]
        public bool AccounceEveryone { get; set; } = true;

        [JsonPropertyName("MinimumBet")]
        public int MinimumBet { get; set; } = 100;

        [JsonPropertyName("MaximumBet")]
        public int MaximumBet { get; set; } = 5000;

        [JsonPropertyName("CommandsForRoulette")]
        public string[] CommandsForRoulette { get; set; } = ["roulette"];

        [JsonPropertyName("Red")]
        public Dictionary<string, int> Red { get; set; } = new() { { "multiplier", 2 }, { "chance", 49 } };

        [JsonPropertyName("Blue")]
        public Dictionary<string, int> Blue { get; set; } = new() { { "multiplier", 2 }, { "chance", 49 } };

        [JsonPropertyName("Green")]
        public Dictionary<string, int> Green { get; set; } = new() { { "multiplier", 14 }, { "chance", 2 } };
    }
}
