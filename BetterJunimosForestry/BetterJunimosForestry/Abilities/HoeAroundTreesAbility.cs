using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewValley.TerrainFeatures;
using StardewModdingAPI;
using StardewValley.Buildings;
using SObject = StardewValley.Object;

// bits of this are from Tractor Mod; https://github.com/Pathoschild/StardewMods/blob/68628a40f992288278b724984c0ade200e6e4296/TractorMod/Framework/BaseAttachment.cs#L132

namespace BetterJunimosForestry.Abilities {
    public class HoeAroundTreesAbility : BetterJunimos.Abilities.IJunimoAbility {

        private readonly IMonitor Monitor;

        internal HoeAroundTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
        }

        public string AbilityName() {
            return "HoeAroundTrees";
        }

        private bool IsMatureFruitTree(TerrainFeature tf) {
            return tf is FruitTree tree && tree.growthStage.Value >= 4;
        }

        private bool IsFruitTreeSapling(TerrainFeature tf) {
            new FruitTree();
            return tf is FruitTree tree && tree.growthStage.Value < 4;
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            // Monitor.Log($"HoeAroundTrees IsActionAvailable around [{pos.X} {pos.Y}] {mode} ({guid})", LogLevel.Debug);

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                bool avail = ShouldHoeThisTile(farm, nextPos, mode);
                bool inRadius = Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos);
                // Monitor.Log($" HoeAroundTrees IsActionAvailable around [{pos.X} {pos.Y}]: [{nextPos.X} {nextPos.Y}] should hoe: {avail} in radius: {inRadius}", LogLevel.Debug);

                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (ShouldHoeThisTile(farm, nextPos, mode)) {
                    return true;
                }
            }
            
            return false;
        }

        private bool ShouldHoeThisTile(Farm farm, Vector2 pos, string mode) {
            if (!CanHoeThisTile(farm, pos)) {
                // Monitor.Log($"    ShouldHoeThisTile [{pos.X} {pos.Y}] mode {mode}: cannot hoe tile", LogLevel.Debug);
                return false;
            }
            
            if (mode == Modes.Orchard) {
                // might we one day want to plant a fruit tree here?
                if (ModEntry.PlantFruitTrees.ShouldPlantFruitTreeOnTile(farm, pos)) return false;
                // might we one day want to plant a fruit tree adjacent?
                if (ModEntry.PlantFruitTrees.TileIsNextToAPlantableTile(farm, pos)) return false;
            }
            
            // might we one day want to plant a wild tree here?
            if (mode == Modes.Forest && ModEntry.PlantTrees.ShouldPlantWildTreeHere(farm, pos)) {
                Monitor.Log($"    ShouldHoeThisTile not hoeing [{pos.X} {pos.Y}] because wild tree plantable", LogLevel.Debug);
                return false;
            }
            
            // would planting out this tile stop a fruit tree from growing?
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    Vector2 v = new Vector2(pos.X + x, pos.Y + y);
                    if (farm.terrainFeatures.ContainsKey(v) && IsFruitTreeSapling(farm.terrainFeatures[v])) {
                        Monitor.Log($"    ShouldHoeThisTile not hoeing [{pos.X} {pos.Y}] because fruit tree adjacent", LogLevel.Debug);
                        return false;
                    }
                }
            }

            if (mode != Modes.Orchard) {
                // Monitor.Log($"    ShouldHoeThisTile: hoeing [{pos.X} {pos.Y}] because not orchard", LogLevel.Debug);
                return true;
            }
            
            // is this tile next to a grown tree, which is the only situation we'll hoe ground in an orchard?
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    Vector2 v = new Vector2(pos.X + x, pos.Y + y);
                    Monitor.Log($"    ShouldHoeThisTile: hoeing [{pos.X} {pos.Y}] because adjacent full grown orchard tree", LogLevel.Debug);
                    if (farm.terrainFeatures.ContainsKey(v) && IsMatureFruitTree(farm.terrainFeatures[v])) return true;
                }
            }
            Monitor.Log($"    ShouldHoeThisTile: not hoeing [{pos.X} {pos.Y}] because not adjacent full grown orchard tree", LogLevel.Debug);
            return false;
        }

        private static bool CanHoeThisTile(Farm farm, Vector2 pos)
        {
            // is this tile plain dirt?
            if (farm.isTileOccupied(pos)) return false;
            if (Util.IsOccupied(farm, pos)) return false;
            if (!Util.CanBeHoed(farm, pos)) return false;
            if (farm.doesTileHaveProperty((int) pos.X, (int) pos.Y, "Diggable", "Back") != null) return true;
            return false;
        }

        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            Monitor.Log($"HoeAroundTrees PerformAction [{pos.X} {pos.Y}]", LogLevel.Debug);
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            
            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            int direction = 0;
            Vector2[] positions = { up, right, down, left };
            foreach (var nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (!ShouldHoeThisTile(farm, nextPos, mode)) continue;
                junimo.faceDirection(direction);
                if (UseToolOnTileManual(nextPos, Game1.player, Game1.currentLocation)) return true;
                direction++;
            }
            return false;
            
        }

        protected bool UseToolOnTileManual(Vector2 tileLocation, Farmer player, GameLocation location) {
            location.makeHoeDirt(tileLocation);
            if (Utility.isOnScreen(Utility.Vector2ToPoint(tileLocation), 64, location)) {
                location.playSound("hoeHit");
            }
            Game1.removeSquareDebrisFromTile((int)tileLocation.X, (int)tileLocation.Y);
            location.checkForBuriedItem((int)tileLocation.X, (int)tileLocation.Y, explosion: false, detectOnly: false, player);
            return true;
        }
        
        public List<int> RequiredItems() {
            return new List<int>();
        }
    }
}