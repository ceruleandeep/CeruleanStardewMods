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

                    var item = ItemsUtil.GetIndexByName(name);
                    if (item == -1) MarketDay.Log($"    Could not get index for object: {name}", LogLevel.Warn);
                    
                    if (name is "Wine" or "Jelly" or "Juice" or "Pickle" or "Roe" or "Aged Roe")
                    {
                        var preservedGoods = prizeLevel.Flavor;
                        var item1 = ItemsUtil.GetIndexByName(preservedGoods);
                        if (item1 == -1) MarketDay.Log($"    Could not get index for flavor: {preservedGoods}", LogLevel.Warn);
                    }
                }
            }
        }
    }
}