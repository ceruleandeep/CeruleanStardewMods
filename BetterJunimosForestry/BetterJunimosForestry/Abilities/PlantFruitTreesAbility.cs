using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewModdingAPI;
using System.Collections.Generic;
using SObject = StardewValley.Object;

namespace BetterJunimosForestry.Abilities {
    public class PlantFruitTreesAbility : BetterJunimos.Abilities.IJunimoAbility {
        private readonly IMonitor Monitor;

        internal PlantFruitTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
        }

        public string AbilityName() {
            return "PlantFruitTrees";
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), pos)) continue;
                if (ShouldPlantFruitTreeOnTile(farm, nextPos)) return true;
            }
            return false;
        }

        internal bool ShouldPlantFruitTreeOnTile(Farm farm, Vector2 pos) {
            return IsTileInPattern(pos) && CanPlantFruitTreeOnTile(farm, pos);
        }
        
        internal bool CanPlantFruitTreeOnTile(Farm farm, Vector2 pos) {
            if (FruitTree.IsGrowthBlocked(pos, farm)) return false;
            if (Util.IsOccupied(farm, pos)) return false;
            if (Util.IsHoed(farm, pos)) return false;
            return true;
        }

        internal bool TileIsNextToAPlantableTile(Farm farm, Vector2 pos) {
            // why isn't IsGrowthBlocked enough?
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    Vector2 v = new Vector2(pos.X + x, pos.Y + y);
                    if (ShouldPlantFruitTreeOnTile(farm, v)) {
                        // Monitor.Log($"TileIsNextToAPlantableTile [{pos.X}, {pos.Y}]: neighbour tile [{v.X}, {v.Y}] should be planted", LogLevel.Info);
                        return true;
                    }
                }
            }

            return false;
        }
        
        internal bool IsTileInPattern(Vector2 pos) {

            if (ModEntry.Config.FruitTreePattern == "rows") {
                return pos.X % 3 == 0 && pos.Y % 3 == 0;
            }

            if (ModEntry.Config.FruitTreePattern == "diagonal") {
                if (pos.X % 4 == 2) return pos.Y % 4 == 2;
                if (pos.X % 4 == 0) return pos.Y % 4 == 0;
                return false;
            }

            if (ModEntry.Config.FruitTreePattern == "tight") {
                if (pos.Y % 2 == 0) return pos.X % 4 == 0;
                if (pos.Y % 2 == 1) return pos.X % 4 == 2;
                return false;
            }

            throw new ArgumentOutOfRangeException($"Pattern '{ModEntry.Config.FruitTreePattern}' not recognized");
        }

        private bool FruitTreePlantable(Farm farm, Vector2 pos) {
            int x = (int)pos.X;
            int y = (int)pos.Y;
            return (farm is Farm && (farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null || farm.doesTileHavePropertyNoNull(x, y, "Type", "Back").Equals("Grass") || farm.doesTileHavePropertyNoNull(x, y, "Type", "Back").Equals("Dirt")) && !farm.doesTileHavePropertyNoNull(x, y, "NoSpawn", "Back").Equals("Tree")) || (farm.CanPlantTreesHere(628, x, y) && (farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null || farm.doesTileHavePropertyNoNull(x, y, "Type", "Back").Equals("Stone")));
        }



        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            Chest chest = Util.GetHutFromId(guid).output.Value;
            Item foundItem;
            foundItem = chest.items.FirstOrDefault(item => item != null && Util.FruitTreeSeeds.Keys.Contains(item.ParentSheetIndex));
            if (foundItem == null) return false;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), pos)) continue;
                if (ShouldPlantFruitTreeOnTile(farm, nextPos)) {
                    bool success = Plant(farm, nextPos, foundItem.ParentSheetIndex);
                    if (success) {
                        //Monitor.Log($"PerformAction planted {foundItem.Name} at {nextPos.X} {nextPos.Y}", LogLevel.Info);
                        Util.RemoveItemFromChest(chest, foundItem);
                        return true;
                    }
                    else {
                        Monitor.Log($"PerformAction could not plant {foundItem.Name} at {nextPos.X} {nextPos.Y}", LogLevel.Warn);
                    }
                }
            }
            return false;
        }

        private bool Plant(Farm farm, Vector2 pos, int index) {
            if (farm.terrainFeatures.Keys.Contains(pos)) {
                Monitor.Log($"Plant: {pos.X} {pos.Y} occupied by {farm.terrainFeatures[pos]}", LogLevel.Error);
                return false;
            }

            FruitTree tree = new FruitTree(index, ModEntry.Config.PlantFruitTreesSize);
            farm.terrainFeatures.Add(pos, tree);

            if (Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, farm)) {
                farm.playSound("stoneStep");
                farm.playSound("dirtyHit");
            }

            return true;
        }


        public List<int> RequiredItems() {
            return Util.FruitTreeSeeds.Keys.ToList<int>();
        }
    }
}