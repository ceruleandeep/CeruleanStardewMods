using System.Collections.Generic;
using StardewValley;

namespace MarketDay.Utility
{
    public static class NPCUtility
    {
        internal static bool IsChild(NPC npc)
        {
            if (npc is StardewValley.Characters.Child) return true; //should get vanilla player-children
            var dispositions = Game1.content.Load<Dictionary<string, string>>("Data\\NPCDispositions");
            if (dispositions.ContainsKey(npc.Name))
            {
                return dispositions[npc.Name].Split('/')[0] == "child";
            }
            //this npc doesn't exist in dispositions? perhaps a child, or other mod-added NPC (e.g. a Moongate)
            return npc.Age == 2; //should get any remaining NPC children
        }
    }
}