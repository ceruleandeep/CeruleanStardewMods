using MarketDay.API;
using MarketDay.Shop;
using StardewModdingAPI;

namespace MarketDay.Utility
{
    /// <summary>
    /// This class registers and handles console commands to SMAPI
    /// </summary>
    class ConsoleCommands
    {
        /// <summary>
        /// Registers all commands
        /// </summary>
        /// <param name="helper">the SMAPI helper</param>
        internal void Register(IModHelper helper)
        {
            helper.ConsoleCommands.Add("open_shop",
                "Opens up a custom shop's menu. \n\n" +
                "Usage: open_shop <ShopName>\n" +
                "-ShopName: the name of the shop to open",
                DisplayShopMenu);

            helper.ConsoleCommands.Add("open_animal_shop",
                "Opens up a custom animal shop's menu. \n\n" +
                "Usage: open_shop <open_animal_shop>\n" +
                "-ShopName: the name of the animal shop to open",
                DisplayAnimalShopMenus);
            helper.ConsoleCommands.Add("reset_shop",
                "Resets the stock of specified shop. Rechecks conditions and randomizations\n\n" +
                "Usage: reset_shop <ShopName>\n" +
                "-ShopName: the name of the shop to reset",
                ResetShopStock);

            helper.ConsoleCommands.Add("list_shops",
                "Lists all shops registered with Shop Tile Framework",
                ListAllShops);

            helper.ConsoleCommands.Add("STFConditions",
                "Will parse a single line of conditions and tell you if it is currently true or false\n\n" +
                "Usage: STFConditions <ConditionsString>\n" +
                "ConditionsString: A conditions string as would be written in the \"When\" field of the shops.json",
                ConditionCheck);
        }

        private void ConditionCheck(string arg1, string[] arg2)
        {
            string[] condition = { string.Join(" ",arg2)};
            MarketDay.monitor.Log($"Expression resolved as: {APIs.Conditions.CheckConditions(condition)}",LogLevel.Info);
        }

        /// <summary>
        /// Opens the item shop of the given name if able
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args">only checks the first argument for a shop name</param>
        private void DisplayShopMenu(string command, string[] args)
        {
            if (args.Length == 0)
            {
                MarketDay.monitor.Log($"A shop name is required", LogLevel.Debug);
                return;
            }

            ShopManager.GrangeShops.TryGetValue(args[0], out GrangeShop value);
            if (value == null)
            {
                MarketDay.monitor.Log($"No shop with a name of {args[0]} was found.", LogLevel.Debug);
            }
            else
            {
                if (!Context.IsPlayerFree)
                {
                    MarketDay.monitor.Log($"The player isn't free to act; can't display a menu right now", LogLevel.Debug);
                    return;
                }

                value.DisplayShop(true);
            }
        }

        /// <summary>
        /// Opens the animal shop of the given name if able
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args">only checks the first argument for a shop name</param>
        private void DisplayAnimalShopMenus(string command, string[] args)
        {
            if (args.Length == 0)
            {
                MarketDay.monitor.Log($"A shop name is required", LogLevel.Debug);
                return;
            }

            ShopManager.AnimalShops.TryGetValue(args[0], out AnimalShop value);
            if (value == null)
            {
                MarketDay.monitor.Log($"No shop with a name of {args[0]} was found.", LogLevel.Debug);
            }
            else
            {
                if (!Context.IsPlayerFree)
                {
                    MarketDay.monitor.Log($"The player isn't free to act; can't display a menu right now", LogLevel.Debug);
                    return;
                }

                value.DisplayShop(true);
            }
        }

        /// <summary>
        /// Resets the shop stock of the given shop name
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args">only checks the first argument for a shop name</param>
        private void ResetShopStock(string command, string[] args)
        {
            if (args.Length == 0)
            {
                MarketDay.monitor.Log($"A shop name is required", LogLevel.Debug);
                return;
            }

            ShopManager.GrangeShops.TryGetValue(args[0], out var shop);
            if (shop == null)
            {
                MarketDay.monitor.Log($"No shop with a name of {args[0]} was found.", LogLevel.Debug);
            }
            else
            {
                if (!Context.IsWorldReady)
                {
                    MarketDay.monitor.Log($"The world hasn't loaded; shop stock can't be updated at this time", LogLevel.Debug);
                    return;
                }
                shop.UpdateItemPriceAndStock();
            }
        }

        /// <summary>
        /// Prints a list of all registered shops
        /// </summary>
        private void ListAllShops(string command, string[] args)
        {
            if (ShopManager.GrangeShops.Count == 0)
            {
                MarketDay.monitor.Log($"No shops were found", LogLevel.Debug);
            }
            else
            {
                string temp = "";
                foreach (string k in ShopManager.GrangeShops.Keys)
                {
                    temp += "\nShop: " + k;
                }

                foreach (string k in ShopManager.AnimalShops.Keys)
                {
                    temp += "\nAnimalShop: " + k;
                }

                MarketDay.monitor.Log(temp, LogLevel.Debug);
            }
        }


    }
}
