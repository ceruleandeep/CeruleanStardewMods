using System.Collections.Generic;
using System.Linq;
using MarketDay.Shop;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using xTile.ObjectModel;

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
                MarketDay.Log($"ShopTiles: Town location not available", LogLevel.Error);
                return ShopLocations;
            }

            var layerWidth = town.map.Layers[0].LayerWidth;
            var layerHeight = town.map.Layers[0].LayerHeight;

            // top left corner is z_MarketDay 253
            for (var x = 0; x < layerWidth; x++)
            {
                for (var y = 0; y < layerHeight; y++)
                {
                    var v = new Vector2(x, y);
                    var tileProperty = TileUtility.GetTileProperty(town, "Back", v);
                    if (tileProperty is null) continue;
                    // foreach (var p in tileProperty)
                    // {
                    //     MarketDay.Log($"    {v}: {p.Key} {p.Value}", LogLevel.Error);
                    // }
                    if (tileProperty.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}.GrangeShop", out var shopProperty))
                    {
                        ShopLocations.Add(v);
                    }
                }
            }

            // var locs = string.Join(", ", ShopLocations);
            // MarketDay.monitor.Log($"ShopTiles: {locs}", LogLevel.Debug);

            return ShopLocations;
        }

        internal static Dictionary<Vector2, GrangeShop> ShopAtTile()
        {
            var town = Game1.getLocationFromName("Town");
            var shopsAtTiles = new Dictionary<Vector2, GrangeShop>();

            foreach (var tile in ShopTiles())
            {
                // MarketDay.monitor.Log($"ShopAtTile: {tile}", LogLevel.Debug);

                var signTile = tile + new Vector2(3, 3);
                if (!town.objects.TryGetValue(signTile, out var obj) || obj is not Sign sign) continue;
                // MarketDay.monitor.Log($"    {signTile} is Sign", LogLevel.Debug);

                if (sign.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.ShopSignKey}", out var signOwner))
                {
                    // MarketDay.monitor.Log($"        signOwner {signOwner}", LogLevel.Debug);

                    shopsAtTiles[tile] = ShopManager.GrangeShops[signOwner];
                }
            }

            return shopsAtTiles;
        }

        public static GrangeShop ShopNearTile(Vector2 tile)
        {
            MarketDay.Log($"ShopNearTile {tile}", LogLevel.Debug, true);

            for (var x=1; x <= 3; x++)
            {
                for (var y=-2; y <= 1; y++)
                {
                    var search = tile + new Vector2(x, y);
                    MarketDay.Log($"    ShopNearTile {x} {y} {search}", LogLevel.Debug, true);
                    if (!Game1.currentLocation.objects.TryGetValue(search, out var chest) || chest is not Chest) continue;
                    chest.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.StockChestKey}", out var shopOwner);
                    MarketDay.Log($"    ShopNearTile {shopOwner}", LogLevel.Debug, true);
                    var shop = ShopManager.GrangeShops[shopOwner];
                    return shop;
                }
            }

            return null;
        }

        internal static string Owner(Item item)
        {
            item.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.GrangeChestKey}", out var grangeChestOwner);
            item.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.StockChestKey}", out var stockChestOwner);
            item.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.ShopSignKey}", out var signOwner);
            string owner = null;
            if (grangeChestOwner is not null) owner = grangeChestOwner;
            if (stockChestOwner is not null) owner = stockChestOwner;
            if (signOwner is not null) owner = signOwner;
            return owner;
        }
    }
}