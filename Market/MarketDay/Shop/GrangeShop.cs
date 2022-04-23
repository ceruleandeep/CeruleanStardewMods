using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MarketDay.ItemPriceAndStock;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace MarketDay.Shop
{
    public class GrangeShop : ItemShop
    {
        private const int WOOD_SIGN = 37;
        private const double BUY_CHANCE = 0.25;

        public readonly List<Item> GrangeDisplay = new();

        public int X;
        public int Y;

        public Chest StorageChest;
        public Sign GrangeSign;

        public Vector2 VisibleChestPosition;
        public Vector2 VisibleSignPosition;

        public Vector2 PlayerHiddenChestPosition = new Vector2(1337, 1337);
        public Vector2 PlayerHiddenSignPosition = new Vector2(1337, 1338);

        public int VisitorsToday;
        public int GrumpyVisitorsToday;
        public List<SalesRecord> Sales = new();
        public Dictionary<NPC, int> recentlyLooked = new();
        public Dictionary<NPC, int> recentlyTended = new();

        public GrangeShop()
        {
            while (GrangeDisplay.Count < 9) GrangeDisplay.Add(null);
        }

        public void SetOrigin(Vector2 Origin)
        {
            this.X = (int) Origin.X;
            this.Y = (int) Origin.Y;
            VisibleChestPosition = new Vector2(X + 3, Y + 1);
            VisibleSignPosition = new Vector2(X + 3, Y + 3);

            // if (MarketDay.Config.HideFurniture)
            // {
            //     HiddenChestPosition = new Vector2(X + 1337, Y + 1);
            //     HiddenSignPosition = new Vector2(X + 1337, Y + 3);
            // }
            // else
            // {
            //     HiddenChestPosition = new Vector2(X + 5, Y + 1);
            //     HiddenSignPosition = new Vector2(X + 5, Y + 3);
            // }

            //
            // if (Quote is null || Quote.Length == 0)
            //     Quote = Get("default-stall-description", new {NpcName = ShopName});
        }

        public bool IsPlayerShop()
        {
            return ShopName == "Player";
        }

        public Vector2 OwnerPosition()
        {
            return new Vector2(X + 3, Y + 2);
        }

        public void OnDayStarted(bool IsMarketDay)
        {
            Sales = new List<SalesRecord>();
            VisitorsToday = 0;
            GrumpyVisitorsToday = 0;
            recentlyLooked = new Dictionary<NPC, int>();
            recentlyTended = new Dictionary<NPC, int>();

            if (!IsMarketDay) return;
            Log($"Market day", LogLevel.Trace);

            GetReferencesToFurniture();

            if (!IsPlayerShop())
            {
                StockChestForTheDay();
            }

            if (!IsPlayerShop() || MarketDay.Config.AutoStockAtStartOfDay) RestockGrangeFromChest(true);

            ShowFurniture();
            DecorateFurniture();
        }

        public void OnTimeChanged(bool IsMarketDay)
        {
            if (!IsMarketDay) return;

            if (IsPlayerShop())
            {
                if (MarketDay.Config.RestockItemsPerHour > 0) RestockGrangeFromChest();
            }
            else
            {
                RestockGrangeFromChest();
            }
        }

        public void OnDayEnding()
        {
            if (IsPlayerShop())
            {
                EmptyStoreIntoChest();
                HideFurniture();
            }
            else
            {
                DestroyFurniture();
            }
        }

        public void OnUpdateTicking()
        {
            // var nearby = new List<string>();
            // foreach (var npc in NearbyNPCs()) nearby.Add($"{npc.displayName} ({npc.movementPause})");
            // monitor.Log($"Nearby NPCs: {string.Join(", ", nearby)}", LogLevel.Debug);

            // var looks = new List<string>();
            // foreach (var (rl, time) in recentlyLooked) looks.Add($"{rl.displayName} {time}");
            // monitor.Log($"Recent looks: {string.Join(", ", looks)}", LogLevel.Debug);

            // var buys = new List<string>();
            // foreach (var record in Sales) buys.AddItem($"{record.npc.displayName} {record.timeOfDay}");
            // monitor.Log($"Recent buys: {string.Join(", ", buys)}", LogLevel.Debug);

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

                VisitorsToday++;
            }
        }

        internal void OnOneSecondUpdateTicking()
        {
            SellSomethingToOnlookers();
            SeeIfOwnerIsAround();
        }

        internal void OnActionButton(ButtonPressedEventArgs e)
        {
            var tile = e.Cursor.Tile;
            Log($"Button pressed at {tile}", LogLevel.Debug);
            if (tile.X < X || tile.X > X + 3 || tile.Y < Y || tile.Y > Y + 4) return;
            if (IsPlayerShop())
            {
                Game1.activeClickableMenu = new StorageContainer(GrangeDisplay, 9, 3, onGrangeChange,
                    StardewValley.Utility.highlightSmallObjects);
            }
            else
            {
                DisplayShop();
            }

            MarketDay.SMod.Helper.Input.Suppress(e.Button);
        }


        /// <summary>
        /// Opens the shop if conditions are met. If not, display the closed message
        /// </summary>
        public new void DisplayShop(bool debug = false)
        {
            MarketDay.monitor.Log($"Attempting to open the shop \"{ShopName}\" at {Game1.timeOfDay}", LogLevel.Debug);

            if (!debug && ShopClosed)
            {
                if (ClosedMessage != null)
                {
                    Game1.activeClickableMenu = new DialogueBox(ClosedMessage);
                }
                else
                {
                    var openingTime = MarketDay.Config.OpeningTime.ToString();
                    openingTime = openingTime[..^2] + ":" + openingTime[^2..];
                    
                    var closingTime = MarketDay.Config.ClosingTime.ToString();
                    closingTime = closingTime[..^2] + ":" + closingTime[^2..];
                    
                    Game1.activeClickableMenu = new DialogueBox(Get("closed", new {openingTime, closingTime}));
                }

                return;
            }
            
            int currency = 0;
            switch (StoreCurrency)
            {
                case "festivalScore":
                    currency = 1;
                    break;
                case "clubCoins":
                    currency = 2;
                    break;
            }

            // var shopMenu = new ShopMenu(StockManager.ItemPriceAndStock, currency);
            var shopMenu = new ShopMenu(ShopStock(), currency, null, OnPurchase);

            if (CategoriesToSellHere != null)
                shopMenu.categoriesToSellHere = CategoriesToSellHere;

            if (_portrait is null)
            {
                // try to load portrait from NPC the shop is named after
                var npc = Game1.getCharacterFromName(ShopName);
                if (npc is not null) _portrait = npc.Portrait;
            } 
            
            if (_portrait != null)
            {
                shopMenu.portraitPerson = new NPC();
                //only add a shop name the first time store is open each day so that items added from JA's side are only added once
                if (!_shopOpenedToday)
                    shopMenu.portraitPerson.Name = "STF." + ShopName;

                shopMenu.portraitPerson.Portrait = _portrait;
            }

            if (Quote != null)
            {
                shopMenu.potraitPersonDialogue = Game1.parseText(Quote, Game1.dialogueFont, 304);
            }

            Game1.activeClickableMenu = shopMenu;
            _shopOpenedToday = true;
        }


        private bool OnPurchase(ISalable item, Farmer who, int stack)
        {
            for (var j = 0; j < GrangeDisplay.Count; j++)
            {
                if (item is not Item i) continue;
                if (GrangeDisplay[j] is null) continue;
                if (GrangeDisplay[j].ParentSheetIndex != i.ParentSheetIndex) continue;
                GrangeDisplay[j] = null;
                return false;
            }

            return false;
        }

        int getSellPriceFromShopStock(Item item)
        {
            foreach (var (stockItem, priceAndQty) in StockManager.ItemPriceAndStock)
            {
                if (item.ParentSheetIndex != ((Item) stockItem).ParentSheetIndex || item.Category != ((Item) stockItem).Category) continue;
                return priceAndQty[0];
            }

            return 0;
        }

        /// <summary>
        /// Generate a dictionary of goods for sale and quantities, to be consumed by the game's shop menu
        /// </summary>
        private Dictionary<ISalable, int[]> ShopStock()
        {
            // this needs to filter StockManager.ItemPriceAndStock
            var stock = new Dictionary<ISalable, int[]>();

            foreach (var stockItem in GrangeDisplay)
            {
                if (stockItem is null) continue;

                // we won't do item quality right now
                int specifiedQuality = 0;

                // we won't do sell price mult right now
                // var price = StardewValley.Utility.getSellToStorePriceOfItem(stockItem, false);

                var price = getSellPriceFromShopStock(stockItem);
                if (price == 0)
                {
                    price = (int)(StardewValley.Utility.getSellToStorePriceOfItem(stockItem, false) * SellPriceMultiplier(stockItem, null));
                }
                
                var sellItem = stockItem.getOne();
                sellItem.Stack = 1;
                if (sellItem is Object o) o.Quality = specifiedQuality;

                stock[sellItem] = new[] { price, 1 };
            }

            return stock;
        }

        private void SeeIfOwnerIsAround()
        {
            var owner = OwnerNearby();
            if (owner is null) return;

            // busy
            if (owner.movementPause is < 10000 and > 0) return;

            // already bought
            if (recentlyTended.TryGetValue(owner, out var time))
            {
                if (time > Game1.timeOfDay - 100) return;
            }

            ;

            owner.Halt();
            owner.faceDirection(2);
            owner.movementPause = 10000;

            var dialog = Get("spruik");
            owner.showTextAboveHead(dialog, -1, 2, 1000);
            owner.Sprite.UpdateSourceRect();

            recentlyTended[owner] = Game1.timeOfDay;
        }

        private NPC OwnerNearby()
        {
            var (ownerX, ownerY) = new Vector2(X + 3, Y + 2);
            var location = Game1.getLocationFromName("Town");
            var npc = location.characters.FirstOrDefault(n => n.Name == ShopName);
            if (npc is null) return null;
            if (npc.getTileX() == (int) ownerX && npc.getTileY() == (int) ownerY) return npc;
            return null;
        }

        private void SellSomethingToOnlookers()
        {
            if (ShopClosed) return;
            
            foreach (var npc in NearbyNPCs())
            {
                // the owner
                if (npc.Name == ShopName) continue;

                // busy looking
                if (npc.movementPause is > 2000 or < 500) continue;

                // already bought
                if (RecentlyBought(npc)) continue;

                // unlucky
                if (Game1.random.NextDouble() < BUY_CHANCE + Game1.player.DailyLuck) continue;

                // check stock                
                // also remove items the NPC dislikes
                var available = GrangeDisplay.Where(gi => gi is not null && ItemPreferenceIndex(gi, npc) > 0).ToList();
                if (available.Count == 0)
                {
                    // no stock
                    GrumpyVisitorsToday++;
                    npc.doEmote(12);
                    return;
                }

                // find what the NPC likes best
                available.Sort((a, b) =>
                    ItemPreferenceIndex(a, npc).CompareTo(ItemPreferenceIndex(b, npc)));

                var i = GrangeDisplay.IndexOf(available[0]);
                var item = GrangeDisplay[i];

                // buy it
                if (IsPlayerShop()) AddToPlayerFunds(item, npc);
                GrangeDisplay[i] = null;

                EmoteForPurchase(npc, item);
            }
        }

        private static bool ShopClosed =>
            Game1.timeOfDay < MarketDay.Config.OpeningTime ||
            Game1.timeOfDay > MarketDay.Config.ClosingTime;

        private static void EmoteForPurchase(NPC npc, Item item)
        {
            if (Game1.random.NextDouble() < 0.25)
            {
                npc.doEmote(20);
            }
            else
            {
                string dialog = Get("buy", new {ItemName = item.DisplayName});
                if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_love)
                {
                    dialog = Get("love", new {ItemName = item.DisplayName});
                }
                else if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_like)
                {
                    dialog = Get("like", new {ItemName = item.DisplayName});
                }
                else if (item is Object o)
                {
                    dialog = o.Quality switch
                    {
                        Object.bestQuality => Get("iridium", new {ItemName = item.DisplayName}),
                        Object.highQuality => Get("gold", new {ItemName = item.DisplayName}),
                        Object.medQuality => Get("silver", new {ItemName = item.DisplayName}),
                        _ => dialog
                    };
                }

                npc.showTextAboveHead(dialog, -1, 2, 1000);
                npc.Sprite.UpdateSourceRect();
            }
        }

        private void AddToPlayerFunds(Item item, NPC npc)
        {
            var mult = SellPriceMultiplier(item, npc);
            var obj = (Object) item;
            if (obj is null) return;

            var salePrice = obj.sellToStorePrice();

            salePrice = Convert.ToInt32(salePrice * mult);

            Game1.player.Money += salePrice;
            Log($"Item {item.ParentSheetIndex} sold for {salePrice}", LogLevel.Debug);

            var newSale = new SalesRecord()
            {
                item = item,
                price = salePrice,
                npc = npc,
                mult = mult,
                timeOfDay = Game1.timeOfDay
            };
            
            Sales.Add(newSale);
        }

        public void ShowSummary()
        {
            var salesDescriptions = (
                from sale in Sales
                let displayMult = Convert.ToInt32((sale.mult - 1) * 100)
                select Get("sales-desc",
                    new
                    {
                        ItemName = sale.item.DisplayName, NpcName = sale.npc.displayName, Price = sale.price,
                        Mult = displayMult
                    })
            ).ToList();
            if (salesDescriptions.Count == 0) salesDescriptions.Add(Get("no-sales-today"));

            string AverageMult;
            if (Sales.Count > 0)
            {
                var avgBonus = Sales.Select(s => s.mult).Average() - 1;
                AverageMult = $"{Convert.ToInt32(avgBonus * 100)}%";
            }
            else
            {
                AverageMult = Get("no-sales-today");
            }

            var message = Get("daily-summary",
                new
                {
                    FarmName = Game1.player.farmName.Value,
                    ItemsSold = Sales.Count,
                    AverageMult,
                    VisitorsToday,
                    GrumpyVisitorsToday,
                    SalesSummary = string.Join("^", salesDescriptions)
                }
            );
            Game1.drawLetterMessage(message);
        }

        private double SellPriceMultiplier(Item item, NPC npc)
        {
            var mult = 1.0;

            // * general quality of display
            mult += GetPointsMultiplier(GetGrangeScore());

            // * farmer is nearby
            if (Game1.player.currentLocation.Name == "Town") mult += 0.2;

            // * value of item on sign
            if (GrangeSign.displayItem.Value is Object o)
            {
                var signSellPrice = o.sellToStorePrice();
                signSellPrice = Math.Min(signSellPrice, 1000);
                mult += signSellPrice / 1000.0 / 10.0;
            }

            if (npc is null) return mult;

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

        private static double ItemPreferenceIndex(Item item, NPC npc)
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

        private List<NPC> NearbyNPCs()
        {
            var location = Game1.getLocationFromName("Town");
            if (location is null) return new List<NPC>();

            var nearby = (from npc in location.characters
                where npc.getTileX() >= X && npc.getTileX() <= X + 2 && npc.getTileY() == Y + 4
                select npc).ToList();
            return nearby;
        }

        private bool RecentlyBought(NPC npc)
        {
            return Sales.Any(sale => sale.npc == npc && sale.timeOfDay > Game1.timeOfDay - 100);
        }

        private void GetReferencesToFurniture()
        {
            if (!MarketDay.IsMarketDay())
            {
                Log("GetReferencesToFurniture called on non-market day", LogLevel.Error);
                return;
            }

            Log($"GetReferencesToFurniture", LogLevel.Trace);

            var location = Game1.getLocationFromName("Town");

            // the storage chest
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is not Chest chest) continue;
                if (!chest.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage",
                    out var owner)) continue;
                if (owner != ShopName) continue;
                Log($"    StorageChest for {owner} at {tile} claims to be at {chest.TileLocation}", LogLevel.Trace);
                if (tile != chest.TileLocation)
                {
                    chest.TileLocation = tile;
                    Log($"    Moving storage chest, now at {chest.TileLocation}", LogLevel.Trace);
                }

                StorageChest = chest;
            }

            if (StorageChest is null)
            {
                Log($"    Creating new StorageChest at {VisibleChestPosition}", LogLevel.Trace);
                StorageChest = new Chest(true, VisibleChestPosition, 232)
                {
                    modData = {[$"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage"] = ShopName}
                };
                location.setObject(VisibleChestPosition, StorageChest);
            }


            // the grange sign
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is not Sign sign) continue;
                if (!sign.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign",
                    out var owner))
                    continue;
                if (owner != ShopName) continue;
                Log($"    GrangeSign for {owner} at {tile} claims to be at {sign.TileLocation}", LogLevel.Trace);
                GrangeSign = sign;
            }

            if (GrangeSign is null)
            {
                Log($"    Creating new GrangeSign at {VisibleSignPosition}", LogLevel.Trace);
                GrangeSign = new Sign(VisibleSignPosition, WOOD_SIGN)
                {
                    modData = {[$"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign"] = ShopName}
                };
                location.objects[VisibleSignPosition] = GrangeSign;
            }


            // the results

            Log($"    ... StorageChest at {StorageChest.TileLocation}", LogLevel.Trace);
            Log($"    ... GrangeSign at {GrangeSign.TileLocation}", LogLevel.Trace);
        }

        private void DecorateFurniture()
        {
            if (ShopColor.R > 0 || ShopColor.G > 0 || ShopColor.B > 0)
            {
                Log($"    ShopColor {ShopColor}", LogLevel.Debug);
                var color = ShopColor;
                color.A = 255;
                StorageChest.playerChoiceColor.Value = color;
            }
            else
            {
                var ci = Game1.random.Next(20);
                var c = new DiscreteColorPicker(0, 0).getColorFromSelection(ci);
                Log($"    ShopColor randomized to {c}", LogLevel.Trace);
                StorageChest.playerChoiceColor.Value = c;
            }

            Log($"    SignObjectIndex {SignObjectIndex}", LogLevel.Trace);
            Item SignItem = GrangeDisplay.Find(item => item is not null);
            if (SignItem is null && StorageChest.items.Count > 0) SignItem = StorageChest.items[0].getOne();
            if (SignObjectIndex > 0) SignItem = new Object(SignObjectIndex, 1);

            if (SignItem is null) return;
            Log($"    GrangeSign.displayItem.Value to {SignItem.DisplayName}", LogLevel.Trace);
            GrangeSign.displayItem.Value = SignItem;
            GrangeSign.displayType.Value = 1;
        }

        private void ShowFurniture()
        {
            var location = Game1.getLocationFromName("Town");

            Debug.Assert(StorageChest is not null, "StorageChest is not null");
            Debug.Assert(GrangeSign is not null, "GrangeSign is not null");
            Debug.Assert(GrangeSign.TileLocation.X > 0, "GrangeSign.TileLocation.X assigned");
            Debug.Assert(GrangeSign.TileLocation.Y > 0, "GrangeSign.TileLocation.Y assigned");

            location.moveObject(
                (int) StorageChest.TileLocation.X, (int) StorageChest.TileLocation.Y,
                (int) VisibleChestPosition.X, (int) VisibleChestPosition.Y);

            location.moveObject(
                (int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) VisibleSignPosition.X, (int) VisibleSignPosition.Y);
        }

        private void HideFurniture()
        {
            var location = Game1.getLocationFromName("Town");
            if (StorageChest is null) return;
            if (GrangeSign is null) return;
            if (!IsPlayerShop()) return;
            location.moveObject(
                (int) StorageChest.TileLocation.X, (int) StorageChest.TileLocation.Y,
                (int) PlayerHiddenChestPosition.X, (int) PlayerHiddenChestPosition.Y);

            location.moveObject(
                (int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) PlayerHiddenSignPosition.X, (int) PlayerHiddenSignPosition.Y);
        }

        private void DestroyFurniture()
        {
            var toRemove = new Dictionary<Vector2, Object>();

            var location = Game1.getLocationFromName("Town");
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is Sign sign)
                {
                    if (!sign.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign",
                        out var owner))
                        continue;
                    if (owner != ShopName) continue;
                    toRemove[tile] = item;
                }

                if (item is Chest chest)
                {
                    if (!chest.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage",
                        out var owner)) continue;
                    if (owner != ShopName) continue;
                    toRemove[tile] = item;
                }
            }

            foreach (var (tile, item) in toRemove)
            {
                Log($"    Removing {item.displayName} from {tile}", LogLevel.Trace);
                location.Objects.Remove(tile);
            }

            StorageChest = null;
            GrangeSign = null;
        }

        private void StockChestForTheDay()
        {
            foreach (var (Salable, priceAndStock) in StockManager.ItemPriceAndStock)
            {
                // priceAndStock: price, stock, currency obj, currency stack
                Log($"    Stock item {Salable.DisplayName} price {priceAndStock[0]} stock {priceAndStock[1]}", LogLevel.Trace);
                var stack = Math.Min(priceAndStock[1], 13);
                while (stack-- > 0)
                {
                    if (Salable is Item item)
                    {
                        var newItem = item.getOne();
                        newItem.Stack = 1;
                        StorageChest.addItem(newItem);
                    }
                    else
                    {
                        Log($"    Stock item {Salable.DisplayName} is not an Item", LogLevel.Warn);
                    }
                }
            }
        }

        private void RestockGrangeFromChest(bool fullRestock = false)
        {
            if (StorageChest is null) return;
            var restockLimitRemaining = MarketDay.Config.RestockItemsPerHour;

            for (var j = 0; j < GrangeDisplay.Count; j++)
            {
                if (StorageChest.items.Count == 0)
                {
                    Log($"RestockGrangeFromChest: {ShopName} out of stock", LogLevel.Info);
                    return;
                }

                if (restockLimitRemaining <= 0) return;
                if (GrangeDisplay[j] != null) continue;

                var stockItem = StorageChest.items[Game1.random.Next(StorageChest.items.Count)];
                var grangeItem = stockItem.getOne();
                grangeItem.Stack = 1;
                addItemToGrangeDisplay(grangeItem, j, false);

                if (stockItem.Stack == 1)
                {
                    StorageChest.items.Remove(stockItem);
                }
                else
                {
                    stockItem.Stack--;
                }

                if (!fullRestock) restockLimitRemaining--;
            }
        }

        private void EmptyStoreIntoChest()
        {
            for (var j = 0; j < GrangeDisplay.Count; j++)
            {
                if (GrangeDisplay[j] == null) continue;
                StorageChest.addItem(GrangeDisplay[j]);
                GrangeDisplay[j] = null;
            }
        }

        private bool onGrangeChange(Item i, int position, Item old, StorageContainer container, bool onRemoval)
        {
            // Log(
            //     $"onGrangeChange: item {i.ParentSheetIndex} position {position} old item {old?.ParentSheetIndex} onRemoval: {onRemoval}",
            //     LogLevel.Debug);

            if (!onRemoval)
            {
                if (i.Stack > 1 || i.Stack == 1 && old is {Stack: 1} && i.canStackWith(old))
                {
                    // Log($"onGrangeChange: big stack", LogLevel.Debug);

                    if (old != null && old.canStackWith(i))
                    {
                        // tried to add extra of same item to a slot that's already taken, 
                        // reset the stack size to 1
                        // Log(
                        //     $"onGrangeChange: can stack: heldItem now {old.Stack} of {old.ParentSheetIndex}, rtn false",
                        //     LogLevel.Debug);

                        container.ItemsToGrabMenu.actualInventory[position].Stack = 1;
                        container.heldItem = old;
                        return false;
                    }

                    if (old != null)
                    {
                        // tried to add item to a slot that's already taken, 
                        // swap the old item back in
                        // Log(
                        //     $"onGrangeChange: cannot stack: helditem now {i.Stack} of {i.ParentSheetIndex}, {old.ParentSheetIndex} to inventory, rtn false",
                        //     LogLevel.Debug);

                        StardewValley.Utility.addItemToInventory(old, position,
                            container.ItemsToGrabMenu.actualInventory);
                        container.heldItem = i;
                        return false;
                    }


                    int allButOne = i.Stack - 1;
                    Item reject = i.getOne();
                    reject.Stack = allButOne;
                    container.heldItem = reject;
                    i.Stack = 1;
                    // Log(
                    //     $"onGrangeChange: only accept 1, reject {allButOne}, heldItem now {reject.Stack} of {reject.ParentSheetIndex}",
                    //     LogLevel.Debug);
                }
            }
            else if (old is {Stack: > 1})
            {
                // Log($"onGrangeChange: old {old.ParentSheetIndex} stack {old.Stack}", LogLevel.Debug);

                if (!old.Equals(i))
                {
                    // Log($"onGrangeChange: item {i.ParentSheetIndex} old {old.ParentSheetIndex} return false",
                    //     LogLevel.Debug);
                    return false;
                }
            }

            var itemToAdd = onRemoval && (old == null || old.Equals(i)) ? null : i;
            // Log($"onGrangeChange: force-add {itemToAdd?.ParentSheetIndex} at {position}", LogLevel.Debug);

            addItemToGrangeDisplay(itemToAdd, position, true);
            return true;
        }

        private void addItemToGrangeDisplay(Item i, int position, bool force)
        {
            while (GrangeDisplay.Count < 9) GrangeDisplay.Add(null);
            // Log($"addItemToGrangeDisplay: item {i?.ParentSheetIndex} position {position} force {force}",
            //     LogLevel.Debug);

            if (position < 0) return;
            if (position >= GrangeDisplay.Count) return;
            if (GrangeDisplay[position] != null && !force) return;

            GrangeDisplay[position] = i;
        }

        // aedenthorn
        internal void drawGrangeItems(Vector2 tileLocation, SpriteBatch spriteBatch, float layerDepth)
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
                        Game1.shadowTexture.Bounds, Color.White, 0f,
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

        // aedenthorn
        private int GetGrangeScore()
        {
            int pointsEarned = 14;
            Dictionary<int, bool> categoriesRepresented = new Dictionary<int, bool>();
            int nullsCount = 0;
            foreach (Item i in GrangeDisplay)
            {
                if (i != null && i is Object)
                {
                    if (Event.IsItemMayorShorts(i as Object))
                    {
                        return -666;
                    }

                    pointsEarned += (i as Object).Quality + 1;
                    int num = (i as Object).sellToStorePrice();
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

                    if (num >= 300 && (i as Object).Quality < 2)
                    {
                        pointsEarned++;
                    }

                    if (num >= 400 && (i as Object).Quality < 1)
                    {
                        pointsEarned++;
                    }

                    int category = (i as Object).Category;
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

        private void Log(string message, LogLevel level)
        {
            MarketDay.monitor.Log($"[{ShopName}] {message}", level);
        }

        private string Get(string key)
        {
            return MarketDay.SMod.Helper.Translation.Get(key);
        }

        private static string Get(string key, object tokens)
        {
            return MarketDay.SMod.Helper.Translation.Get(key, tokens);
        }
    }
}