using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewModdingAPI;
using System.Collections.Generic;
using BetterJunimos.Abilities;
using StardewValley.Buildings;
using SObject = StardewValley.Object;

namespace BetterJunimosForestry.Abilities {
    public class PlantTreesAbility : IJunimoAbility {
        private readonly IMonitor Monitor;

        internal PlantTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
        }

        public string AbilityName() {
            return "PlantTrees";
        }

        public bool IsActionAvailable(GameLocation location, Vector2 pos, Guid guid) {
            var mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            if (mode != Modes.Forest) return false;

            var hut = Util.GetHutFromId(guid);
            
            var (x, y) = pos;
            var up = new Vector2(x, y + 1);
            var right = new Vector2(x + 1, y);
            var down = new Vector2(x, y - 1);
            var left = new Vector2(x - 1, y);

            Vector2[] positions = { up, right, down, left };
            foreach (var nextPos in positions) {
                if (!Util.IsWithinRadius(location, hut, nextPos)) continue;
                if (ShouldPlantWildTreeHere(location, hut, nextPos)) return true;
            }
            return false;
        }

        public bool PerformAction(GameLocation location, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            var hut = Util.GetHutFromId(guid);
            var chest = hut.output.Value;
            var foundItem = chest.items.FirstOrDefault(item => item != null && Util.WildTreeSeeds.Keys.Contains(item.ParentSheetIndex));
            if (foundItem == null) return false;

            var (x, y) = pos;
            var up = new Vector2(x, y + 1);
            var right = new Vector2(x + 1, y);
            var down = new Vector2(x, y - 1);
            var left = new Vector2(x - 1, y);

            Vector2[] positions = { up, right, down, left };
            foreach (var nextPos in positions) {
                if (!Util.IsWithinRadius(location, hut, nextPos)) continue;
                if (!ShouldPlantWildTreeHere(location, hut, nextPos)) continue;
                if (!Plant(location, nextPos, foundItem.ParentSheetIndex)) continue;
                Util.RemoveItemFromChest(chest, foundItem);
                return true;
            }
            return false;
        }

        // is this tile plantable? 
        internal bool ShouldPlantWildTreeHere(GameLocation farm, JunimoHut hut, Vector2 pos) {
            if (Util.BlocksDoor(hut, pos)) return false;

            // Monitor.Log($"    ShouldPlantWildTreeHere: {pos.X} {pos.Y} pattern {ModEntry.Config.WildTreePattern} in pattern {IsTileInPattern(pos)} plantable {Plantable(location, pos)}", LogLevel.Debug);
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
            //         if (!Plantable(location, v)) {
            //             Monitor.Log($"ShouldPlantWildTreeHere: {pos.X} {pos.Y} is not plantable so not planting here", LogLevel.Debug);
            //             return false;
            //         }
            //     }
            // }

            // Monitor.Log($"        ShouldPlantWildTreeHere: yes, {pos.X} {pos.Y} plantable", LogLevel.Debug);
            return true;
        }

        internal static bool IsTileInPattern(Vector2 pos) {
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
        private static bool Plantable(GameLocation location, Vector2 pos) {
            if (location.isTileOccupied(pos)) return false;  // is something standing on it? an impassable building? a terrain feature?
            if (Util.IsHoed(location, pos)) return false;
            if (Util.IsOccupied(location, pos)) return false;
            if (Util.SpawningTreesForbidden(location, pos)) return false;
            if (!Util.CanBeHoed(location, pos)) return false;
            return true;
        }
        
        private static bool Plant(GameLocation location, Vector2 pos, int index) {
            if (location.terrainFeatures.Keys.Contains(pos)) {
                return false;
            }

            var tree = new Tree(Util.WildTreeSeeds[index], ModEntry.Config.PlantWildTreesSize);
            location.terrainFeatures.Add(pos, tree);

            if (Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, location)) {
                location.playSound("stoneStep");
                location.playSound("dirtyHit");
            }

            ++Game1.stats.SeedsSown;
            return true;
        }


        public List<int> RequiredItems() {
            return Util.WildTreeSeeds.Keys.ToList();
        }
        
        
        /* older API compat */
        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            return IsActionAvailable((GameLocation) farm, pos, guid);
        }
        
        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            return PerformAction((GameLocation) farm, pos, junimo, guid);
        }
    }
}