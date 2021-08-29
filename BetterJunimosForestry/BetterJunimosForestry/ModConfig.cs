using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace BetterJunimosForestry {
    internal class ModConfig {
        public Dictionary<string, bool> JunimoAbilites { get; set; } = new Dictionary<string, bool>();
        internal string[] WildTreePatternChoices = new string[] { "tight", "loose", "fruity-tight", "fruity-loose" };
        internal string[] FruitTreePatternChoices = new string[] { "rows", "diagonal", "tight" };
        public string WildTreePattern { get; set; } = "loose";
        public string FruitTreePattern { get; set; } = "rows";
        public int PlantWildTreesSize { get; set; } = 0;
        public int PlantFruitTreesSize { get; set; } = 0;
        public bool SustainableWildTreeHarvesting { get; set; } = true;
        public bool InfiniteJunimoInventory { get; set; } = false;
        
        public bool HarvestGrassEnabled { get; set; } = true;

    }
}
