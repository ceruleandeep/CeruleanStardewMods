using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarmersMarket.Data
{
    public class StoreData
    {
        public string ShopName { get; set; }
        public string Quote { get; set; }
        public int SignObject { get; set; }
        public Color Color { get; set; }
        
        public double DefaultSellPriceMultiplier { get; set; } = 1.0;
        public Dictionary<string, int[]> ItemStocks { get; set; }
    }
}