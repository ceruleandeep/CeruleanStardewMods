using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MarketDay.API;
using MarketDay.Data;
using MarketDay.ItemPriceAndStock;
using MarketDay.Shop;
using MarketDay.Utility;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using xTile.ObjectModel;
using SObject = StardewValley.Object;

namespace MarketDay
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
    public class MarketDay : Mod
    {
        internal static bool MapChangesSynced;
        internal static List<Vector2> ShopLocations = new();
        internal static Dictionary<Vector2, GrangeShop> ShopAtTile = new(); 
        
        private static ContentPatcher.IContentPatcherAPI ContentPatcherAPI;
        // private static IManagedConditions MarketDayConditions;
        internal static ModConfig Config;

        internal static IModHelper helper;
        internal static IMonitor monitor;
        internal static Mod SMod;
        
        
        // ChroniclerCherry
        //The following variables are to help revert hardcoded warps done by the carpenter and
        //animal shop menus
        internal static GameLocation SourceLocation;
        private static Vector2 _playerPos = Vector2.Zero;
        internal static bool VerboseLogging = false;
        
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
            Helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdateTicking;
            Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.DayEnding += OnDayEnding;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.GameLoop.Saved += OnSaved;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Input.ButtonPressed += STF_Input_ButtonPressed;

            ShopManager.LoadContentPacks();
            MakePlayerShop();

            var harmony = new Harmony("ceruleandeep.MarketDay");
            harmony.PatchAll();

            helper.ConsoleCommands.Add("fm_destroy", "Destroy furniture", DestroyAllFurniture);
            helper.ConsoleCommands.Add("fm_reload", "Reload shop data", HotReload);
            helper.ConsoleCommands.Add("fm_tiles", "List shop tiles", ListShopTiles);
        }

        private static void MakePlayerShop()
        {
            var PlayerShop = new GrangeShop()
            {
                ShopName = "Player",
                Quote = "Player store",
                ItemStocks = new ItemStock[0]
            };
            ShopManager.GrangeShops.Add("Player", PlayerShop);
        }

        public void HotReload(string command, string[] args)
        {
            monitor.Log($"Reloading shop data", LogLevel.Info);

            monitor.Log($"    Closing stores", LogLevel.Debug);
            foreach (var store in ShopAtTile.Values)
            {
                store.OnDayEnding();
            }

            monitor.Log($"    Removing non-player stores", LogLevel.Debug);
            foreach (var (ShopName, shop) in ShopManager.GrangeShops)
            {
                if (!shop.IsPlayerShop()) ShopManager.GrangeShops.Remove(ShopName);
            }
            
            monitor.Log($"    Loading content packs", LogLevel.Debug);
            ShopManager.LoadContentPacks();
            ShopManager.InitializeShops();

            ItemsUtil.UpdateObjectInfoSource();
            ShopManager.InitializeItemStocks();

            ItemsUtil.RegisterItemsToRemove();
            
            monitor.Log($"    Updating stock", LogLevel.Debug);
            ShopManager.UpdateStock();

            monitor.Log($"    Opening stores", LogLevel.Debug);
            AssignShopsToGrangeLocations();
            foreach (var store in ShopAtTile.Values) store.SetupForNewDay(IsMarketDay());
        }

        public static void ListShopTiles(string command, string[] args)
        {
            monitor.Log($"ListShopTiles: {Game1.currentSeason} {Game1.dayOfMonth} {Game1.timeOfDay} {Game1.ticks}", LogLevel.Info);
            var town = Game1.getLocationFromName("Town");
            if (town is null)
            {
                monitor.Log($"    Town location not available", LogLevel.Error);
                return;
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

                    monitor.Log($"    {x} {y}: {tileSheetIdAt} {tileIndexAt}", LogLevel.Debug);
                }
            }
        }

        /// <summary>Raised after the game is saved</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaving(object sender, SavingEventArgs e)
        {
            monitor.Log($"OnSaving: {Game1.currentSeason} {Game1.dayOfMonth} {Game1.timeOfDay} {Game1.ticks}", LogLevel.Info);
            ListShopTiles("", null);
            Helper.WriteConfig(Config);
            monitor.Log($"OnSaving: complete", LogLevel.Debug);

        }

        void OnSaved(object sender, SavedEventArgs e)
        {
            monitor.Log($"OnSaved: {Game1.currentSeason} {Game1.dayOfMonth} {Game1.timeOfDay} {Game1.ticks}", LogLevel.Info);
            ListShopTiles("", null);
            monitor.Log($"OnSaved: complete", LogLevel.Debug);
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
            MapChangesSynced = false;

            // STF 
            ShopManager.UpdateStock();
            
            // send market day prompt
            if (IsMarketDay())
            {
                var openingTime = (Config.OpeningTime*100).ToString();
                openingTime = openingTime[..^2] + ":" + openingTime[^2..];
                
                var prompt = Get("market-day", new {openingTime});
                MessageUtility.SendMessage(prompt);
            }
        }

        private static void AssignShopsToGrangeLocations()
        {
            if (!MapChangesSynced) throw new Exception("Map changes not synced");
            
            var availableShopNames = new List<string>(); //(ShopManager.GrangeShops.Keys.ToList());

            foreach (var (shopName, shop) in ShopManager.GrangeShops)
            {
                if (shop.When is not null)
                {
                    if (!APIs.Conditions.CheckConditions(shop.When))
                    {
                        monitor.Log($"Shop {shopName}: opening condition not met", LogLevel.Warn);
                        continue;
                    }
                }
                availableShopNames.Add(shopName);
            }

            StardewValley.Utility.Shuffle(Game1.random, availableShopNames);
            StardewValley.Utility.Shuffle(Game1.random, ShopLocations);
            availableShopNames.Remove("Player");
            availableShopNames.Insert(0, "Player");

            var strNames = string.Join(", ", availableShopNames);
            monitor.Log($"BuildStores: Adding stores ({ShopLocations.Count} of {strNames})", LogLevel.Info);

            foreach (var ShopLocation in ShopLocations)
            {
                if (availableShopNames.Count == 0) break;
                var ShopName = availableShopNames[0];
                availableShopNames.RemoveAt(0);

                ShopAtTile[ShopLocation] = ShopManager.GrangeShops[ShopName];
                ShopManager.GrangeShops[ShopName].SetOrigin(ShopLocation);
            }
        }
        
        void OnTimeChanged(object sender, EventArgs e)
        {
            if (!MapChangesSynced)
            {
                ShopLocations = MapUtility.ShopTiles();
                RecalculateSchedules();
                
                MapChangesSynced = true;

                AssignShopsToGrangeLocations();
                foreach (var store in ShopAtTile.Values) store.SetupForNewDay(IsMarketDay());
            }
            
            if (Game1.timeOfDay % 100 > 0) return;
            foreach (var store in ShopAtTile.Values) store.OnTimeChanged(IsMarketDay());
        }

        void RecalculateSchedules()
        {
            foreach (NPC npc in StardewValley.Utility.getAllCharacters())
            {
                if (npc.isVillager())
                    npc.Schedule = npc.getSchedule(Game1.dayOfMonth);
            }
        }

        void OnDayEnding(object sender, EventArgs e)
        {
            monitor.Log($"OnDayEnding: {Game1.currentSeason} {Game1.dayOfMonth} {Game1.timeOfDay} {Game1.ticks}", LogLevel.Info);
            ListShopTiles("", null);
            foreach (var store in ShopAtTile.Values) store.OnDayEnding();
            
            ListShopTiles("", null);
            monitor.Log($"OnDayEnding: complete", LogLevel.Debug);
        }

        private void DestroyAllFurniture(string command, string[] args)
        {
            monitor.Log($"DestroyAllFurniture", LogLevel.Trace);

            var toRemove = new Dictionary<Vector2, SObject>();

            var location = Game1.getLocationFromName("Town");
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out _))
                {
                    monitor.Log($"    GrangeStorage at {item.TileLocation}", LogLevel.Trace);
                    toRemove[tile] = item;
                }

                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out _))
                {
                    monitor.Log($"    GrangeSign at {item.TileLocation}", LogLevel.Trace);
                    toRemove[tile] = item;
                }
            }

            foreach (var (tile, item) in toRemove)
            {
                if (item is not Chest && item is not Sign) continue;
                monitor.Log($"    Removing {item.displayName} from {tile}", LogLevel.Trace);
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

            if (Config.DebugKeybinds) CheckDebugKeybinds(e);
            
            string chestOwner = null;
            string signOwner = null;


            if (Game1.currentLocation.objects.TryGetValue(new Vector2(x, y), out var objectAt))
            {
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out chestOwner);
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out signOwner);
            }
            
            if (e.Button.IsUseToolButton() && objectAt is not null)
            {
                if (signOwner is not null) {
                    if (signOwner == "Player")
                    {
                        // clicked on player shop sign, show the summary
                        Helper.Input.Suppress(e.Button);
                        ShopManager.GrangeShops[signOwner].ShowSummary();
                        return;
                    }
                    
                    // clicked on NPC shop sign, open the store
                    // monitor.Log($"Suppress and show shop: clicked on NPC shop sign, open the store", LogLevel.Debug);
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

        private void CheckDebugKeybinds(ButtonPressedEventArgs e)
        {
            if (e.Button == Config.ReloadKeybind)
            {
                HotReload("", null);
                Helper.Input.Suppress(e.Button);
            }

            string oldOutput = Game1.debugOutput;
            if (e.Button == Config.WarpKeybind)
            {
                Monitor.Log("Warping", LogLevel.Debug);

                var debugCommand = Game1.player.currentLocation.Name == "Town"
                    ? "warp FarmHouse"
                    : "warp Town";
                Game1.game1.parseDebugInput(debugCommand);

                // show result
                monitor.Log(Game1.debugOutput != oldOutput
                    ? $"> {Game1.debugOutput}"
                    : $"Sent debug command '{debugCommand}' to the game, but there was no output.", LogLevel.Info);

                Helper.Input.Suppress(e.Button);
            }

            if (e.Button == Config.OpenConfigKeybind)
            {
                configMenu.OpenModMenu(ModManifest);
                Helper.Input.Suppress(e.Button);
            }

            if (e.Button == Config.StatusKeybind)
            {
                Monitor.Log("Status:", LogLevel.Debug);
                Helper.Input.Suppress(e.Button);
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
            
            ContentPatcherAPI =
                Helper.ModRegistry.GetApi<ContentPatcher.IContentPatcherAPI>("Pathoschild.ContentPatcher");
            // ContentPatcherAPI.RegisterToken(ModManifest, "MarketDayOpen",
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
            
            configMenu.AddNumberOption(ModManifest,
                () => Config.OpeningTime,
                val => Config.OpeningTime = val,
                () => Helper.Translation.Get("cfg.opening-time"),
                () => Helper.Translation.Get("cfg.opening-time.msg"),
                min: 0,
                max: 26
            );
            configMenu.AddNumberOption(ModManifest,
                () => Config.ClosingTime,
                val => Config.ClosingTime = val,
                () => Helper.Translation.Get("cfg.closing-time"),
                () => Helper.Translation.Get("cfg.closing-time.msg"),
                min: 0,
                max: 26
            );
            configMenu.AddNumberOption(ModManifest,
                () => Config.DayOfWeek,
                val => Config.DayOfWeek = val,
                () => Helper.Translation.Get("cfg.day-of-week"),
                () => Helper.Translation.Get("cfg.day-of-week.msg"),
                min: 0,
                max: 6
            );

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
                () => Config.StallVisitChance,
                val => Config.StallVisitChance = val,
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
            
            configMenu.AddBoolOption(ModManifest,
                () => Config.NPCVisitors,
                val => Config.NPCVisitors = val,
                () => Helper.Translation.Get("cfg.npc-visitors"),
                () => Helper.Translation.Get("cfg.npc-visitors.msg")
            );

            configMenu.AddBoolOption(ModManifest,
                () => Config.DebugKeybinds,
                val => Config.DebugKeybinds = val,
                () => Helper.Translation.Get("cfg.debug-keybinds"),
                () => Helper.Translation.Get("cfg.debug-keybinds.msg")
            );
            
            configMenu.AddKeybind(ModManifest,
                () => Config.OpenConfigKeybind,
                val => Config.OpenConfigKeybind = val,
                () => Helper.Translation.Get("cfg.open-config"),
                () => ""
            );
            
            configMenu.AddKeybind(ModManifest,
                () => Config.ReloadKeybind,
                val => Config.ReloadKeybind = val,
                () => Helper.Translation.Get("cfg.reload"),
                () => ""
            );
            
            configMenu.AddKeybind(ModManifest,
                () => Config.WarpKeybind,
                val => Config.WarpKeybind = val,
                () => Helper.Translation.Get("cfg.warp"),
                () => ""
            );
            
            configMenu.AddKeybind(ModManifest,
                () => Config.StatusKeybind,
                val => Config.StatusKeybind = val,
                () => Helper.Translation.Get("cfg.status"),
                () => ""
            );
        }

        private void SaveConfig() {
            Helper.WriteConfig(Config);
            Helper.Content.InvalidateCache(@"Data/Blueprints");
        }
        
        internal static bool IsMarketDay()
        {
            return Game1.dayOfMonth % 7 == Config.DayOfWeek &&
                   !Game1.isRaining &&
                   !Game1.isSnowing &&
                   !StardewValley.Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason);
        }
        
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