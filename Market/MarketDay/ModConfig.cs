using System.Collections.Generic;
using StardewModdingAPI;

namespace MarketDay
{
    internal class ModConfig
    {
        public bool AutoStockAtStartOfDay { get; set; } = true;
        public int RestockItemsPerHour { get; set; } = 3;
        public float StallVisitChance { get; set; } = 0.9f;
        public bool ReceiveMessages { get; set; } = true;
        public bool PeekIntoChests { get; set; }
        public bool RuinTheFurniture { get; set; }
        public bool HideFurniture { get; set; } = true;
        public int DayOfWeek { get; set; } = 6;
        public int OpeningTime { get; set; } = 8;
        public int ClosingTime { get; set; } = 18;
        public bool NPCVisitors { get; set; } = false;
        public bool DebugKeybinds { get; set; } = false;
        public SButton OpenConfigKeybind { get; set; } = SButton.R;
        public SButton WarpKeybind { get; set; } = SButton.G;
        public SButton ReloadKeybind { get; set; } = SButton.V;
        public SButton StatusKeybind { get; set; } = SButton.Z;
    }
}
