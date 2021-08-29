using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using SObject = StardewValley.Object;

namespace BetterJunimosForestry {
    internal class FakeFarmer : Farmer
    {
        public override Vector2 GetToolLocation(bool ignoreClick = false)
        {
            return new Vector2(0, 0);
        }
    }
    
    public class Util {
        internal static ModConfig Config;
        
        internal static Dictionary<int, int> WildTreeSeeds = new Dictionary<int, int>
        {
            {292, 8}, // mahogany
            {309, 1}, // acorn
            {310, 2}, // maple
            {311, 3}, // pine
            {891, 7}  // mushroom
        };
        
        internal static Dictionary<int, string> FruitTreeSeeds = new Dictionary<int, string>
        {
            {69, "Banana"},
            {835, "Mango"},
            {628, ""},
            {629, ""},
            {630, ""},
            {631, ""},
            {632, ""},
            {633, ""}
        };
        
        /// <summary>Get whether a tile is blocked due to something it contains.</summary>
        /// <param name="location">The current location.</param>
        /// <param name="tile">The tile to check.</param>
        /// <remarks>Derived from <see cref="StardewValley.Tools.Hoe.DoFunction"/>.</remarks>
        public static bool IsOccupied(GameLocation location, Vector2 tile)
        {
            // impassable tiles (e.g. water)
            if (!location.isTilePassable(new Location((int)tile.X, (int)tile.Y), Game1.viewport))
                return true;

            // objects & large terrain features
            if (location.objects.ContainsKey(tile) || location.largeTerrainFeatures.Any(p => p.tilePosition.Value == tile))
                return true;

            // non-dirt terrain features
            if (location.terrainFeatures.TryGetValue(tile, out TerrainFeature feature))
            {
                HoeDirt dirt = feature as HoeDirt;
                if (dirt is not {crop: null})
                    return true;
            }

            // buildings
            if (location is BuildableGameLocation buildableLocation)
            {
                if (buildableLocation.buildings.Any(building => building.occupiesTile(tile)))
                    return true;
            }

            // buildings from the map
            if (location.getTileIndexAt(Utility.Vector2ToPoint(tile), "Buildings") > -1) return true;
            
            // furniture
            if (location.GetFurnitureAt(tile) != null) return true;
            
            return false;
        }
        
        internal static bool CanBeHoed(GameLocation location, Vector2 tileLocation)
        {
            int XCoord = (int)tileLocation.X;
            int YCoord = (int)tileLocation.Y;

            if (location.terrainFeatures.ContainsKey(tileLocation))
            {
                if (location.terrainFeatures[tileLocation] is HoeDirt hoeDirt)
                {
                    if(hoeDirt.crop != null)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (location.objects.ContainsKey(tileLocation))
            {
                return false;
            }
            if(location.doesTileHaveProperty(XCoord, YCoord, "Diggable", "Back") == null)
            {
                return false;
            }

            if (!location.isTilePassable(new Location(XCoord, YCoord), Game1.viewport))
            {
                return false;
            }

            foreach (ResourceClump resourceClump in location.resourceClumps)
            {
                if (resourceClump.occupiesTile((int)tileLocation.X, (int)tileLocation.Y))
                {
                    return false;
                }
            }
            Rectangle tileLocationRect = new Rectangle((int)tileLocation.X * 64 + 1, (int)tileLocation.Y * 64 + 1, 62, 62);
            foreach (LargeTerrainFeature largeTerrainFeature in location.largeTerrainFeatures)
            {
                if (largeTerrainFeature.getBoundingBox().Intersects(tileLocationRect))
                {
                    return false;
                }
            }

            Furniture f = location.GetFurnitureAt(tileLocation);
            if (f != null)
            {
                return false;
            }

            if (location is Farm farm)
            {
                foreach(var building in farm.buildings)
                {
                    if (building.occupiesTile(tileLocation)){
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool IsHoed(Farm farm, Vector2 pos) {
            if (farm.terrainFeatures.TryGetValue(pos, out TerrainFeature feature))
            {
                return feature is HoeDirt;
            }

            return false;
        }
        
        public static bool SpawningTreesForbidden(Farm farm, Vector2 pos) {
            string noSpawn = farm.doesTileHaveProperty((int)pos.X, (int)pos.Y, "NoSpawn", "Back");
            bool cantSpawnHere = noSpawn != null && (noSpawn.Equals("Tree") || noSpawn.Equals("All") || noSpawn.Equals("True"));
            if (cantSpawnHere) return true;
            return false;
        }
        
        public static Guid? GuidOfHutOnTile(Vector2 pos) {
            foreach (Building b in Game1.getFarm().buildings) {
                if (b is JunimoHut hut && b.occupiesTile(pos)) {
                    return GetHutIdFromHut(hut);
                }
            }
            return null;
        }

        public static JunimoHut HutOnTile(Vector2 pos) {
            foreach (Building b in Game1.getFarm().buildings) {
                if (b is JunimoHut hut && b.occupiesTile(pos)) {
                    return hut;
                }
            }
            return null;
        }

        public static void DrawScroll(SpriteBatch b, Vector2 position, int scroll_width) {
            float alpha = 1f;
            float layerDepth = 0.88f;
            b.Draw(Game1.mouseCursors, position + new Vector2(-12f, -3f) * 4f, new Rectangle(325, 318, 12, 18), Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, layerDepth - 0.001f);
            b.Draw(Game1.mouseCursors, position + new Vector2(0f, -3f) * 4f, new Rectangle(337, 318, 1, 18), Color.White * alpha, 0f, Vector2.Zero, new Vector2(scroll_width, 4f), SpriteEffects.None, layerDepth - 0.001f);
            b.Draw(Game1.mouseCursors, position + new Vector2(scroll_width, -12f), new Rectangle(338, 318, 12, 18), Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, layerDepth - 0.001f);
        }
        
        public static Guid GetHutIdFromHut(JunimoHut hut) {
            return Game1.getFarm().buildings.GuidOf(hut);
        }

        public static Vector2 GetPosFromHut(JunimoHut hut) {
            return new (hut.tileX.Value, hut.tileY.Value);
        }
        
        public static string GetModeForHut(JunimoHut hut) {
            if (hut is null) return Modes.Normal;
            
            if (ModEntry.HutStates.TryGetValue(GetPosFromHut(hut), out HutState state)) {
                return state.Mode;
            }
            return Modes.Normal;
        }
        
        public static JunimoHut GetHutFromId(Guid id) {
            Farm farm = Game1.getFarm();
            try {
                return farm.buildings[id] as JunimoHut;
            }
            catch (Exception ex) {
                ModEntry.SMonitor.Log($"GetHutFromId: exception while getting position of {id}", LogLevel.Error);
                ModEntry.SMonitor.Log($"{ex.ToString()}", LogLevel.Error);
                return null;
            }
        }

        public static Vector2 GetHutPositionFromId(Guid id) {
            Farm farm = Game1.getFarm();
            try {
                JunimoHut hut = farm.buildings[id] as JunimoHut;
                return new Vector2(hut.tileX.Value, hut.tileY.Value);
            }
            catch {
                ModEntry.SMonitor.Log($"GetHutPositionFromId: exception while getting position of {id}", LogLevel.Error);
                return new Vector2(0, 0);
            }
        }

        public static Vector2 GetHutPositionFromHut(JunimoHut hut) {
            return new Vector2(hut.tileX.Value, hut.tileY.Value);
        }

        public static JunimoHut GetHutFromPosition(Vector2 pos) {
            Farm farm = Game1.getFarm();
            foreach (Building b in farm.buildings) {
                if (b.occupiesTile(pos) && b is JunimoHut hut) {
                    return hut;
                }
            }

            return null;
        }
        
        public static void AddItemToChest(Farm farm, Chest chest, SObject item) {
            Item obj = chest.addItem(item);
            if (obj == null) return;
            Vector2 pos = chest.TileLocation;
            for (int index = 0; index < obj.Stack; ++index)
                Game1.createObjectDebris(item.ParentSheetIndex, (int)pos.X + 1, (int)pos.Y + 1, -1, item.Quality, 1f, farm);
        }

        public static void RemoveItemFromChest(Chest chest, Item item) {
            if (Config.InfiniteJunimoInventory) { return; }
            item.Stack--;
            if (item.Stack == 0) {
                chest.items.Remove(item);
            }
        }
        
        public static int MaxRadius() {
            return ModEntry.BJApi.GetJunimoHutMaxRadius();
        }

        public static bool IsWithinRadius(JunimoHut hut, Vector2 pos) {
            int radius = MaxRadius();
            bool outcome = true;
            if (pos.X < hut.tileX.Value + 1 - radius || pos.X >= hut.tileX.Value + 2 + radius) outcome = false;
            if (pos.Y < hut.tileY.Value + 1 - radius || pos.Y >= hut.tileY.Value + 2 + radius) outcome = false;
            // ModEntry.SMonitor.Log($"IsWithinRadius: hut [{hut.tileX.Value} {hut.tileY.Value}], pos [{pos.X} {pos.Y}], radius {radius}: {outcome}", LogLevel.Trace);
            return outcome;
        }
        
        public static bool BlocksDoor(JunimoHut hut, Vector2 pos) {
            bool blocks = (int)pos.X == hut.tileX.Value + 1 && (int)pos.Y == hut.tileY.Value + 2;
            // ModEntry.SMonitor.Log($"BlocksDoor: hut [{hut.tileX.Value} {hut.tileY.Value}], pos [{pos.X} {pos.Y}], blocks: {blocks}", LogLevel.Trace);
            return blocks;
        }
    }
}
