using System;
using System.Collections.Generic;
using System.Linq;
using BetterJunimos;
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
        private static readonly List<int> WildTreeSeeds = new List<int> {292, 309, 310, 311, 891};
        static Dictionary<string, Dictionary<int, bool>> cropSeasons = new Dictionary<string, Dictionary<int, bool>>();
        private const int SunflowerSeeds = 431;

        internal HoeAroundTreesAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
            var seasons = new List<string>{"spring", "summer", "fall", "winter"};
            foreach (string season in seasons) {
                cropSeasons[season] = new Dictionary<int, bool>();
            }
        }

        public string AbilityName() {
            return "HoeAroundTrees";
        }
        
        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid)
        {
            JunimoHut hut = Util.GetHutFromId(guid);
            string mode = Util.GetModeForHut(hut);
            if (mode == Modes.Normal) return false;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                // bool avail = ShouldHoeThisTile(farm, hut, nextPos, mode);
                // bool inRadius = Util.IsWithinRadius(hut, nextPos);
                // Monitor.Log($" HoeAroundTrees IsActionAvailable around [{pos.X} {pos.Y}]: [{nextPos.X} {nextPos.Y}] should hoe: {avail} in radius: {inRadius}", LogLevel.Debug);

                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (ShouldHoeThisTile(farm, hut, nextPos, mode)) {
                    return true;
                }
            }
            
            return false;
        }

        private bool ShouldHoeThisTile(Farm farm, JunimoHut hut, Vector2 pos, string mode) {
            if (!CanHoeThisTile(farm, pos)) {
                // Monitor.Log($"    ShouldHoeThisTile [{pos.X} {pos.Y}] mode {mode}: cannot hoe tile", LogLevel.Debug);
                return false;
            }
            
            if (mode == Modes.Orchard) {
                // might we one day want to plant a fruit tree here?
                if (ModEntry.PlantFruitTrees.ShouldPlantFruitTreeOnTile(farm, hut, pos)) return false;
                // might we one day want to plant a fruit tree adjacent?
                if (ModEntry.PlantFruitTrees.TileIsNextToAPlantableTile(farm, hut, pos)) return false;
            }
            
            // might we one day want to plant a wild tree here?
            if (mode == Modes.Forest && ModEntry.PlantTrees.ShouldPlantWildTreeHere(farm, hut, pos)) {
                // Monitor.Log($"    ShouldHoeThisTile not hoeing [{pos.X} {pos.Y}] because wild tree plantable", LogLevel.Debug);
                return false;
            }
            
            // would planting out this tile stop a fruit tree from growing?
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    Vector2 v = new Vector2(pos.X + x, pos.Y + y);
                    if (farm.terrainFeatures.ContainsKey(v) && IsFruitTreeSapling(farm.terrainFeatures[v])) {
                        // Monitor.Log($"    ShouldHoeThisTile not hoeing [{pos.X} {pos.Y}] because fruit tree adjacent", LogLevel.Debug);
                        return false;
                    }
                }
            }

            // is there something to plant here if we hoe it?
            if (!SeedsAvailableToPlantThisTile(hut, pos, Util.GetHutIdFromHut(hut)))
            {
                return false;
            } 
            
            if (mode != Modes.Orchard) {
                // Monitor.Log($"    ShouldHoeThisTile: hoeing [{pos.X} {pos.Y}] because not orchard", LogLevel.Debug);
                return true;
            }
            
            // is this tile next to a grown tree, which is the only situation we'll hoe ground in an orchard?
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    Vector2 v = new Vector2(pos.X + x, pos.Y + y);
                    // Monitor.Log($"    ShouldHoeThisTile: hoeing [{pos.X} {pos.Y}] because adjacent full grown orchard tree", LogLevel.Debug);
                    if (farm.terrainFeatures.ContainsKey(v) && IsMatureFruitTree(farm.terrainFeatures[v])) return true;
                }
            }
            // Monitor.Log($"    ShouldHoeThisTile: not hoeing [{pos.X} {pos.Y}] because not adjacent full grown orchard tree", LogLevel.Debug);
            return false;
        }

        private bool IsMatureFruitTree(TerrainFeature tf) {
            return tf is FruitTree tree && tree.growthStage.Value >= 4;
        }

        private bool IsFruitTreeSapling(TerrainFeature tf) {
            new FruitTree();
            return tf is FruitTree tree && tree.growthStage.Value < 4;
        }

        public bool SeedsAvailableToPlantThisTile(JunimoHut hut, Vector2 pos, Guid guid)
        {
            Item foundItem;
            
            // todo: this section is potentially slow and might be refined
            if (ModEntry.BJApi is null)
            {
                Monitor.Log($"SeedsAvailableToPlantThisTile: Better Junimos API not available", LogLevel.Error);
                return false;
            }
            Chest chest = hut.output.Value;
            if (ModEntry.BJApi.GetCropMapForHut(guid) is null)
            {
                foundItem = PlantableSeed(chest);
                return (foundItem is not null);
            }

            var cropType = ModEntry.BJApi.GetCropMapForHut(guid).CropTypeAt(hut, pos);
            foundItem = PlantableSeed(chest, cropType);
            return (foundItem is not null);
        }

        /// <summary>Get an item from the chest that is a crop seed, plantable in this season</summary>
        private Item PlantableSeed(Chest chest, string cropType=null) {
            List<Item> foundItems = chest.items.ToList().FindAll(item =>
                item is {Category: SObject.SeedsCategory} && !WildTreeSeeds.Contains(item.ParentSheetIndex)
            );

            if (cropType == CropTypes.Trellis) {
                foundItems = foundItems.FindAll(IsTrellisCrop);
            } else if (cropType == CropTypes.Ground) {
                foundItems = foundItems.FindAll(item => !IsTrellisCrop(item));
            }

            if (foundItems.Count == 0) return null;

            foreach (Item foundItem in foundItems) {
                // TODO: check if item can grow to harvest before end of season
                if (foundItem.ParentSheetIndex == SunflowerSeeds && Game1.IsFall && Game1.dayOfMonth >= 25) {
                    // there is no way that a sunflower planted on Fall 25 will grow to harvest
                    continue;
                }

                var key = foundItem.ParentSheetIndex;
                try {
                    if (cropSeasons[Game1.currentSeason][key]) {
                        return foundItem;
                    }
                } catch (KeyNotFoundException)
                {
                    Monitor.Log($"Cache miss: {foundItem.ParentSheetIndex} {Game1.currentSeason}", LogLevel.Debug);
                    var crop = new Crop(foundItem.ParentSheetIndex, 0, 0);
                    cropSeasons[Game1.currentSeason][key] = crop.seasonsToGrowIn.Contains(Game1.currentSeason);
                    if (cropSeasons[Game1.currentSeason][key]) {
                        return foundItem;
                    }
                }
                return foundItem;

                //
                // Crop crop = new Crop(foundItem.ParentSheetIndex, 0, 0);
                // if (crop.seasonsToGrowIn.Contains(Game1.currentSeason)) {
                //     return foundItem;
                // }
            }

            return null;
        }
        
        private bool IsTrellisCrop(Item item) {
            Crop crop = new Crop(item.ParentSheetIndex, 0, 0);
            return crop.raisedSeeds.Value;
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
            // Monitor.Log($"HoeAroundTrees PerformAction [{pos.X} {pos.Y}]", LogLevel.Debug);
            JunimoHut hut = Util.GetHutFromId(guid);
            string mode = Util.GetModeForHut(Util.GetHutFromId(guid));
            if (mode == Modes.Normal) return false;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            int direction = 0;
            Vector2[] positions = { up, right, down, left };
            foreach (var nextPos in positions) {
                if (!Util.IsWithinRadius(hut, nextPos)) continue;
                if (!ShouldHoeThisTile(farm, hut, nextPos, mode)) continue;
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