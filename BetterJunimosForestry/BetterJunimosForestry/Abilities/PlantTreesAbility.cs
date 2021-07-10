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
    public class PlantTreesAbility : BetterJunimos.Abilities.IJunimoAbility {
        private readonly IMonitor Monitor;

        internal PlantTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
        }

        public string AbilityName() {
            return "PlantTrees";
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), pos)) continue;
                if (ShouldPlantWildTreeHere(farm, nextPos)) return true;
            }
            return false;

            //return farm.terrainFeatures.ContainsKey(pos) && farm.terrainFeatures[pos] is HoeDirt hd && hd.crop == null &&
            //    !farm.objects.ContainsKey(pos);
        }

        // is this tile plantable? 
        internal bool ShouldPlantWildTreeHere(Farm farm, Vector2 pos) {
            // Monitor.Log($"    ShouldPlantWildTreeHere: {pos.X} {pos.Y} pattern {ModEntry.Config.WildTreePattern} in pattern {IsTileInPattern(pos)} plantable {Plantable(farm, pos)}", LogLevel.Debug);
            // is this tile in the planting pattern?
            if (!IsTileInPattern(pos)) {
                // Monitor.Log($"        ShouldPlantWildTreeHere: no, {pos.X} {pos.Y} not in pattern", LogLevel.Debug);
                return false;
            }

            if (!Plantable(farm, pos)) {
                // Monitor.Log($"        ShouldPlantWildTreeHere: no, {pos.X} {pos.Y} not plantable", LogLevel.Debug);
                return false;
            }
            
            // would a tree here restrict passage?
            // if (ModEntry.Config.WildTreePattern == "tight" || ModEntry.Config.WildTreePattern == "impassable") return true;
            // for (int x = -1; x < 2; x++) {
            //     for (int y = -1; y < 2; y++) {
            //         Vector2 v = new Vector2(pos.X + x, pos.Y + y);
            //         if (!Plantable(farm, v)) {
            //             Monitor.Log($"ShouldPlantWildTreeHere: {pos.X} {pos.Y} is not plantable so not planting here", LogLevel.Debug);
            //             return false;
            //         }
            //     }
            // }

            // Monitor.Log($"        ShouldPlantWildTreeHere: yes, {pos.X} {pos.Y} plantable", LogLevel.Debug);
            return true;
        }

        internal static bool IsTileInPattern(Vector2 pos) {
            if (ModEntry.Config.WildTreePattern == "impassable") {
                return true;
            }

            if (ModEntry.Config.WildTreePattern == "tight") {
                return pos.X % 2 == 0;
            }

            if (ModEntry.Config.WildTreePattern == "loose") {
                return pos.X % 2 == 0 && pos.Y % 2 == 0;
            }

            if (ModEntry.Config.WildTreePattern == "fruity-tight") {
                return pos.X % 3 == 0 && pos.Y % 3 == 0;
            }

            if (ModEntry.Config.WildTreePattern == "fruity-loose") {
                if (pos.X % 4 == 2) return pos.Y % 2 == 0;
                if (pos.X % 4 == 0) return pos.Y % 2 == 0;
                return false;
            }

            throw new ArgumentOutOfRangeException($"Pattern '{ModEntry.Config.WildTreePattern}' not recognized");
        }

        // is this tile plantable?
        private bool Plantable(Farm farm, Vector2 pos) {
            if (farm.isTileOccupied(pos)) return false;
            if (Util.IsHoed(farm, pos)) return false;
            if (Util.IsOccupied(farm, pos)) return false;
            if (Util.SpawningTreesForbidden(farm, pos)) return false;
            if (!Util.CanBeHoed(farm, pos)) return false;
            return true;
        }
        
        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            Chest chest = Util.GetHutFromId(guid).output.Value;
            Item foundItem;
            foundItem = chest.items.FirstOrDefault(item => item != null && Util.WildTreeSeeds.Keys.Contains(item.ParentSheetIndex));
            if (foundItem == null) return false;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), pos)) continue;
                if (ShouldPlantWildTreeHere(farm, nextPos)) {
                    bool success = Plant(farm, nextPos, foundItem.ParentSheetIndex);
                    if (success) {
                        //Monitor.Log($"PerformAction planted {foundItem.Name} at {nextPos.X} {nextPos.Y}", LogLevel.Info);
                        Util.RemoveItemFromChest(chest, foundItem);
                        return true;
                    } else {
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

            Tree tree = new Tree(Util.WildTreeSeeds[index], ModEntry.Config.PlantWildTreesSize);
            farm.terrainFeatures.Add(pos, tree);

            if (Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, farm)) {
                farm.playSound("stoneStep");
                farm.playSound("dirtyHit");
            }

            ++Game1.stats.SeedsSown;
            return true;
        }


        public List<int> RequiredItems() {
            return Util.WildTreeSeeds.Keys.ToList<int>();
        }
    }
}