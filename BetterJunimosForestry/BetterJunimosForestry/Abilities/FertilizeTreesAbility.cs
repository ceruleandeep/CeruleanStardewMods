using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace BetterJunimosForestry.Abilities {
    public class FertilizeTreesAbility : BetterJunimos.Abilities.IJunimoAbility {
        int tree_fertilizer = 805;

        public string AbilityName() {
            return "FertilizeTrees";
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            // in practice only seeds can be fertilized because the junimos can't get on top of saplings
            return farm.terrainFeatures.ContainsKey(pos) && farm.terrainFeatures[pos] is Tree t &&
                t.growthStage.Value < 5 && !farm.objects.ContainsKey(pos) && !t.fertilized.Value;
        }

        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            Chest chest = Util.GetHutFromId(guid).output.Value;
            Item foundItem = chest.items.FirstOrDefault(item => item != null && item.ParentSheetIndex == tree_fertilizer);
            if (foundItem == null) return false;

            if (farm.terrainFeatures[pos] is Tree t) {
                t.fertilize(farm);
                Util.RemoveItemFromChest(chest, foundItem);
                return true;
            }

            return false;
        }

        public List<int> RequiredItems() {
            return new() { tree_fertilizer };
        }
    }
}