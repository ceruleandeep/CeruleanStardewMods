using System.Collections.Generic;
using MarketDay.Utility;
using StardewModdingAPI;

namespace MarketDay.Data
{
    public class PrizeLevel
    {
        public int Gold { get; set; }
        public int Score { get; set; }
        public string Object { get; set; }
        public int Quality { get; set; } = 0;
        public int Stack { get; set; } = 3;
    }
    
    public class ProgressionLevel
    {
        public string Name { get; set; }
        public int MarketSize { get; set; }
        public int UnlockAtEarnings { get; set; }
        public List<PrizeLevel> Prizes { get; set; }
    }
    
    public class ProgressionModel
    {
        public List<ProgressionLevel> Levels { get; set; }

        internal void CheckItems()
        {
            MarketDay.Log("Checking progression data", LogLevel.Debug);
            if (Levels.Count == 0) MarketDay.Log($"    No levels loaded", LogLevel.Error);
            foreach (var level in Levels)
            {
                foreach (var prizeLevel in level.Prizes)
                {
                    var name = prizeLevel.Object;
                    if (name.Contains("Jelly") || name.Contains("Wine") || name.Contains("Honey") || name.Contains("Juice"))
                    {
                        var bits = name.Split(" ");
                        var preservedGoods = string.Join(" ", bits[..^1]);
                        var preserveMethod = bits[^1];
                        
                        var item1 = ItemsUtil.GetIndexByName(preservedGoods);
                        if (item1 == -1) MarketDay.Log($"    Could not get index for {preservedGoods}", LogLevel.Warn);
                        // else MarketDay.Log($"    Level {level.Name} Prize {preservedGoods} idx {item1}", LogLevel.Debug);
                        
                        var item2 = ItemsUtil.GetIndexByName(preserveMethod);
                        if (item2 == -1) MarketDay.Log($"    Could not get index for {preserveMethod}", LogLevel.Warn);
                        // else MarketDay.Log($"    Level {level.Name} Prize {preserveMethod} idx {item2}", LogLevel.Debug);
                    }
                    else
                    {
                        var item = ItemsUtil.GetIndexByName(name);
                        if (item == -1) MarketDay.Log($"    Could not get index for {name}", LogLevel.Warn);
                        // else MarketDay.Log($"    Level {level.Name} Prize {name} idx {item}", LogLevel.Debug);
                    }
                }
            }
        }
    }
}