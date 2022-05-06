using System;
using System.Collections.Generic;
using System.Linq;
using MarketDay.Utility;
using StardewModdingAPI;

namespace MarketDay.Data
{
    public class PrizeLevel
    {
        public string Name { get; set; }
        public int Gold { get; set; }
        public int Score { get; set; }
        public string Object { get; set; }
        public string Flavor { get; set; }
        public int Quality { get; set; } = 0;
        public int Stack { get; set; } = 3;
    }
    
    public class ProgressionLevel
    {
        public string Name { get; set; }
        public int MarketSize { get; set; }
        public int UnlockAtEarnings { get; set; }

        public int AutoRestock { get; set; } = 4;

        public int ShopSize { get; set; } = 9;

        public double PriceMultiplier { get; set; } = 1;
        
        public List<PrizeLevel> Prizes { get; set; }

        public PrizeLevel PrizeForEarnings(int gold)
        {
            var eligiblePrizes = Prizes.Where(p => p.Score == 0 && p.Gold <= gold).OrderBy(p => p.Gold);
            return eligiblePrizes.Any() ? eligiblePrizes.Last() : null;
        }
        
    }
    
    public class ProgressionModel
    {
        public List<ProgressionLevel> Levels { get; set; }

        internal ProgressionLevel CurrentLevel
        {
            get
            {
                ProgressionLevel highestUnlocked = null;
                var gold = MarketDay.GetSharedValue(MarketDay.TotalGoldKey);
                foreach (var level in Levels.Where(level => level.UnlockAtEarnings <= gold)) highestUnlocked = level;
                return highestUnlocked;
            }
        }

        internal int AutoRestock =>
            Math.Max(0, 
                MarketDay.Config.Progression 
                ? CurrentLevel.AutoRestock
                : MarketDay.Config.RestockItemsPerHour
                );

        internal int ShopSize =>
            Math.Max(1, Math.Min(9, 
                MarketDay.Config.Progression 
                ? CurrentLevel.ShopSize
                : 9
                ));

        internal double PriceMultiplier =>
            Math.Max(1, Math.Min(4, 
                MarketDay.Config.Progression 
                ? CurrentLevel.PriceMultiplier
                : 1
            ));

        internal int GoldTarget => CurrentLevel?.Prizes.Where(p => p.Score==0).OrderBy(p => p.Gold).First().Gold ?? 0;

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