using System.Collections.Generic;
using StardewModdingAPI;

namespace MarketDay
{
    internal class ModConfig
    {
        public int RestockItemsPerHour { get; set; } = 3;
        public float StallVisitChance { get; set; } = 0.9f;
        public bool ReceiveMessages { get; set; } = true;
        public bool PeekIntoChests { get; set; }
        public bool RuinTheFurniture { get; set; }
        public int DayOfWeek { get; set; } = 6;
        public int OpeningTime { get; set; } = 8;
        public int ClosingTime { get; set; } = 18;
        
        public bool VerboseLogging { get; set; } = false;
        public bool NPCVisitors { get; set; } = true;
        public bool DebugKeybinds { get; set; } = false;
        public SButton OpenConfigKeybind { get; set; } = SButton.V;
        public SButton WarpKeybind { get; set; } = SButton.Z;
        public SButton ReloadKeybind { get; set; } = SButton.R;
        public SButton StatusKeybind { get; set; } = SButton.Q;
    }
}
