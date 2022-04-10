using System.Collections.Generic;
using StardewModdingAPI;

namespace FarmersMarket
{
    internal class ModConfig
    {
        public bool StockGrangeAutomatically { get; set; } = true;
        public bool RestockAutomatically { get; set; } = true;
        public bool ExtraDebugOutput { get; set; }
    }
}
