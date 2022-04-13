using System;
using System.Collections.Generic;
using System.Linq;
using FarmersMarket.API;
using FarmersMarket.Data;
using FarmersMarket.ItemPriceAndStock;
using FarmersMarket.Shop;
using FarmersMarket.Utility;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using xTile.ObjectModel;
using SObject = StardewValley.Object;

namespace FarmersMarket
{
    public class SalesRecord
    {
        public Item item;
        public int price;
        public NPC npc;
        public double mult;
        public int timeOfDay;
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class FarmersMarket : Mod
    {
        internal const int PLAYER_STORE_X = 23;
        internal const int PLAYER_STORE_Y = 63;

        internal static MarketDataModel MarketData;
        
        // internal static List<Vector2> ShopLocations = new()
        // {
        //     new(28, 58),
        //     new(28, 63),
        //     new(33, 63),
        //     new(33, 58),
        //     new(33, 68),
        //     new(28, 68),
        // };

        internal static Dictionary<Vector2, GrangeShop> ShopAtTile = new(); 
        
        private static ContentPatcher.IContentPatcherAPI ContentPatcherAPI;
        // private static IManagedConditions MarketDayConditions;
        internal static ModConfig Config;

        internal static IModHelper helper;
        internal static IMonitor monitor;
        
        
        // ChroniclerCherry
        //The following variables are to help revert hardcoded warps done by the carpenter and
        //animal shop menus
        internal static GameLocation SourceLocation;
        private static Vector2 _playerPos = Vector2.Zero;
        internal static bool VerboseLogging = false;
        
        internal static Mod SMod;
        
        // internal static ContentPack StoresData;
        // internal static List<GrangeShop> Stores = new();
        
        private IGenericModConfigMenuApi configMenu;



        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="h">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper h)
        {
            helper = h;
            monitor = Monitor;
            SMod = this;

            Helper.Events.GameLoop.GameLaunched += OnLaunched;
            Helper.Events.GameLoop.GameLaunched += STF_OnLaunched;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            Helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
            // Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdateTicking;
            Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.DayEnding += OnDayEnding;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Input.ButtonPressed += STF_Input_ButtonPressed;

            ShopManager.LoadContentPacks();

            var PlayerShop = new GrangeShop()
            {
                ShopName = "Player",
                Quote = "Player store", 
                ItemStocks = new ItemStock[0]
            };
            ShopManager.GrangeShops.Add("Player", PlayerShop);

            var harmony = new Harmony("ceruleandeep.FarmersMarket");
            harmony.PatchAll();

            helper.ConsoleCommands.Add("fm_destroy", "Destroy furniture", DestroyAllFurniture);
        }

        /// <summary>Raised after the game is saved</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaving(object sender, SavingEventArgs e)
        {
            Helper.WriteConfig(Config);
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.
        ///
        /// For STF compat:
        /// On a save loaded, store the language for translation purposes. Done on save loaded in
        /// case it's changed between saves
        /// 
        /// Also retrieve all object informations. This is done on save loaded because that's
        /// when JA adds custom items
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaveLoaded(object sender, EventArgs e)
        {
            // reload the config to pick up any changes made in GMCM on the title screen
            Config = Helper.ReadConfig<ModConfig>();
            
            // some hooks for STF
            Translations.UpdateSelectedLanguage();
            ShopManager.UpdateTranslations();

            ItemsUtil.UpdateObjectInfoSource();
            ShopManager.InitializeItemStocks();

            ItemsUtil.RegisterItemsToRemove();
        }

        void OnDayStarted(object sender, EventArgs e)
        {
            monitor.Log($"OnDayStarted", LogLevel.Info);

            // note: we build the stores the night before to stay one step ahead of the route planner
            // i.e. BuildStores() called elsewhere;

            // STF 
            ShopManager.UpdateStock();

            foreach (var store in ShopAtTile.Values) store.OnDayStarted(IsMarketDay());
        }

        private static void BuildStores()
        {
            var availableShopNames = ShopManager.GrangeShops.Keys.ToList();
            availableShopNames.Remove("Player");
            StardewValley.Utility.Shuffle(Game1.random, availableShopNames);

            var strNames = string.Join(", ", availableShopNames);
            monitor.Log($"BuildStores: Adding stores ({MarketData.ShopLocations.Count} of {strNames})", LogLevel.Info);

            foreach (var ShopLocation in MarketData.ShopLocations)
            {
                if (availableShopNames.Count == 0) break;
                var ShopName = availableShopNames[0];
                availableShopNames.RemoveAt(0);

                ShopAtTile[ShopLocation] = ShopManager.GrangeShops[ShopName];
                ShopManager.GrangeShops[ShopName].SetOrigin(ShopLocation);
            }
            
            var PlayerShopLocation = new Vector2(PLAYER_STORE_X, PLAYER_STORE_Y);
            ShopAtTile[PlayerShopLocation] = ShopManager.GrangeShops["Player"];
            ShopManager.GrangeShops["Player"].SetOrigin(PlayerShopLocation);

            // ShopAtTile[ShopLocations[0]] = ShopManager.GrangeShops["Vincent"];
            // ShopAtTile[ShopLocations[1]] = ShopManager.GrangeShops["Alex"];

            // ShopManager.GrangeShops["Vincent"].SetOrigin((int)ShopLocations[0].X, (int)ShopLocations[0].Y);
            // ShopManager.GrangeShops["Alex"].SetOrigin((int)ShopLocations[1].X, (int)ShopLocations[1].Y);

            //
            // // Stores = new List<GrangeShop>();
            // // StardewValley.Utility.Shuffle(Game1.random, StoresData.Shops);
            //     
            // for (var j = 0; j < ShopLocations.Count; j++)
            // {
            //     // to open a store we need its store data
            //     // STF reads it from content packs in ModEntry (ShopManager.LoadContentPacks())
            //     // and calls ShopManager.RegisterShops(data, contentPack);
            //     // which adds it to ShopManager.GrangeShops
            //     //
            //     // well, we probably don't need to open it, STF should have done that
            //     // we just need to assign it to a position
            //     
            //     var storeData = StoresData.Shops[j];
            //     var (x, y) = StoresData.ShopLocations[j];
            //     var store = new GrangeShop(storeData.ShopName, storeData.Quote, false, (int) x, (int) y)
            //     {
            //         StoreColor = storeData.Color,
            //         SignObjectIndex = storeData.SignObject,
            //         PurchasePriceMultiplier = storeData.DefaultSellPriceMultiplier,
            //         Stock = storeData.ItemStocks
            //     };
            //
            //     monitor.Log($"Adding a store for {store.Name}", LogLevel.Debug);
            //     Stores.Add(store);
            // }
            //
            // Stores.Add(new GrangeShop("Player", null, true, PLAYER_STORE_X, PLAYER_STORE_Y));
        }

        public static List<GrangeShop> ActiveShops()
        {
            return ShopAtTile.Values.ToList();
        }

        void OnTimeChanged(object sender, EventArgs e)
        {
            if (Game1.timeOfDay % 100 > 0) return;
            foreach (var store in ShopAtTile.Values) store.OnTimeChanged();
        }

        void OnDayEnding(object sender, EventArgs e)
        {
            foreach (var store in ShopAtTile.Values) store.OnDayEnding();

            // get ready for tomorrow
            BuildStores();
        }

        private void DestroyAllFurniture(string command, string[] args)
        {
            monitor.Log($"DestroyAllFurniture", LogLevel.Debug);

            var toRemove = new Dictionary<Vector2, SObject>();

            var location = Game1.getLocationFromName("Town");
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out _))
                {
                    monitor.Log($"    GrangeStorage at {item.TileLocation}", LogLevel.Debug);
                    toRemove[tile] = item;
                }

                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out _))
                {
                    monitor.Log($"    GrangeSign at {item.TileLocation}", LogLevel.Debug);
                    toRemove[tile] = item;
                }
            }

            foreach (var (tile, item) in toRemove)
            {
                if (item is not Chest && item is not Sign) continue;
                monitor.Log($"    Removing {item.displayName} from {tile}", LogLevel.Debug);
                location.Objects.Remove(tile);
            }

            foreach (var store in ShopManager.GrangeShops.Values)
            {
                store.StorageChest = null;
                store.GrangeSign = null;
            }
        }

        // private void EnsureChestHidden()
        // {
        //     monitor.Log($"EnsureChestHidden: I have been instructed to move the furniture", LogLevel.Debug);
        //
        //     var location = Game1.getLocationFromName("Town");
        //     GetReferencesToFurniture();
        //
        //     monitor.Log($"EnsureChestHidden: StorageChest from {StorageChest.TileLocation} to {HiddenChestPosition}",
        //         LogLevel.Debug);
        //     location.setObject(HiddenChestPosition, StorageChest);
        //     location.objects.Remove(VisibleChestPosition);
        //     // location.moveObject((int)StorageChest.TileLocation.X, (int)StorageChest.TileLocation.Y, (int)HiddenChestPosition.X, (int)HiddenChestPosition.Y);
        //     StorageChest.modData[$"{ModManifest.UniqueID}/MovedYou"] = "yes";
        //
        //     monitor.Log($"EnsureChestHidden: GrangeSign from {GrangeSign.TileLocation} to {HiddenSignPosition}",
        //         LogLevel.Debug);
        //     location.moveObject((int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
        //         (int) HiddenSignPosition.X, (int) HiddenSignPosition.Y);
        //
        //     // location.setObject(HiddenSignPosition, GrangeSign);
        //     //
        //     // location.removeObject(VisibleChestPosition, false);
        //     // location.removeObject(VisibleSignPosition, false);
        //     //
        //
        //     // location.setObject(HiddenChestPosition, StorageChest);
        //     // location.objects[HiddenSignPosition] = GrangeSign;
        //     // location.setObject(VisibleChestPosition, null);
        //     // location.objects[VisibleSignPosition] = null;
        // }

        void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            if (!e.IsMultipleOf(10)) return;
            if (!Context.IsWorldReady) return;
            if (!IsMarketDay()) return;

            foreach (var store in ShopAtTile.Values)
            {
                store.OnUpdateTicking();
            }
        }

        private void OnOneSecondUpdateTicking(object sender, OneSecondUpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (!IsMarketDay()) return;

            foreach (var store in ShopAtTile.Values)
            {
                store.OnOneSecondUpdateTicking();
            }
        }
        

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.activeClickableMenu is not null) return;

            var (x, y) = e.Cursor.GrabTile;
            var (tx, ty) = e.Cursor.Tile;

            string chestOwner = null;
            string signOwner = null;

            if (Game1.currentLocation.objects.TryGetValue(new Vector2(x, y), out var objectAt))
            {
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out chestOwner);
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out signOwner);
                // monitor.Log($"OnButtonPressed GrabTile {x},{y} Obj: {objectAt.displayName} Chest: {chestOwner} Sign: {signOwner} Tool: {e.Button.IsUseToolButton()} Act: {e.Button.IsActionButton()}",
                //     LogLevel.Debug);
            }
            
            if (e.Button.IsUseToolButton() && objectAt is not null)
            {
                if (signOwner is not null) {
                    if (signOwner == "Player")
                    {
                        // clicked on player shop sign, show the summary
                        monitor.Log($"Suppress and show summary: clicked on player shop sign, show the summary", LogLevel.Debug);
                        Helper.Input.Suppress(e.Button);
                        ShopManager.GrangeShops[signOwner].ShowSummary();
                        return;
                    }
                    
                    // clicked on NPC shop sign, open the store
                    monitor.Log($"Suppress and show shop: clicked on NPC shop sign, open the store", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    ShopManager.GrangeShops[signOwner].DisplayShop(true);
                    return;
                }

                // stop player demolishing chests and signs
                if (chestOwner is not null && !Config.RuinTheFurniture)
                {
                    monitor.Log($"Suppress and shake: stop player demolishing chests and signs", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    objectAt.shakeTimer = 500;
                    return;
                }

            }

            if (e.Button.IsActionButton() && objectAt is not null)
            {
                // keep the player out of other people's chests
                if (chestOwner is not null && chestOwner != "Player" && !Config.PeekIntoChests)
                {
                    monitor.Log($"Suppress and shake: keep the player out of other people's chests", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    objectAt.shakeTimer = 500;
                    return;
                }

                // keep the player out of other people's signs
                if (signOwner is not null && signOwner != "Player")
                {
                    monitor.Log($"Suppress and show shop: keep the player out of other people's signs", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    ShopManager.GrangeShops[signOwner].DisplayShop(true);
                    return;
                }
            }
            
            if (e.Button.IsActionButton()) {
                // refer clicks on grange tiles to each store
                var tileIndexAt = Game1.currentLocation.getTileIndexAt((int) x, (int) y, "Buildings");
                if (tileIndexAt is < 349 or > 351) return;

                foreach (var store in ShopAtTile.Values)
                {
                    store.OnActionButton(e);
                }
            }
        }

        
        /// <summary>
        /// When input is received, check that the player is free and used an action button
        /// If so, attempt open the shop if it exists
        ///
        /// From STF/ChroniclerCherry
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void STF_Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            //context and button check
            if (!Context.CanPlayerMove)
                return;

            //Resets the boolean I use to check if a menu used to move the player around came from my mod
            //and lets me return them to their original location
            SourceLocation = null;
            _playerPos = Vector2.Zero;

            if (Constants.TargetPlatform == GamePlatform.Android)
            {
                if (e.Button != SButton.MouseLeft)
                    return;
                if (e.Cursor.GrabTile != e.Cursor.Tile)
                    return;

                if (VerboseLogging)
                    monitor.Log("Input detected!");
            }
            else if (!e.Button.IsActionButton())
                return;

            Vector2 clickedTile = Helper.Input.GetCursorPosition().GrabTile;

            //check if there is a tile property on Buildings layer
            IPropertyCollection tileProperty = TileUtility.GetTileProperty(Game1.currentLocation, "Buildings", clickedTile);

            if (tileProperty == null)
                return;

            //if there is a tile property, attempt to open shop if it exists
            CheckForShopToOpen(tileProperty,e);
        }

        /// <summary>
        /// Checks the tile property for shops, and open them
        /// </summary>
        /// <param name="tileProperty"></param>
        /// <param name="e"></param>
        private void CheckForShopToOpen(IPropertyCollection tileProperty, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            //check if there is a Shop property on clicked tile
            tileProperty.TryGetValue("Shop", out PropertyValue shopProperty);
            if (VerboseLogging)
                monitor.Log($"Shop Property value is: {shopProperty}");
            if (shopProperty != null) //There was a `Shop` property so attempt to open shop
            {
                //check if the property is for a vanilla shop, and gets the shopmenu for that shop if it exists
                IClickableMenu menu = TileUtility.CheckVanillaShop(shopProperty, out bool warpingShop);
                if (menu != null)
                {
                    if (warpingShop)
                    {
                        SourceLocation = Game1.currentLocation;
                        _playerPos = Game1.player.position.Get();
                    }

                    //stop the click action from going through after the menu has been opened
                    helper.Input.Suppress(e.Button);
                    Game1.activeClickableMenu = menu;

                }
                else //no vanilla shop found
                {
                    //Extract the tile property value
                    string shopName = shopProperty.ToString();

                    if (ShopManager.GrangeShops.ContainsKey(shopName))
                    {
                        //stop the click action from going through after the menu has been opened
                        helper.Input.Suppress(e.Button);
                        ShopManager.GrangeShops[shopName].DisplayShop();
                    }
                    else
                    {
                        Monitor.Log($"A Shop tile was clicked, but a shop by the name \"{shopName}\" " +
                            $"was not found.", LogLevel.Debug);
                    }
                }
            }
            else //no shop property found
            {
                tileProperty.TryGetValue("AnimalShop", out shopProperty); //see if there's an AnimalShop property
                if (shopProperty != null) //no animal shop found
                {
                    string shopName = shopProperty.ToString();
                    if (ShopManager.AnimalShops.ContainsKey(shopName))
                    {
                        //stop the click action from going through after the menu has been opened
                        helper.Input.Suppress(e.Button);
                        ShopManager.AnimalShops[shopName].DisplayShop();
                    }
                    else
                    {
                        Monitor.Log($"An Animal Shop tile was clicked, but a shop by the name \"{shopName}\" " +
                            $"was not found.", LogLevel.Debug);
                    }
                }

            } //end shopProperty null check
        }
        
        private void OnLaunched(object sender, GameLaunchedEventArgs e)
        {
            monitor.Log($"OnLaunched", LogLevel.Info);

            Config = Helper.ReadConfig<ModConfig>();
            setupGMCM();

            MarketData = Helper.Data.ReadJsonFile<MarketDataModel>("Assets/stores.json") ?? new MarketDataModel();

            // monitor.Log($"NPC stores:", LogLevel.Info);
            // foreach (var store in StoresData.GrangeShops)
            // {
            //     monitor.Log($"    GrangeShop {store.ShopName} symbol {store.SignObject} color {store.Color}",
            //         LogLevel.Debug);
            // }

            BuildStores();

            ContentPatcherAPI =
                Helper.ModRegistry.GetApi<ContentPatcher.IContentPatcherAPI>("Pathoschild.ContentPatcher");
            // ContentPatcherAPI.RegisterToken(ModManifest, "FarmersMarketOpen",
            //     () => { return Context.IsWorldReady ? new[] {IsMarketDayJustForToken() ? "true" : "false"} : null; });
        }

        /// <summary>
        /// On game launched initialize all the shops and register all external APIs
        ///
        /// From STF/ChroniclerCherry
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void STF_OnLaunched(object sender, GameLaunchedEventArgs e)
        {
            ShopManager.InitializeShops();

            APIs.RegisterJsonAssets();
            if (APIs.JsonAssets != null)
                APIs.JsonAssets.AddedItemsToShop += JsonAssets_AddedItemsToShop;

            APIs.RegisterExpandedPreconditionsUtility();
            APIs.RegisterBFAV();
            APIs.RegisterFAVR();
        }
        
        private void JsonAssets_AddedItemsToShop(object sender, System.EventArgs e)
        {
            if (Game1.activeClickableMenu is ShopMenu shop)
            {
                shop.setItemPriceAndStock(ItemsUtil.RemoveSpecifiedJAPacks(shop.itemPriceAndStock));
            }
        }

        private void setupGMCM() {
            configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Unregister(ModManifest);
            configMenu.Register(ModManifest, () => Config = new ModConfig(), SaveConfig);
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, false);

            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.main-settings"));

            configMenu.AddBoolOption(ModManifest,
                () => Config.AutoStockAtStartOfDay,
                val => Config.AutoStockAtStartOfDay = val,
                () => Helper.Translation.Get("cfg.auto-stock"),
                () => Helper.Translation.Get("cfg.auto-stock.msg")
            );
            
            configMenu.AddNumberOption(ModManifest,
                () => Config.RestockItemsPerHour,
                val => Config.RestockItemsPerHour = val,
                () => Helper.Translation.Get("cfg.restock-per-hour"),
                () => Helper.Translation.Get("cfg.restock-per-hour.msg"),
                0,
                9
            );
            
            configMenu.AddNumberOption(ModManifest,
                () => Config.PlayerStallVisitChance,
                val => Config.PlayerStallVisitChance = val,
                () => Helper.Translation.Get("cfg.player-stall-visit-chance"),
                () => Helper.Translation.Get("cfg.player-stall-visit-chance"),
                min: 0f,
                max: 1f
            );

            configMenu.AddNumberOption(ModManifest,
                () => Config.NPCStallVisitChance,
                val => Config.NPCStallVisitChance = val,
                () => Helper.Translation.Get("cfg.npc-stall-visit-chance"),
                () => Helper.Translation.Get("cfg.npc-stall-visit-chance"),
                min: 0f,
                max: 1f
            );

            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.debug-settings"));

            configMenu.AddBoolOption(ModManifest,
                () => Config.PeekIntoChests,
                val => Config.PeekIntoChests = val,
                () => Helper.Translation.Get("cfg.peek-into-chests"),
                () => Helper.Translation.Get("cfg.peek-into-chests.msg")
            );

            configMenu.AddBoolOption(ModManifest,
                () => Config.RuinTheFurniture,
                val => Config.RuinTheFurniture = val,
                () => Helper.Translation.Get("cfg.ruin-furniture"),
                () => Helper.Translation.Get("cfg.ruin-furniture.msg")
            );
            
            configMenu.AddBoolOption(ModManifest,
                () => Config.HideFurniture,
                val => Config.HideFurniture = val,
                () => Helper.Translation.Get("cfg.hide-furniture"),
                () => Helper.Translation.Get("cfg.hide-furniture.msg")
            );
        }

        private void SaveConfig() {
            Helper.WriteConfig(Config);
            Helper.Content.InvalidateCache(@"Data/Blueprints");
        }
        
        internal static bool IsMarketDay()
        {
            return Game1.dayOfMonth % 7 == 6 &&
                   !Game1.isRaining &&
                   !Game1.isSnowing &&
                   !StardewValley.Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason);
            //
            // if (MarketDayConditions is null || !MarketDayConditions.IsReady)
            // {
            //     monitor.Log($"IsMarketDay: MarketDayConditions null", LogLevel.Warn);
            //     return false;
            // }
            //
            // MarketDayConditions.UpdateContext();
            // return MarketDayConditions.IsMatch;
        }

        // private bool IsMarketDayJustForToken()
        // {
        //     if (MarketDayConditions is null)
        //     {
        //         monitor.Log($"IsMarketDayJustForToken: MarketDayConditions null", LogLevel.Warn);
        //         return false;
        //     }
        //
        //     if (!MarketDayConditions.IsReady)
        //     {
        //         monitor.Log($"IsMarketDayJustForToken: MarketDayConditions not ready", LogLevel.Warn);
        //         return false;
        //     }
        //
        //     MarketDayConditions.UpdateContext();
        //     monitor.Log(
        //         $"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} MarketDayConditions {MarketDayConditions.IsMatch}",
        //         LogLevel.Info);
        //
        //
        //     var rawConditions = new Dictionary<string, string>
        //     {
        //         ["DayOfWeek"] = "Saturday",
        //         ["Weather"] = "Sun, Wind",
        //         ["HasValue:{{DayEvent}}"] = "false"
        //     };
        //     var spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
        //         ModManifest,
        //         rawConditions,
        //         new SemanticVersion("1.20.0")
        //     );
        //     spareMarketDayConditions.UpdateContext();
        //     monitor.Log(
        //         $"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} spareMarketDayConditions {spareMarketDayConditions.IsMatch}",
        //         LogLevel.Info);
        //
        //
        //     rawConditions = new Dictionary<string, string>
        //     {
        //         ["DayOfWeek"] = "Saturday",
        //     };
        //     spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
        //         ModManifest,
        //         rawConditions,
        //         new SemanticVersion("1.20.0")
        //     );
        //     spareMarketDayConditions.UpdateContext();
        //     monitor.Log(
        //         $"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} DayOfWeek {spareMarketDayConditions.IsMatch}",
        //         LogLevel.Info);
        //
        //     rawConditions = new Dictionary<string, string>
        //     {
        //         ["Weather"] = "Sun, Wind",
        //     };
        //     spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
        //         ModManifest,
        //         rawConditions,
        //         new SemanticVersion("1.20.0")
        //     );
        //     spareMarketDayConditions.UpdateContext();
        //     monitor.Log(
        //         $"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} Weather {spareMarketDayConditions.IsMatch}",
        //         LogLevel.Info);
        //
        //     rawConditions = new Dictionary<string, string>
        //     {
        //         ["HasValue:{{DayEvent}}"] = "false"
        //     };
        //     spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
        //         ModManifest,
        //         rawConditions,
        //         new SemanticVersion("1.20.0")
        //     );
        //     spareMarketDayConditions.UpdateContext();
        //     monitor.Log(
        //         $"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} DayEvent {spareMarketDayConditions.IsMatch}",
        //         LogLevel.Info);
        //
        //
        //     return MarketDayConditions.IsMatch;
        // }

        private string Get(string key)
        {
            return Helper.Translation.Get(key);
        }

        private string Get(string key, object tokens)
        {
            return Helper.Translation.Get(key, tokens);
        }
    }
}