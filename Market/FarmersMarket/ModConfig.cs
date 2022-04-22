using System.Collections.Generic;
using StardewModdingAPI;

namespace FarmersMarket
{
    internal class ModConfig
    {
        public bool AutoStockAtStartOfDay { get; set; } = true;
        public int RestockItemsPerHour { get; set; } = 5;
        public float PlayerStallVisitChance { get; set; } = 0.9f;
        public float NPCStallVisitChance { get; set; } = 0.9f;
        public bool ExtraDebugOutput { get; set; }
        public bool PeekIntoChests { get; set; }
        public bool RuinTheFurniture { get; set; }
        public bool HideFurniture { get; set; } = true;
        public int DayOfMonth { get; set; } = 6;

        public int OpeningTime { get; set; } = 800;

        public int ClosingTime { get; set; } = 1800;

    }
}
