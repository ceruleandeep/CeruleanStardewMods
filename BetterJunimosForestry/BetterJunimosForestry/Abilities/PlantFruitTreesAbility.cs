using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using StardewValley.Buildings;
using SObject = StardewValley.Object;

namespace BetterJunimosForestry.Abilities
{
    public class PlantFruitTreesAbility : BetterJunimos.Abilities.IJunimoAbility
    {
        private List<int> _RequiredItems;
        
        public string AbilityName()
        {
            return "PlantFruitTrees";
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid)
        {
            JunimoHut hut = Util.GetHutFromId(guid);

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = {up, right, down, left};
            foreach (var nextPos in positions)
            {
                if (!Util.IsWithinRadius(hut, pos)) continue;
                if (ShouldPlantFruitTreeOnTile(farm, hut, nextPos)) return true;
            }

            return false;
        }

        internal bool ShouldPlantFruitTreeOnTile(Farm farm, JunimoHut hut, Vector2 pos)
        {
            if (Util.BlocksDoor(hut, pos)) return false;
            return IsTileInPattern(pos) && CanPlantFruitTreeOnTile(farm, pos);
        }

        private static bool CanPlantFruitTreeOnTile(Farm farm, Vector2 pos)
        {
            if (FruitTree.IsGrowthBlocked(pos, farm)) return false;
            if (Util.IsOccupied(farm, pos)) return false;
            if (Util.IsHoed(farm, pos)) return false;
            return true;
        }

        internal bool TileIsNextToAPlantableTile(Farm farm, JunimoHut hut, Vector2 pos)
        {
            // why isn't IsGrowthBlocked enough?
            for (var x = -1; x < 2; x++)
            {
                for (var y = -1; y < 2; y++)
                {
                    var v = new Vector2(pos.X + x, pos.Y + y);
                    if (ShouldPlantFruitTreeOnTile(farm, hut, v))
                    {
                        // Monitor.Log($"TileIsNextToAPlantableTile [{pos.X}, {pos.Y}]: neighbour tile [{v.X}, {v.Y}] should be planted", LogLevel.Info);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTileInPattern(Vector2 pos)
        {
            if (ModEntry.Config.FruitTreePattern == "rows")
            {
                return pos.X % 3 == 0 && pos.Y % 3 == 0;
            }

            if (ModEntry.Config.FruitTreePattern == "diagonal")
            {
                if (pos.X % 4 == 2) return pos.Y % 4 == 2;
                if (pos.X % 4 == 0) return pos.Y % 4 == 0;
                return false;
            }

            if (ModEntry.Config.FruitTreePattern == "tight")
            {
                if (pos.Y % 2 == 0) return pos.X % 4 == 0;
                if (pos.Y % 2 == 1) return pos.X % 4 == 2;
                return false;
            }

            throw new ArgumentOutOfRangeException($"Pattern '{ModEntry.Config.FruitTreePattern}' not recognized");
        }

        private bool FruitTreePlantable(Farm farm, Vector2 pos)
        {
            var x = (int) pos.X;
            var y = (int) pos.Y;
            return (farm != null &&
                    (farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null ||
                     farm.doesTileHavePropertyNoNull(x, y, "Type", "Back").Equals("Grass") ||
                     farm.doesTileHavePropertyNoNull(x, y, "Type", "Back").Equals("Dirt")) &&
                    !farm.doesTileHavePropertyNoNull(x, y, "NoSpawn", "Back").Equals("Tree")) ||
                   (farm.CanPlantTreesHere(628, x, y) && (farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null ||
                                                          farm.doesTileHavePropertyNoNull(x, y, "Type", "Back")
                                                              .Equals("Stone")));
        }
        
        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid)
        {
            var hut = Util.GetHutFromId(guid);
            var chest = hut.output.Value;
            var foundItem = chest.items.FirstOrDefault(item =>
                item != null && RequiredItems().Contains(item.ParentSheetIndex));
            if (foundItem == null) return false;

            var up = new Vector2(pos.X, pos.Y + 1);
            var right = new Vector2(pos.X + 1, pos.Y);
            var down = new Vector2(pos.X, pos.Y - 1);
            var left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = {up, right, down, left};
            foreach (var nextPos in positions)
            {
                if (!Util.IsWithinRadius(hut, pos)) continue;
                if (!ShouldPlantFruitTreeOnTile(farm, hut, nextPos)) continue;
                if (!Plant(farm, nextPos, foundItem.ParentSheetIndex)) continue;
                Util.RemoveItemFromChest(chest, foundItem);
                return true;
            }

            return false;
        }

        private static bool Plant(Farm farm, Vector2 pos, int index)
        {
            if (farm.terrainFeatures.Keys.Contains(pos))
            {
                return false;
            }

            var tree = new FruitTree(index, ModEntry.Config.PlantFruitTreesSize);
            farm.terrainFeatures.Add(pos, tree);

            if (!Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, farm)) return true;
            farm.playSound("stoneStep");
            farm.playSound("dirtyHit");

            return true;
        }
        
        public List<int> RequiredItems()
        {
            // this is heavy, cache it
            if (_RequiredItems is not null) return _RequiredItems;
            var saplings = Game1.objectInformation.Where(pair => pair.Value.Split('/')[0].Contains("Sapling"));
            _RequiredItems = (from kvp in saplings select kvp.Key).ToList();
            return _RequiredItems;
        }
    }
}