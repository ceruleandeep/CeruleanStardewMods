using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace MarketDay.Utility
{
    public class MapUtility
    {
        /// <summary>
        /// Returns the tile property found at the given parameters
        /// </summary>
        /// <param name="map">an instance of the the map location</param>
        /// <param name="layer">the name of the layer</param>
        /// <param name="tile">the coordinates of the tile</param>
        /// <returns>The tile property if there is one, null if there isn't</returns>
        public static List<Vector2> ShopTiles()
        {
            List<Vector2> ShopLocations = new();
            var town = Game1.getLocationFromName("Town");
            if (town is null)
            {
                MarketDay.monitor.Log($"ShopTiles: Town location not available", LogLevel.Error);
                return ShopLocations;
            }

            var layerWidth = town.map.Layers[0].LayerWidth;
            var layerHeight = town.map.Layers[0].LayerHeight;

            // top left corner is z_MarketDay 253
            for (var x = 0; x < layerWidth; x++)
            {
                for (var y = 0; y < layerHeight; y++)
                {
                    var tileSheetIdAt = town.getTileSheetIDAt(x, y, "Buildings");
                    if (tileSheetIdAt != "z_MarketDay") continue;
                    var tileIndexAt = town.getTileIndexAt(x, y, "Buildings");
                    if (tileIndexAt != 253) continue;
                    
                    ShopLocations.Add(new Vector2(x, y));

                    MarketDay.monitor.Log($"ShopTiles:    {x} {y}: {tileSheetIdAt} {tileIndexAt}", LogLevel.Debug);
                }
            }

            return ShopLocations;
        }

        
    }
}