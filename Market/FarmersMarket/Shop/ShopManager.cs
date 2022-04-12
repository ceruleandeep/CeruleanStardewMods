using FarmersMarket;
using FarmersMarket.Data;
using FarmersMarket.ItemPriceAndStock;
using FarmersMarket.Utility;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmersMarket.Shop
{
    /// <summary>
    /// This class holds and manages all the shops, loading content packs to create shops
    /// And containing methods to update everything that needs to
    /// </summary>
    class ShopManager
    {
        public static Dictionary<string, GrangeShop> GrangeShops = new();
        public static Dictionary<string, AnimalShop> AnimalShops = new();

        /// <summary>
        /// Takes content packs and loads them as ItemShop and AnimalShop objects
        /// </summary>
        public static void LoadContentPacks()
        {
            FarmersMarket.monitor.Log("Adding Content Packs...", LogLevel.Info);
            foreach (IContentPack contentPack in FarmersMarket.helper.ContentPacks.GetOwned())
            {
                if (!contentPack.HasFile("shops.json"))
                {
                    FarmersMarket.monitor.Log($"No shops.json found from the mod {contentPack.Manifest.UniqueID}. " +
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
                    FarmersMarket.monitor.Log($"Invalid JSON provided by {contentPack.Manifest.UniqueID}.", LogLevel.Error);
                    FarmersMarket.monitor.Log(ex.Message + ex.StackTrace,LogLevel.Error);
                    continue;
                }

                FarmersMarket.monitor.Log($"Loading: {contentPack.Manifest.Name} by {contentPack.Manifest.Author} | " +
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
                    if (GrangeShops.ContainsKey(((ItemShopModel) shopPack).ShopName))
                    {
                        FarmersMarket.monitor.Log($"{contentPack.Manifest.Name} is trying to add a Shop \"{((ItemShopModel) shopPack).ShopName}\"," +
                            $" but a shop of this name has already been added. " +
                            $"It will not be added.", LogLevel.Warn);
                        continue;
                    }
                    shopPack.ContentPack = contentPack;
                    GrangeShops.Add(((ItemShopModel) shopPack).ShopName, shopPack);
                }
            }

            if (data.AnimalShops != null)
            {
                foreach (AnimalShop animalShopPack in data.AnimalShops)
                {
                    if (AnimalShops.ContainsKey(animalShopPack.ShopName))
                    {
                        FarmersMarket.monitor.Log($"{contentPack.Manifest.Name} is trying to add an AnimalShop \"{animalShopPack.ShopName}\"," +
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
            foreach (ItemShop itemShop in GrangeShops.Values)
            {
                itemShop.Initialize();
            }
        }

        /// <summary>
        /// Initializes the stocks of each shop after the save file has loaded so that item IDs are available to generate items
        /// </summary>
        public static void InitializeItemStocks()
        {
            foreach (ItemShop itemShop in GrangeShops.Values)
            {
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
                FarmersMarket.monitor.Log($"Refreshing stock for all custom shops...", LogLevel.Debug);

            foreach (ItemShop store in GrangeShops.Values)
            {
                store.UpdateItemPriceAndStock();
                store.UpdatePortrait();
            }
        }

    }
}
