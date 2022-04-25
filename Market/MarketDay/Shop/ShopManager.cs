using MarketDay;
using MarketDay.ItemPriceAndStock;
using MarketDay.Utility;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using MarketDay.API;
using MarketDay.Data;

namespace MarketDay.Shop
{
    /// <summary>
    /// This class holds and manages all the shops, loading content packs to create shops
    /// And containing methods to update everything that needs to
    /// </summary>
    internal static class ShopManager
    {
        public static readonly Dictionary<string, GrangeShop> GrangeShops = new();
        public static readonly Dictionary<string, AnimalShop> AnimalShops = new();

        /// <summary>
        /// Takes content packs and loads them as ItemShop and AnimalShop objects
        /// </summary>
        public static void LoadContentPacks()
        {
            MarketDay.monitor.Log("Adding Content Packs...", LogLevel.Info);
            foreach (IContentPack contentPack in MarketDay.helper.ContentPacks.GetOwned())
            {
                if (!contentPack.HasFile("shops.json"))
                {
                    MarketDay.monitor.Log($"No shops.json found from the mod {contentPack.Manifest.UniqueID}. " +
                        $"Skipping pack.", LogLevel.Warn);
                    continue;
                }

                ContentPack data;
                try
                {
                    data = contentPack.ReadJsonFile<ContentPack>("shops.json");
                }
                catch (Exception ex)
                {
                    MarketDay.monitor.Log($"Invalid JSON provided by {contentPack.Manifest.UniqueID}.", LogLevel.Error);
                    MarketDay.monitor.Log(ex.Message + ex.StackTrace,LogLevel.Error);
                    continue;
                }

                MarketDay.monitor.Log($"Loading: {contentPack.Manifest.Name} by {contentPack.Manifest.Author} | " +
                    $"{contentPack.Manifest.Version} | {contentPack.Manifest.Description}", LogLevel.Info);

                RegisterShops(data, contentPack);
            }
        }

        /// <summary>
        /// Saves each shop as long as its has a unique name
        /// </summary>
        /// <param name="data"></param>
        /// <param name="contentPack"></param>
        public static void RegisterShops(ContentPack data, IContentPack contentPack)
        {
            if (data.GrangeShops != null)
            {
                foreach (var shopPack in data.GrangeShops)
                {
                    if (GrangeShops.ContainsKey(shopPack.ShopName))
                    {
                        MarketDay.monitor.Log($"{contentPack.Manifest.Name} is trying to add a Shop \"{shopPack.ShopName}\"," +
                            $" but a shop of this name has already been added. " +
                            $"It will not be added.", LogLevel.Warn);
                        continue;
                    }

                    shopPack.ContentPack = contentPack;
                    MarketDay.monitor.Log($"{contentPack.Manifest.Name} is adding \"{shopPack.ShopName}\"",
                        LogLevel.Debug);
                    GrangeShops.Add(shopPack.ShopName, shopPack);
                }
            }

            if (data.AnimalShops != null)
            {
                foreach (AnimalShop animalShopPack in data.AnimalShops)
                {
                    if (AnimalShops.ContainsKey(animalShopPack.ShopName))
                    {
                        MarketDay.monitor.Log($"{contentPack.Manifest.Name} is trying to add an AnimalShop \"{animalShopPack.ShopName}\"," +
                            $" but a shop of this name has already been added. " +
                            $"It will not be added.", LogLevel.Warn);
                        continue;
                    }
                    AnimalShops.Add(animalShopPack.ShopName, animalShopPack);
                }
            }
        }

        /// <summary>
        /// Update all translations for each shop when a save file is loaded
        /// </summary>
        public static void UpdateTranslations()
        {
            foreach (ItemShop itemShop in GrangeShops.Values)
            {
                itemShop.UpdateTranslations();
            }

            foreach (AnimalShop animalShop in AnimalShops.Values)
            {
                animalShop.UpdateTranslations();
            }
        }

        /// <summary>
        /// Initializes all shops once the game is loaded
        /// </summary>
        public static void InitializeShops()
        {
            foreach (var itemShop in GrangeShops.Values)
            {
                itemShop.Initialize();
            }
        }

        /// <summary>
        /// Initializes the stocks of each shop after the save file has loaded so that item IDs are available to generate items
        /// </summary>
        public static void InitializeItemStocks()
        {
            foreach (GrangeShop itemShop in GrangeShops.Values)
            {
                if (itemShop.StockManager is null)
                {
                    MarketDay.Log($"InitializeItemStocks: StockManager for {itemShop.ShopName} is null", LogLevel.Warn);
                    return;
                }
                itemShop.StockManager.Initialize();
            }
        }

        /// <summary>
        /// Updates the stock for all itemshops at the start of each day
        /// and updates their portraits too to match the current season
        /// </summary>
        internal static void UpdateStock()
        {
            if (GrangeShops.Count > 0)
                MarketDay.monitor.Log($"Refreshing stock for all custom shops...", LogLevel.Debug);

            foreach (GrangeShop store in GrangeShops.Values)
            {
                store.UpdateItemPriceAndStock();
                store.UpdatePortrait();
            }
        }

    }
}
