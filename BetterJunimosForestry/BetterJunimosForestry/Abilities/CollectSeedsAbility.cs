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
    public class CollectSeedsAbility : BetterJunimos.Abilities.IJunimoAbility {

        private readonly IMonitor Monitor;
        private Axe FakeAxe = new Axe();

        internal CollectSeedsAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
            FakeAxe.UpgradeLevel = 1;
            FakeAxe.IsEfficient = true;
        }

        public string AbilityName() {
            return "CollectSeeds";
        }

        protected bool IsHarvestableSeed(TerrainFeature t, string mode) {
            if (t is not Tree tree) return false;
            if (tree.growthStage.Value != 0) return false;
            if (mode == Modes.Normal) return false;
            if (mode == Modes.Forest && PlantTreesAbility.IsTileInPattern(t.currentTileLocation)) return false;
            return true;
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            if (farm.terrainFeatures.ContainsKey(pos) && IsHarvestableSeed(farm.terrainFeatures[pos], mode)) {
                return true;
            }
            return false;
        }

        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            if (farm.terrainFeatures.ContainsKey(pos) && IsHarvestableSeed(farm.terrainFeatures[pos], mode)) {
                UseToolOnTile(FakeAxe, pos, Game1.player, Game1.currentLocation);
                return true;
            }
            return false;
        }

        protected bool UseToolOnTile(Tool tool, Vector2 tile, Farmer player, GameLocation location) {
            Vector2 lc = GetToolPixelPosition(tile);
            tool.DoFunction(location, (int)lc.X, (int)lc.Y, 0, player);
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