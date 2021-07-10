using System.Collections.Generic;
using StardewModdingAPI;

namespace JunimoDialog
{
    internal class ModConfig
    {
        public bool Happy { get; set; } = true;
        public bool Grumpy { get; set; } = true;
        public float DialogChance { get; set; } = 0.05f;
        public float JunimoTextChance { get; set; } = 0.50f;
        public bool ExtraDebugOutput { get; set; } = false;
    }
}
