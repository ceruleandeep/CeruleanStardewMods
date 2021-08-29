using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewValley.TerrainFeatures;
using StardewModdingAPI;
using SObject = StardewValley.Object;

// bits of this are from Tractor Mod; https://github.com/Pathoschild/StardewMods/blob/68628a40f992288278b724984c0ade200e6e4296/TractorMod/Framework/BaseAttachment.cs#L132

namespace BetterJunimosForestry.Abilities {
    public class ChopTreesAbility : BetterJunimos.Abilities.IJunimoAbility {

        private readonly IMonitor Monitor;
        private Axe FakeAxe = new Axe();
        private FakeFarmer FakeFarmer = new FakeFarmer();
        
        internal ChopTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
            FakeAxe.UpgradeLevel = 1;
            FakeAxe.IsEfficient = true;
        }

        public string AbilityName() {
            return "ChopTrees";
        }

        protected bool IsHarvestableTree(TerrainFeature t, string mode) {
            if (t is not Tree tree) return false;
            if (tree.tapped.Value) return false;
            if (mode == Modes.Crops || mode == Modes.Orchard) return true;
            if (tree.growthStage.Value < 5) return false;
            if (ModEntry.Config.SustainableWildTreeHarvesting && !tree.hasSeed.Value) return false;
            return true;
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            if (mode == Modes.Normal) return false;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (farm.terrainFeatures.ContainsKey(nextPos) && IsHarvestableTree(farm.terrainFeatures[nextPos], mode)) {
                    // Monitor.Log($"Pos {nextPos} contains tree", LogLevel.Debug);
                    return true;
                }
            }
            return false;
        }

        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            int direction = 0;
            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (farm.terrainFeatures.ContainsKey(nextPos) && IsHarvestableTree(farm.terrainFeatures[nextPos], mode)) {
                    junimo.faceDirection(direction);
                    return UseToolOnTile(FakeAxe, nextPos, Game1.currentLocation);
                }
                direction++;
            }
            return false;
        }
        
        protected bool UseToolOnTile(Tool tool, Vector2 tile, GameLocation location) {
            Vector2 lc = GetToolPixelPosition(tile);
            tool.DoFunction(location, (int)lc.X, (int)lc.Y, 0, FakeFarmer);
            return true;
        }

        protected Vector2 GetToolPixelPosition(Vector2 tile) {
            return (tile * Game1.tileSize) + new Vector2(Game1.tileSize / 2f);
        }

        public List<int> RequiredItems() {
            return new List<int>();
        }
    }
}