using System;
using System.Collections.Generic;
using System.Linq;
using ContentPatcher;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = System.Object;
using SObject = StardewValley.Object;

/*
 * TODO:
 * allow another villager to open a shop
 * auto-restock from the chest
 * stick a sales report in the mail
 * make sure this stuff doesn't run during the fair
 */

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

    public class Store
    {
        public int X;
        public int Y;
        public Chest StorageChest;
        public Sign GrangeSign;
        
        public Vector2 VisibleChestPosition;
        public Vector2 VisibleSignPosition;
        public Vector2 HiddenChestPosition;
        public Vector2 HiddenSignPosition;
        
        public List<SalesRecord> Sales = new();
        public Dictionary<NPC, int> recentlyLooked = new();

        Store(int X, int Y)
        {
            
        }

    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class FarmersMarket : Mod
    {
        internal const int STORE_X = 23;
        internal const int STORE_Y = 63;
        internal const double VISIT_CHANCE = 0.25;
        private const double BUY_CHANCE = 0.25;
        private const int WOOD_SIGN = 37;

        private static Vector2 VisibleChestPosition = new Vector2(STORE_X + 3, STORE_Y + 1);
        private static Vector2 VisibleSignPosition = new Vector2(STORE_X + 3, STORE_Y + 3);

        private static Vector2 HiddenChestPosition = new Vector2(STORE_X - 9, STORE_Y + 1);
        private static Vector2 HiddenSignPosition = new Vector2(STORE_X - 9, STORE_Y + 3);

        // private Vector2 HiddenChestPosition = new Vector2(1337, 1337); 
        // private Vector2 HiddenSignPosition = new Vector2(1337, 1338); 

        private static ContentPatcher.IContentPatcherAPI ContentPatcherAPI;
        private static IManagedConditions MarketDayConditions;
        internal static ModConfig Config;
        internal static IMonitor SMonitor;
        private static readonly List<Item> GrangeDisplay = new();

        private static List<SalesRecord> Sales = new();
        private Dictionary<NPC, int> recentlyLooked = new();

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;

            Helper.Events.GameLoop.GameLaunched += OnLaunched;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            Helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
            // Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdateTicking;
            Helper.Events.GameLoop.DayEnding += OnDayEnding;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;

            while (GrangeDisplay.Count < 9) GrangeDisplay.Add(null);

            Harmony harmony = new Harmony("ceruleandeep.FarmersMarket");
            harmony.PatchAll();
            
            helper.ConsoleCommands.Add("fm_destroy", "Destroy furniture", DestroyFurniture);

        }

        /// <summary>Raised after the game is saved</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaving(object sender, SavingEventArgs e)
        {
            Helper.WriteConfig(Config);
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaveLoaded(object sender, EventArgs e)
        {
            // reload the config to pick up any changes made in GMCM on the title screen
            Config = Helper.ReadConfig<ModConfig>();
        }

        [EventPriority(EventPriority.Low)]
        void OnDayStarted(object sender, EventArgs e)
        {
            Sales = new List<SalesRecord>();
            recentlyLooked = new Dictionary<NPC, int>();
            
            var rawConditions = new Dictionary<string, string>
            {
                ["DayOfWeek"] = "Saturday",
                ["Weather"] = "Sun, Wind",
                ["HasValue:{{DayEvent}}"] = "false"
            };
            MarketDayConditions = ContentPatcherAPI.ParseConditions(
                ModManifest,
                rawConditions,
                new SemanticVersion("1.20.0")
            );
            
            if (IsMarketDay())
            {
                SMonitor.Log($"Market day", LogLevel.Info);
                EnsureChestPresent();
            }
            else
            {
                EnsureChestHidden();
                SMonitor.Log($"Apparently not a market day", LogLevel.Info);
            }
        }

        void OnDayEnding(object sender, EventArgs e)
        {
            EmptyStoreIntoChest();
            EnsureChestHidden();
        }

        private void EnsureChestPresent()
        {
            GetReferencesToFurniture();

            var location = Game1.getLocationFromName("Town");
            location.setObject(VisibleChestPosition, StorageChest);
            location.removeObject(HiddenChestPosition, false);
            // location.moveObject((int)StorageChest.TileLocation.X, (int)StorageChest.TileLocation.Y, (int)VisibleChestPosition.X, (int)VisibleChestPosition.Y);
            location.moveObject((int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) VisibleSignPosition.X, (int) VisibleSignPosition.Y);
            //
            // location.setObject(VisibleChestPosition, StorageChest);
            // location.setObject(VisibleSignPosition, GrangeSign);
            // // location.objects[VisibleSignPosition] = GrangeSign;
            //
            // location.removeObject(HiddenChestPosition, false);
            // location.removeObject(HiddenSignPosition, false);
        }

        private void GetReferencesToFurniture()
        {
            SMonitor.Log($"GetReferencesToFurniture", LogLevel.Debug);

            var location = Game1.getLocationFromName("Town");
            if (StorageChest is null)
            {
                foreach (var (tile, item) in location.Objects.Pairs)
                {
                    if (item is not Chest chest) continue;
                    if (!chest.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out _)) continue;
                    SMonitor.Log($"    StorageChest at {tile} claims to be at {chest.TileLocation}", LogLevel.Debug);
                    StorageChest = chest;
                }

                if (StorageChest is null)
                {
                    SMonitor.Log($"    Creating new StorageChest at {HiddenChestPosition}", LogLevel.Debug);
                    StorageChest = new Chest(true, HiddenChestPosition, 232)
                    {
                        modData = {[$"{ModManifest.UniqueID}/GrangeStorage"] = "true"}
                    };
                    location.setObject(HiddenChestPosition, StorageChest);
                }
            }

            if (GrangeSign is null)
            {
                foreach (var (tile, item) in location.Objects.Pairs)
                {
                    if (item is not Sign sign) continue;
                    if (!sign.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out _)) continue;
                    SMonitor.Log($"    GrangeSign at {tile} claims to be at {sign.TileLocation}", LogLevel.Debug);
                    GrangeSign = sign;
                }

                if (GrangeSign is null)
                    SMonitor.Log($"    Creating new GrangeSign at {HiddenSignPosition}", LogLevel.Debug);
                GrangeSign = new Sign(HiddenSignPosition, WOOD_SIGN)
                {
                    modData = {[$"{ModManifest.UniqueID}/GrangeSign"] = "true"}
                };
                location.objects[HiddenSignPosition] = GrangeSign;
            }
            SMonitor.Log($"    ... StorageChest at {StorageChest.TileLocation}", LogLevel.Debug);
            SMonitor.Log($"    ... GrangeSign at {GrangeSign.TileLocation}", LogLevel.Debug);

        }

        private void DestroyFurniture(string command, string[] args)
        {
            SMonitor.Log($"DestroyFurniture", LogLevel.Debug);

            var toRemove = new Dictionary<Vector2, SObject>();

            var location = Game1.getLocationFromName("Town");
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out _))
                {
                    SMonitor.Log($"    GrangeStorage at {item.TileLocation}", LogLevel.Debug);
                    toRemove[tile] = item;
                }
                
                if (item.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out _))
                {
                    SMonitor.Log($"    GrangeSign at {item.TileLocation}", LogLevel.Debug);
                    toRemove[tile] = item;
                }
            }

            foreach (var (tile, item) in toRemove)
            {
                SMonitor.Log($"    Removing {item.displayName} from {tile}", LogLevel.Debug);
                location.Objects.Remove(tile);
            }

            StorageChest = null;
            GrangeSign = null;
        }

        private static void EmptyStoreIntoChest()
        {
            for (var j = 0; j < GrangeDisplay.Count; j++)
            {
                if (GrangeDisplay[j] == null) continue;
                StorageChest.addItem(GrangeDisplay[j]);
                GrangeDisplay[j] = null;
            }
        }

        private void EnsureChestHidden()
        {
            SMonitor.Log($"EnsureChestHidden: I have been instructed to move the furniture", LogLevel.Debug);

            var location = Game1.getLocationFromName("Town");
            GetReferencesToFurniture();

            SMonitor.Log($"EnsureChestHidden: StorageChest from {StorageChest.TileLocation} to {HiddenChestPosition}",
                LogLevel.Debug);
            location.setObject(HiddenChestPosition, StorageChest);
            location.objects.Remove(VisibleChestPosition);
            // location.moveObject((int)StorageChest.TileLocation.X, (int)StorageChest.TileLocation.Y, (int)HiddenChestPosition.X, (int)HiddenChestPosition.Y);
            StorageChest.modData[$"{ModManifest.UniqueID}/MovedYou"] = "yes";

            SMonitor.Log($"EnsureChestHidden: GrangeSign from {GrangeSign.TileLocation} to {HiddenSignPosition}",
                LogLevel.Debug);
            location.moveObject((int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) HiddenSignPosition.X, (int) HiddenSignPosition.Y);

            // location.setObject(HiddenSignPosition, GrangeSign);
            //
            // location.removeObject(VisibleChestPosition, false);
            // location.removeObject(VisibleSignPosition, false);
            //

            // location.setObject(HiddenChestPosition, StorageChest);
            // location.objects[HiddenSignPosition] = GrangeSign;
            // location.setObject(VisibleChestPosition, null);
            // location.objects[VisibleSignPosition] = null;
        }

        void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            if (!e.IsMultipleOf(10)) return;
            if (!Context.IsWorldReady) return;
            if (!IsMarketDay()) return;

            // var nearby = new List<string>();
            // foreach (var npc in NearbyNPCs()) nearby.Add($"{npc.displayName} ({npc.movementPause})");
            // SMonitor.Log($"Nearby NPCs: {string.Join(", ", nearby)}", LogLevel.Debug);

            // var looks = new List<string>();
            // foreach (var (rl, time) in recentlyLooked) looks.Add($"{rl.displayName} {time}");
            // SMonitor.Log($"Recent looks: {string.Join(", ", looks)}", LogLevel.Debug);

            // var buys = new List<string>();
            // foreach (var record in Sales) buys.AddItem($"{record.npc.displayName} {record.timeOfDay}");
            // SMonitor.Log($"Recent buys: {string.Join(", ", buys)}", LogLevel.Debug);

            foreach (var npc in NearbyNPCs().Where(npc => npc.movementPause <= 0 && !RecentlyBought(npc)))
            {
                if (recentlyLooked.TryGetValue(npc, out var time))
                {
                    if (Game1.timeOfDay - time < 100) continue;
                }

                npc.Halt();
                npc.faceDirection(0);
                npc.movementPause = 5000;
                recentlyLooked[npc] = Game1.timeOfDay;
            }
        }

        private static bool RecentlyBought(NPC npc)
        {
            return Sales.Any(sale => sale.npc == npc && sale.timeOfDay > Game1.timeOfDay - 100);
        }

        private void OnOneSecondUpdateTicking(object sender, OneSecondUpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (!IsMarketDay()) return;
            SellSomethingFromGrangeDisplay();
        }

        private void SellSomethingFromGrangeDisplay()
        {
            foreach (var npc in NearbyNPCs())
            {
                // busy looking
                if (npc.movementPause is > 2000 or < 500) continue;

                // already bought
                if (RecentlyBought(npc)) continue;

                // unlucky
                if (Game1.random.NextDouble() < BUY_CHANCE + Game1.player.DailyLuck) continue;

                // check stock                
                var available = GrangeDisplay.Where(gi => gi is not null).ToList();
                if (available.Count == 0)
                {
                    // no stock
                    npc.doEmote(12);
                    return;
                }

                available.Sort((a, b) =>
                    ItemPurchaseLikelihoodMultiplier(a, npc).CompareTo(ItemPurchaseLikelihoodMultiplier(b, npc)));

                foreach (var ai in available)
                {
                    var m = SellPriceMultiplier(ai, npc);
                    SMonitor.Log($"Available item {ai.DisplayName} mult {m}");
                }

                var i = GrangeDisplay.IndexOf(available[0]);
                var item = GrangeDisplay[i];

                var mult = SellPriceMultiplier(item, npc);
                var obj = ((SObject) item);
                if (obj is null) continue;
                var salePrice = obj.sellToStorePrice();

                salePrice = Convert.ToInt32(salePrice * mult);

                Game1.player.Money += salePrice;
                SMonitor.Log($"Item {item.ParentSheetIndex} sold for {salePrice}", LogLevel.Debug);
                GrangeDisplay[i] = null;

                var newSale = new SalesRecord()
                {
                    item = item,
                    price = salePrice,
                    npc = npc,
                    mult = mult,
                    timeOfDay = Game1.timeOfDay
                };
                Sales.Add(newSale);
                ListSales();

                if (Game1.random.NextDouble() < 0.25)
                {
                    npc.doEmote(20);
                }
                else
                {
                    string dialog;
                    if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_love)
                    {
                        dialog = Get("love", new {itemName = item.DisplayName});
                    }
                    else if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_like)
                    {
                        dialog = Get("like", new {itemName = item.DisplayName});
                    }
                    else if (((SObject) item).Quality == SObject.bestQuality)
                    {
                        dialog = Get("iridium", new {itemName = item.DisplayName});
                    }
                    else if (((SObject) item).Quality == SObject.highQuality)
                    {
                        dialog = Get("gold", new {itemName = item.DisplayName});
                    }
                    else if (((SObject) item).Quality == SObject.medQuality)
                    {
                        dialog = Get("silver", new {itemName = item.DisplayName});
                    }
                    else
                    {
                        dialog = Get("buy", new {itemName = item.DisplayName});
                    }

                    npc.showTextAboveHead(dialog, -1, 2, 1000);
                    npc.Sprite.UpdateSourceRect();
                }
            }
        }

        private static void ListSales()
        {
            foreach (var sale in Sales)
            {
                var displayMult = Convert.ToInt32(sale.mult * 100);
                SMonitor.Log(
                    sale.npc is not null
                        ? $"{sale.item.DisplayName} sold to {sale.npc?.displayName} for {sale.price}g ({displayMult}%)"
                        : $"{sale.item.DisplayName} sold for {sale.price}g ({displayMult}%)",
                    LogLevel.Debug);
            }
        }

        private static List<NPC> NearbyNPCs()
        {
            var location = Game1.getLocationFromName("Town");
            if (location is null) return new List<NPC>();

            // foreach (var n in location.characters)
            // {
            //     var dx = Math.Abs(STORE_X + 1 - n.getTileX());
            //     var dy = Math.Abs(STORE_Y + 4 - n.getTileY());
            //     var dist = Math.Abs(dx) + Math.Abs(dy);
            //     var pf = n.controller is null ? "no controller" : "has a controller";
            //     SMonitor.Log($"NearbyNPC: {n.displayName} pf {pf} dx {dx} dy {dy} dist {dist}", LogLevel.Debug);
            // }

            //x= 23 24 25
            //STORE_Y=67
            var nearby = (from npc in location.characters
                // let dx = Math.Abs(STORE_X + 1 - npc.getTileX())
                // let dy = Math.Abs(STORE_Y + 4 - npc.getTileY())
                where npc.getTileX() >= STORE_X && npc.getTileX() <= STORE_X + 2 && npc.getTileY() == STORE_Y + 4
                select npc).ToList();
            return nearby;
        }

        private static NPC NearbyNPC()
        {
            var location = Game1.getLocationFromName("Town");
            if (location is null) return null;

            var nearby = NearbyNPCs();
            return nearby.Count == 0 ? null : nearby[Game1.random.Next(nearby.Count)];
        }

        /*
        * sell price buffs:
        
        * sold to an NPC
        * npc relationship
        * NPC likes/loves item
        * item is in season
        * lucky
        */

        private static double SellPriceMultiplier(Item item, NPC npc)
        {
            var mult = 1.0;

            // * general quality of display
            mult += GetPointsMultiplier(GetGrangeScore());

            // * farmer is nearby
            if (Game1.player.currentLocation.Name == "Town") mult += 0.2;

            // * value of item on sign
            if (GrangeSign.displayItem.Value is not null)
            {
                var signSellPrice = ((SObject) GrangeSign.displayItem.Value).sellToStorePrice();
                signSellPrice = Math.Min(signSellPrice, 1000);
                mult += signSellPrice / 1000.0 / 10.0;
            }

            // * gift taste
            switch (npc.getGiftTasteForThisItem(item))
            {
                case NPC.gift_taste_like:
                    mult += 1.2;
                    break;
                case NPC.gift_taste_love:
                    mult += 1.4;
                    break;
            }

            // * friendship
            var hearts = Game1.player.getFriendshipHeartLevelForNPC(npc.Name);
            mult += hearts / 100.0;

            // * talked today;
            if (Game1.player.hasPlayerTalkedToNPC(npc.Name)) mult += 0.1;

            return mult;
        }

        private static double ItemPurchaseLikelihoodMultiplier(Item item, NPC npc)
        {
            // * gift taste
            switch (npc.getGiftTasteForThisItem(item))
            {
                case NPC.gift_taste_dislike:
                case NPC.gift_taste_hate:
                    return 0.0;
                case NPC.gift_taste_neutral:
                    return 1.0;
                case NPC.gift_taste_like:
                    return 2.0;
                case NPC.gift_taste_love:
                    return 4.0;
                default:
                    return 1.0;
            }
        }

        void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!IsMarketDay()) return;

            Vector2 tileLocation = new Vector2(STORE_X, STORE_Y);
            float drawLayer = Math.Max(0f, ((tileLocation.Y + 1) * 64 - 24) / 10000f) + tileLocation.X * 1E-05f;
            drawGrangeItems(tileLocation, e.SpriteBatch, drawLayer);
        }

        // aedenthorn
        private static void drawGrangeItems(Vector2 tileLocation, SpriteBatch spriteBatch, float layerDepth)
        {
            Vector2 start = Game1.GlobalToLocal(Game1.viewport, tileLocation * 64);

            // if (Config.ShowCurrentScoreOnGrange)
            // {
            //     int score = GetGrangeScore();
            //     string farmName = $"{Game1.player.farmName.Value} Farm";
            //     spriteBatch.DrawString(Game1.smallFont,
            //         farmName,
            //         new Vector2(start.X + 24 * 4 - farmName.Length * 8, start.Y + 51 * 4),
            //         GetPointsColor(score), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, layerDepth + 0.0202f);
            // }

            start.X += 4f;
            int xCutoff = (int) start.X + 168;
            start.Y += 8f;

            for (int j = 0; j < GrangeDisplay.Count; j++)
            {
                if (GrangeDisplay[j] != null)
                {
                    start.Y += 42f;
                    start.X += 4f;
                    spriteBatch.Draw(Game1.shadowTexture, start,
                        new Microsoft.Xna.Framework.Rectangle?(Game1.shadowTexture.Bounds), Color.White, 0f,
                        Vector2.Zero, 4f, SpriteEffects.None, layerDepth + 0.02f);
                    start.Y -= 42f;
                    start.X -= 4f;
                    GrangeDisplay[j].drawInMenu(spriteBatch, start, 1f, 1f,
                        layerDepth + 0.0201f + j / 10000f, StackDrawType.Hide);
                }

                start.X += 60f;
                if (start.X >= xCutoff)
                {
                    start.X = xCutoff - 168;
                    start.Y += 64f;
                }
            }
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.activeClickableMenu is not null) return;

            var (x, y) = e.Cursor.Tile;

            if (e.Button.IsUseToolButton())
            {
                if (Game1.currentLocation.objects.TryGetValue(new Vector2(x, y), out var objectAt))
                {
                    if (objectAt.modData.ContainsKey($"{ModManifest.UniqueID}/GrangeStorage") ||
                        objectAt.modData.ContainsKey($"{ModManifest.UniqueID}/GrangeSign"))
                    {
                        // Helper.Input.Suppress(e.Button);
                        return;
                    }
                }
            }

            if (e.Button.IsActionButton())
            {
                if (!IsMarketDay()) return;

                var tileIndexAt = Game1.currentLocation.getTileIndexAt((int) x, (int) y, "Buildings");
                if (tileIndexAt is < 349 or > 351) return;

                Game1.activeClickableMenu =
                    new StorageContainer(GrangeDisplay, 9, 3, onGrangeChange, Utility.highlightSmallObjects);

                Helper.Input.Suppress(e.Button);
            }
        }

        private static void addItemToGrangeDisplay(Item i, int position, bool force)
        {
            SMonitor.Log($"addItemToGrangeDisplay: item count {GrangeDisplay.Count}", LogLevel.Debug);

            while (GrangeDisplay.Count < 9) GrangeDisplay.Add(null);
            SMonitor.Log($"addItemToGrangeDisplay: item {i?.ParentSheetIndex} position {position} force {force}",
                LogLevel.Debug);

            if (position < 0) return;
            if (position >= GrangeDisplay.Count) return;
            if (GrangeDisplay[position] != null && !force) return;

            SMonitor.Log($"addItemToGrangeDisplay: adding item", LogLevel.Debug);

            GrangeDisplay[position] = i;
        }

        private static bool onGrangeChange(Item i, int position, Item old, StorageContainer container, bool onRemoval)
        {
            SMonitor.Log(
                $"onGrangeChange: item {i.ParentSheetIndex} position {position} old item {old?.ParentSheetIndex} onRemoval: {onRemoval}",
                LogLevel.Debug);

            if (!onRemoval)
            {
                if (i.Stack > 1 || i.Stack == 1 && old is {Stack: 1} && i.canStackWith(old))
                {
                    SMonitor.Log($"onGrangeChange: big stack", LogLevel.Debug);

                    if (old != null && old.canStackWith(i))
                    {
                        // tried to add extra of same item to a slot that's already taken, 
                        // reset the stack size to 1
                        SMonitor.Log(
                            $"onGrangeChange: can stack: heldItem now {old.Stack} of {old.ParentSheetIndex}, rtn false",
                            LogLevel.Debug);

                        container.ItemsToGrabMenu.actualInventory[position].Stack = 1;
                        container.heldItem = old;
                        return false;
                    }

                    if (old != null)
                    {
                        // tried to add item to a slot that's already taken, 
                        // swap the old item back in
                        SMonitor.Log(
                            $"onGrangeChange: cannot stack: helditem now {i.Stack} of {i.ParentSheetIndex}, {old.ParentSheetIndex} to inventory, rtn false",
                            LogLevel.Debug);

                        Utility.addItemToInventory(old, position, container.ItemsToGrabMenu.actualInventory);
                        container.heldItem = i;
                        return false;
                    }


                    int allButOne = i.Stack - 1;
                    Item reject = i.getOne();
                    reject.Stack = allButOne;
                    container.heldItem = reject;
                    i.Stack = 1;
                    SMonitor.Log(
                        $"onGrangeChange: only accept 1, reject {allButOne}, heldItem now {reject.Stack} of {reject.ParentSheetIndex}",
                        LogLevel.Debug);
                }
            }
            else if (old is {Stack: > 1})
            {
                SMonitor.Log($"onGrangeChange: old {old.ParentSheetIndex} stack {old?.Stack}", LogLevel.Debug);

                if (!old.Equals(i))
                {
                    SMonitor.Log($"onGrangeChange: item {i.ParentSheetIndex} old {old?.ParentSheetIndex} return false",
                        LogLevel.Debug);
                    return false;
                }
            }

            var itemToAdd = onRemoval && (old == null || old.Equals(i)) ? null : i;
            SMonitor.Log($"onGrangeChange: force-add {itemToAdd?.ParentSheetIndex} at {position}", LogLevel.Debug);

            addItemToGrangeDisplay(itemToAdd, position, true);
            return true;
        }

        private void OnLaunched(object sender, GameLaunchedEventArgs e)
        {
            Config = Helper.ReadConfig<ModConfig>();
            Helper.WriteConfig(Config);

            var api = Helper.ModRegistry.GetApi<GenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
            if (api is null) return;
            api.RegisterModConfig(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));
            api.SetDefaultIngameOptinValue(ModManifest, true);

            api.RegisterSimpleOption(ModManifest, "Grumpy", "", () => Config.Grumpy, val => Config.Grumpy = val);
            api.RegisterClampedOption(ModManifest, "Dialog Chance", "Dialog Chance", () => Config.DialogChance,
                val => Config.DialogChance = val, 0.0f, 1.0f, 0.05f);
            api.RegisterClampedOption(ModManifest, "Junimo Language Chance",
                "Chance for dialog to appear in Junimo language", () => Config.JunimoTextChance,
                val => Config.JunimoTextChance = val, 0.0f, 1.0f, 0.50f);
            api.RegisterSimpleOption(ModManifest, "Extra debug output", "", () => Config.ExtraDebugOutput,
                val => Config.ExtraDebugOutput = val);

            ContentPatcherAPI =
                this.Helper.ModRegistry.GetApi<ContentPatcher.IContentPatcherAPI>("Pathoschild.ContentPatcher");
            ContentPatcherAPI.RegisterToken(ModManifest, "FarmersMarketOpen",
                () => { return Context.IsWorldReady ? new[] {IsMarketDayJustForToken() ? "true" : "false"} : null; });
        }

        private static bool IsMarketDay()
        {
            if (MarketDayConditions is null || !MarketDayConditions.IsReady)
            {
                SMonitor.Log($"IsMarketDay: MarketDayConditions null", LogLevel.Warn);
                return false;
            }

            MarketDayConditions.UpdateContext();
            return MarketDayConditions.IsMatch;
        }

        private bool IsMarketDayJustForToken()
        {
            if (MarketDayConditions is null)
            {
                SMonitor.Log($"IsMarketDayJustForToken: MarketDayConditions null", LogLevel.Warn);
                return false;
            }

            if (!MarketDayConditions.IsReady)
            {
                SMonitor.Log($"IsMarketDayJustForToken: MarketDayConditions not ready", LogLevel.Warn);
                return false;
            }

            MarketDayConditions.UpdateContext();
            SMonitor.Log($"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} MarketDayConditions {MarketDayConditions.IsMatch}", LogLevel.Info);


            var rawConditions = new Dictionary<string, string>
            {
                ["DayOfWeek"] = "Saturday",
                ["Weather"] = "Sun, Wind",
                ["HasValue:{{DayEvent}}"] = "false"
            };
            var spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
                ModManifest,
                rawConditions,
                new SemanticVersion("1.20.0")
            );
            spareMarketDayConditions.UpdateContext();
            SMonitor.Log($"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} spareMarketDayConditions {spareMarketDayConditions.IsMatch}",
                LogLevel.Info);


            rawConditions = new Dictionary<string, string>
            {
                ["DayOfWeek"] = "Saturday",
            };
            spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
                ModManifest,
                rawConditions,
                new SemanticVersion("1.20.0")
            );
            spareMarketDayConditions.UpdateContext();
            SMonitor.Log($"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} DayOfWeek {spareMarketDayConditions.IsMatch}",
                LogLevel.Info);

            rawConditions = new Dictionary<string, string>
            {
                ["Weather"] = "Sun, Wind",
            };
            spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
                ModManifest,
                rawConditions,
                new SemanticVersion("1.20.0")
            );
            spareMarketDayConditions.UpdateContext();
            SMonitor.Log($"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} Weather {spareMarketDayConditions.IsMatch}",
                LogLevel.Info);
            
            rawConditions = new Dictionary<string, string>
            {
                ["HasValue:{{DayEvent}}"] = "false"
            };
            spareMarketDayConditions = ContentPatcherAPI.ParseConditions(
                ModManifest,
                rawConditions,
                new SemanticVersion("1.20.0")
            );
            spareMarketDayConditions.UpdateContext();
            SMonitor.Log($"IsMarketDayJustForToken: {Game1.dayOfMonth}th {Game1.timeOfDay} DayEvent {spareMarketDayConditions.IsMatch}",
                LogLevel.Info);
            

            
            return MarketDayConditions.IsMatch;
        }

        // aedenthorn
        private static int GetGrangeScore()
        {
            int pointsEarned = 14;
            Dictionary<int, bool> categoriesRepresented = new Dictionary<int, bool>();
            int nullsCount = 0;
            foreach (Item i in GrangeDisplay)
            {
                if (i != null && i is SObject)
                {
                    if (Event.IsItemMayorShorts(i as SObject))
                    {
                        return -666;
                    }

                    pointsEarned += (i as SObject).Quality + 1;
                    int num = (i as SObject).sellToStorePrice(-1L);
                    if (num >= 20)
                    {
                        pointsEarned++;
                    }

                    if (num >= 90)
                    {
                        pointsEarned++;
                    }

                    if (num >= 200)
                    {
                        pointsEarned++;
                    }

                    if (num >= 300 && (i as SObject).Quality < 2)
                    {
                        pointsEarned++;
                    }

                    if (num >= 400 && (i as SObject).Quality < 1)
                    {
                        pointsEarned++;
                    }

                    int category = (i as SObject).Category;
                    if (category <= -27)
                    {
                        switch (category)
                        {
                            case -81:
                            case -80:
                                break;
                            case -79:
                                categoriesRepresented[-79] = true;
                                continue;
                            case -78:
                            case -77:
                            case -76:
                                continue;
                            case -75:
                                categoriesRepresented[-75] = true;
                                continue;
                            default:
                                if (category != -27)
                                {
                                    continue;
                                }

                                break;
                        }

                        categoriesRepresented[-81] = true;
                    }
                    else if (category != -26)
                    {
                        if (category != -18)
                        {
                            switch (category)
                            {
                                case -14:
                                case -6:
                                case -5:
                                    break;
                                case -13:
                                case -11:
                                case -10:
                                case -9:
                                case -8:
                                case -3:
                                    continue;
                                case -12:
                                case -2:
                                    categoriesRepresented[-12] = true;
                                    continue;
                                case -7:
                                    categoriesRepresented[-7] = true;
                                    continue;
                                case -4:
                                    categoriesRepresented[-4] = true;
                                    continue;
                                default:
                                    continue;
                            }
                        }

                        categoriesRepresented[-5] = true;
                    }
                    else
                    {
                        categoriesRepresented[-26] = true;
                    }
                }
                else if (i == null)
                {
                    nullsCount++;
                }
            }

            pointsEarned += Math.Min(30, categoriesRepresented.Count * 5);
            int displayFilledPoints = 9 - 2 * nullsCount;
            pointsEarned += displayFilledPoints;
            return pointsEarned;
        }

        private static Color GetPointsColor(int score)
        {
            if (score >= 90)
                return new Color(120, 255, 120);
            if (score >= 75)
                return Color.Yellow;
            if (score >= 60)
                return new Color(255, 200, 0);
            if (score < 0)
                return Color.MediumPurple;
            return Color.Red;
        }

        private static double GetPointsMultiplier(int score)
        {
            return score switch
            {
                >= 90 => 0.15,
                >= 75 => 0.1,
                >= 60 => 0.05,
                < 0 => 0,
                _ => 0
            };
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