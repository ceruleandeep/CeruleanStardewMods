using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SObject = StardewValley.Object;

/*
 * TODO:
 * allow sales of non-item items
 
 * write default translations for texts
 
 * prevent smashing of furniture with Harmony
 * boost price if item is in season
 * fix the drawing layer
 * custom sell prices for player store with prob of purchase?
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
        private const int WOOD_SIGN = 37;
        private const double BUY_CHANCE = 0.25;

        public readonly List<Item> GrangeDisplay = new();

        public int X;
        public int Y;
        public string Name;
        public string Description;
        public double PurchasePriceMultiplier = 1.0;
        public Color StoreColor;
        public int SignObjectIndex;
        public Dictionary<string, int[]> Stock;

        public Chest StorageChest;
        public Sign GrangeSign;

        public bool PlayerStore;

        public Vector2 VisibleChestPosition;
        public Vector2 VisibleSignPosition;
        public Vector2 HiddenChestPosition;
        public Vector2 HiddenSignPosition;

        public int VisitorsToday;
        public int GrumpyVisitorsToday;
        public List<SalesRecord> Sales = new();
        public Dictionary<NPC, int> recentlyLooked = new();
        public Dictionary<NPC, int> recentlyTended = new();

        
        public Store(string name, string description, bool playerStore, int X, int Y)
        {
            Name = name;
            Description = description;
            PlayerStore = playerStore;
            this.X = X;
            this.Y = Y;
            VisibleChestPosition = new Vector2(X + 3, Y + 1);
            VisibleSignPosition = new Vector2(X + 3, Y + 3);

            if (FarmersMarket.Config.HideFurniture)
            {
                HiddenChestPosition = new Vector2(X + 1337, Y + 1);
                HiddenSignPosition = new Vector2(X + 1337, Y + 3);
            }
            else
            {
                HiddenChestPosition = new Vector2(X + 5, Y + 1);
                HiddenSignPosition = new Vector2(X + 5, Y + 3);
            }

            while (GrangeDisplay.Count < 9) GrangeDisplay.Add(null);

            if (Description is null || Description.Length == 0)
                Description = Get("default-stall-description", new {NpcName = Name});
            Log($"stall description: {Description} (from {description})", LogLevel.Debug);
        }

        public void OnDayStarted(bool IsMarketDay)
        {
            Sales = new List<SalesRecord>();
            VisitorsToday = 0;
            GrumpyVisitorsToday = 0;
            recentlyLooked = new Dictionary<NPC, int>();
            recentlyTended = new Dictionary<NPC, int>();
            
            GetReferencesToFurniture();

            if (IsMarketDay)
            {
                Log($"Market day", LogLevel.Info);
                if (!PlayerStore)
                {
                    StockChestForTheDay();
                }
                if (!PlayerStore || FarmersMarket.Config.AutoStockAtStartOfDay) RestockGrangeFromChest(true);
                ShowFurniture();
            }
            else
            {
                Log($"Apparently not a market day", LogLevel.Info);
                HideFurniture();
            }
        }

        public void OnTimeChanged()
        {
            if (PlayerStore)
            {
                if (FarmersMarket.Config.RestockItemsPerHour > 0) RestockGrangeFromChest();
            }
            else
            {
                RestockGrangeFromChest();
            }
        }
        
        public void OnDayEnding()
        {
            if (PlayerStore)
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

                VisitorsToday++;
            }
        }

        internal void OnOneSecondUpdateTicking()
        {
            SellSomethingToOnlookers();
            SeeIfOwnerIsAround();
        }

        internal void OnRenderedWorld(RenderedWorldEventArgs e)
        {
            var tileLocation = new Vector2(X, Y);
            var drawLayer = Math.Max(0f, ((tileLocation.Y + 1) * 64 - 24) / 10000f) + tileLocation.X * 1E-05f;
            drawGrangeItems(tileLocation, e.SpriteBatch, drawLayer);
        }

        internal void OnActionButton(ButtonPressedEventArgs e)
        {
            var tile = e.Cursor.Tile;
            Log($"Button pressed at {tile}", LogLevel.Debug);
            if (tile.X < X || tile.X > X + 3 || tile.Y < Y || tile.Y > Y + 4) return;
            if (PlayerStore)
            {
                Game1.activeClickableMenu = new StorageContainer(GrangeDisplay, 9, 3, onGrangeChange, Utility.highlightSmallObjects);
            }
            else
            {
                ShowShopMenu();
            }
            FarmersMarket.SMod.Helper.Input.Suppress(e.Button);
        }

        internal void ShowShopMenu()
        {
            Game1.activeClickableMenu = new ShopMenu(ShopStock(), 0, Name, OnPurchase)
            {
                portraitPerson = Game1.getCharacterFromName(Name),
                potraitPersonDialogue = Game1.parseText(Description, Game1.dialogueFont, 304)
            };
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

        
        private Dictionary<ISalable, int[]> ShopStock()
        {
            var stock = new Dictionary<ISalable, int[]>();
            
            foreach (var stockItem in GrangeDisplay)
            {
                if (stockItem is null) continue;

                // stock[] has the numeric dataa from te json
                int price;
                int specifiedSellPrice = -2;
                int specifiedQuality = 0;

                
                var stockItemData = Stock[$"{stockItem.ParentSheetIndex}"];

                FarmersMarket.SMonitor.Log(
                    $"Item {stockItem.DisplayName} {stockItemData[0]} {stockItemData[1]}", LogLevel.Debug);

                
                if (stockItemData.Length >= 3)
                {
                    specifiedQuality = Stock[$"{stockItem.ParentSheetIndex}"][2];
                    FarmersMarket.SMonitor.Log(
                        $"... Item {stockItem.DisplayName} " +
                        $"idx {stockItem.ParentSheetIndex} " +
                        $"lookup {Stock[$"{stockItem.ParentSheetIndex}"][2]} " +
                        $"specifiedQuality {specifiedQuality}",
                        LogLevel.Debug);
                }
                else
                {
                    specifiedQuality = 0;
                }

                if (Stock[$"{stockItem.ParentSheetIndex}"].Length >= 2)
                {
                    specifiedSellPrice = Stock[$"{stockItem.ParentSheetIndex}"][1];
                    FarmersMarket.SMonitor.Log(
                        $"... Item {stockItem.DisplayName} " +
                        $"idx {stockItem.ParentSheetIndex} " +
                        $"lookup {Stock[$"{stockItem.ParentSheetIndex}"][1]} " +
                        $"sell price {specifiedSellPrice}",
                        LogLevel.Debug);
                }
                else
                {
                    specifiedSellPrice = -1;
                }

                if (specifiedSellPrice >= 0) {
                    price = specifiedSellPrice;
                } else if (stockItem is SObject o)
                    price = o.sellToStorePrice();
                else
                {
                    price = stockItem.salePrice();
                }

                price = (int)Math.Max(PurchasePriceMultiplier, 0) * price;

                var sellItem = stockItem.getOne();
                sellItem.Stack = 1;
                ((SObject)sellItem).Quality = specifiedQuality;

                stock[sellItem] = new[]
                {
                    (int) (price * SellPriceMultiplier(stockItem, null)),
                    1
                };
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
            };

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
            var npc = location.characters.FirstOrDefault(n => n.Name == Name);
            if (npc is null) return null;
            if (npc.getTileX() == (int) ownerX && npc.getTileY() == (int) ownerY) return npc;
            return null;
        }

        private void SellSomethingToOnlookers()
        {
            foreach (var npc in NearbyNPCs())
            {
                // the owner
                if (npc.Name == Name) continue;
                
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
                if (PlayerStore) AddToPlayerFunds(item, npc);
                GrangeDisplay[i] = null;

                EmoteForPurchase(npc, item);
            }
        }

        private static void EmoteForPurchase(NPC npc, Item item)
        {
            if (Game1.random.NextDouble() < 0.25)
            {
                npc.doEmote(20);
            }
            else
            {
                string dialog;
                if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_love)
                {
                    dialog = Get("love", new {ItemName = item.DisplayName});
                }
                else if (npc.getGiftTasteForThisItem(item) == NPC.gift_taste_like)
                {
                    dialog = Get("like", new {ItemName = item.DisplayName});
                }
                else if (((SObject) item).Quality == SObject.bestQuality)
                {
                    dialog = Get("iridium", new {ItemName = item.DisplayName});
                }
                else if (((SObject) item).Quality == SObject.highQuality)
                {
                    dialog = Get("gold", new {ItemName = item.DisplayName});
                }
                else if (((SObject) item).Quality == SObject.medQuality)
                {
                    dialog = Get("silver", new {ItemName = item.DisplayName});
                }
                else
                {
                    dialog = Get("buy", new {ItemName = item.DisplayName});
                }

                npc.showTextAboveHead(dialog, -1, 2, 1000);
                npc.Sprite.UpdateSourceRect();
            }
        }

        private void AddToPlayerFunds(Item item, NPC npc)
        {
            var mult = SellPriceMultiplier(item, npc);
            var obj = (SObject) item;
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
            ListSales();
        }

        private void ListSales()
        {
            foreach (var sale in Sales)
            {
                var displayMult = Convert.ToInt32(sale.mult * 100);
                Log(
                    sale.npc is not null
                        ? $"{sale.item.DisplayName} sold to {sale.npc?.displayName} for {sale.price}g ({displayMult}%)"
                        : $"{sale.item.DisplayName} sold for {sale.price}g ({displayMult}%)",
                    LogLevel.Debug);
            }
        }
        
        public void ShowSummary() {
            var salesDescriptions = (
                from sale in Sales 
                let displayMult = Convert.ToInt32((sale.mult - 1) * 100) 
                select Get("sales-desc", new {ItemName = sale.item.DisplayName, NpcName = sale.npc.displayName, Price = sale.price, Mult = displayMult})
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
            if (GrangeSign.displayItem.Value is SObject o)
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
            Log($"GetReferencesToFurniture", LogLevel.Debug);

            var location = Game1.getLocationFromName("Town");
            
            
            // the storage chest
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is not Chest chest) continue;
                if (!chest.modData.TryGetValue($"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeStorage",
                    out var owner)) continue;
                if (owner != Name) continue;
                Log($"    StorageChest for {owner} at {tile} claims to be at {chest.TileLocation}", LogLevel.Debug);
                if (tile != chest.TileLocation)
                {
                    chest.TileLocation = tile;
                    Log($"    Moving storage chest, now at {chest.TileLocation}", LogLevel.Debug);

                }
                StorageChest = chest;
            }

            if (StorageChest is null)
            {
                Log($"    Creating new StorageChest at {HiddenChestPosition}", LogLevel.Debug);
                StorageChest = new Chest(true, HiddenChestPosition, 232)
                {
                    modData = {[$"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeStorage"] = Name}
                };
                location.setObject(HiddenChestPosition, StorageChest);
            }

            
            // the grange sign
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is not Sign sign) continue;
                if (!sign.modData.TryGetValue($"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeSign",
                    out var owner))
                    continue;
                if (owner != Name) continue;
                Log($"    GrangeSign for {owner} at {tile} claims to be at {sign.TileLocation}", LogLevel.Debug);
                GrangeSign = sign;
            }

            if (GrangeSign is null)
            {
                Log($"    Creating new GrangeSign at {HiddenSignPosition}", LogLevel.Debug);
                GrangeSign = new Sign(HiddenSignPosition, WOOD_SIGN)
                {
                    modData = {[$"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeSign"] = Name}
                };
                location.objects[HiddenSignPosition] = GrangeSign;
            }

            
            // the results
            
            Log($"    ... StorageChest at {StorageChest.TileLocation}", LogLevel.Debug);
            Log($"    ... GrangeSign at {GrangeSign.TileLocation}", LogLevel.Debug);

            if (StoreColor.R > 0 || StoreColor.G > 0 || StoreColor.B > 0)
            {
                Log($"    StoreColor {StoreColor}", LogLevel.Debug);

                StoreColor.A = 255;
                StorageChest.playerChoiceColor.Value = StoreColor;
            }

            if (SignObjectIndex > 0)
            {
                Log($"    GrangeSign.displayItem.Value to {SignObjectIndex}", LogLevel.Debug);
                GrangeSign.displayItem.Value = new SObject(SignObjectIndex, 1);
                GrangeSign.displayType.Value = 1;
            }
        }

        private void ShowFurniture()
        {
            var location = Game1.getLocationFromName("Town");

            Debug.Assert(StorageChest is not null, "StorageChest is not null");
            Debug.Assert(GrangeSign is not null, "GrangeSign is not null");
            Debug.Assert(GrangeSign.TileLocation.X > 0, "GrangeSign.TileLocation.X assigned");
            Debug.Assert(GrangeSign.TileLocation.Y > 0, "GrangeSign.TileLocation.Y assigned");

            // location.setObject(VisibleChestPosition, StorageChest);
            // location.removeObject(HiddenChestPosition, false);
            location.moveObject(
                (int) StorageChest.TileLocation.X, (int) StorageChest.TileLocation.Y,
                (int) VisibleChestPosition.X, (int) VisibleChestPosition.Y);

            location.moveObject(
                (int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) VisibleSignPosition.X, (int) VisibleSignPosition.Y);

            // location.moveObject((int)StorageChest.TileLocation.X, (int)StorageChest.TileLocation.Y, (int)VisibleChestPosition.X, (int)VisibleChestPosition.Y);
            //
            // location.setObject(VisibleChestPosition, StorageChest);
            // location.setObject(VisibleSignPosition, GrangeSign);
            // // location.objects[VisibleSignPosition] = GrangeSign;
            //
            // location.removeObject(HiddenChestPosition, false);
            // location.removeObject(HiddenSignPosition, false);
        }

        private void HideFurniture()
        {
            var location = Game1.getLocationFromName("Town");
            location.moveObject(
                (int) StorageChest.TileLocation.X, (int) StorageChest.TileLocation.Y,
                (int) HiddenChestPosition.X, (int) HiddenChestPosition.Y);

            location.moveObject(
                (int) GrangeSign.TileLocation.X, (int) GrangeSign.TileLocation.Y,
                (int) HiddenSignPosition.X, (int) HiddenSignPosition.Y);
        }

        private void DestroyFurniture()
        {
            var toRemove = new Dictionary<Vector2, SObject>();

            var location = Game1.getLocationFromName("Town");
            foreach (var (tile, item) in location.Objects.Pairs)
            {
                if (item is Sign sign)
                {
                    if (!sign.modData.TryGetValue($"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeSign",
                        out var owner))
                        continue;
                    if (owner != Name) continue;
                    toRemove[tile] = item;
                }

                if (item is Chest chest)
                {
                    if (!chest.modData.TryGetValue($"{FarmersMarket.SMod.ModManifest.UniqueID}/GrangeStorage",
                        out var owner)) continue;
                    if (owner != Name) continue;
                    toRemove[tile] = item;
                }
            }

            foreach (var (tile, item) in toRemove)
            {
                Log($"    Removing {item.displayName} from {tile}", LogLevel.Debug);
                location.Objects.Remove(tile);
            }

            StorageChest = null;
            GrangeSign = null;
        }

        private void StockChestForTheDay()
        {
            foreach (var (sIdx, stockData) in Stock)
            {
                if (int.TryParse(sIdx, out var idx))
                {
                    var stack = stockData[0];
                    StorageChest.addItem(new SObject(idx, stack));
                }
                else
                {
                    Log($"Could not parse {sIdx}", LogLevel.Warn);
                }
            }
        }

        private void RestockGrangeFromChest(bool fullRestock=false)
        {
            var restockLimitRemaining = FarmersMarket.Config.RestockItemsPerHour;
            
            if (StorageChest.items.Count < 1) return;
            for (var j = 0; j < GrangeDisplay.Count; j++)
            {
                if (restockLimitRemaining <= 0) return;
                if (GrangeDisplay[j] != null) continue;

                var stockItem = StorageChest.items[Game1.random.Next(StorageChest.items.Count)];
                Item;
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

                        Utility.addItemToInventory(old, position, container.ItemsToGrabMenu.actualInventory);
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
        private void drawGrangeItems(Vector2 tileLocation, SpriteBatch spriteBatch, float layerDepth)
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
                if (i != null && i is SObject)
                {
                    if (Event.IsItemMayorShorts(i as SObject))
                    {
                        return -666;
                    }

                    pointsEarned += (i as SObject).Quality + 1;
                    int num = (i as SObject).sellToStorePrice();
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
            FarmersMarket.SMonitor.Log($"[{Name}] {message}", level);
        }

        private string Get(string key)
        {
            return FarmersMarket.SMod.Helper.Translation.Get(key);
        }

        private static string Get(string key, object tokens)
        {
            return FarmersMarket.SMod.Helper.Translation.Get(key, tokens);
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class FarmersMarket : Mod
    {
        internal const double VISIT_CHANCE = 0.9;

        internal const int PLAYER_STORE_X = 23;
        internal const int PLAYER_STORE_Y = 63;
        private static ContentPatcher.IContentPatcherAPI ContentPatcherAPI;
        // private static IManagedConditions MarketDayConditions;
        internal static ModConfig Config;
        internal static IMonitor SMonitor;
        internal static Mod SMod;

        internal static StoresListData StoresData;

        internal static List<Store> Stores = new();
        
        private IGenericModConfigMenuApi configMenu;


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;
            SMod = this;

            Helper.Events.GameLoop.GameLaunched += OnLaunched;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            Helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
            // Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.OneSecondUpdateTicking += OnOneSecondUpdateTicking;
            Helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            Helper.Events.GameLoop.DayEnding += OnDayEnding;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;

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

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaveLoaded(object sender, EventArgs e)
        {
            // reload the config to pick up any changes made in GMCM on the title screen
            Config = Helper.ReadConfig<ModConfig>();
        }

        void OnDayStarted(object sender, EventArgs e)
        {
            SMonitor.Log($"OnDayStarted", LogLevel.Info);

            // var rawConditions = new Dictionary<string, string>
            // {
            //     ["DayOfWeek"] = "Saturday",
            //     ["Weather"] = "Sun, Wind",
            //     ["HasValue:{{DayEvent}}"] = "false"
            // };
            // MarketDayConditions = ContentPatcherAPI.ParseConditions(
            //     ModManifest,
            //     rawConditions,
            //     new SemanticVersion("1.20.0")
            // );

            // we build the stores the night before to stay one step ahead of the route planner
            // BuildStores();
            
            foreach (var store in Stores) store.OnDayStarted(IsMarketDay());
        }

        private static void BuildStores()
        {
            Stores = new List<Store>();
            Utility.Shuffle(Game1.random, StoresData.Stores);
            for (var j = 0; j < StoresData.StoreLocations.Count; j++)
            {
                var storeData = StoresData.Stores[j];
                var (x, y) = StoresData.StoreLocations[j];
                var store = new Store(storeData.NpcName, storeData.Description, false, (int) x, (int) y)
                {
                    StoreColor = storeData.Color,
                    SignObjectIndex = storeData.SignObject,
                    PurchasePriceMultiplier = storeData.PurchasePriceMultiplier,
                    Stock = storeData.Stock
                };

                SMonitor.Log($"Adding a store for {store.Name}", LogLevel.Debug);
                Stores.Add(store);
            }

            Stores.Add(new Store("Player", null, true, PLAYER_STORE_X, PLAYER_STORE_Y));
        }

        void OnTimeChanged(object sender, EventArgs e)
        {
            if (Game1.timeOfDay % 100 > 0) return;
            foreach (var store in Stores) store.OnTimeChanged();
        }

        void OnDayEnding(object sender, EventArgs e)
        {
            foreach (var store in Stores) store.OnDayEnding();

            // get ready for tomorrow
            BuildStores();
        }

        private void DestroyAllFurniture(string command, string[] args)
        {
            SMonitor.Log($"DestroyAllFurniture", LogLevel.Debug);

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
                if (item is not Chest && item is not Sign) continue;
                SMonitor.Log($"    Removing {item.displayName} from {tile}", LogLevel.Debug);
                location.Objects.Remove(tile);
            }

            foreach (var store in Stores)
            {
                store.StorageChest = null;
                store.GrangeSign = null;
            }
        }

        // private void EnsureChestHidden()
        // {
        //     SMonitor.Log($"EnsureChestHidden: I have been instructed to move the furniture", LogLevel.Debug);
        //
        //     var location = Game1.getLocationFromName("Town");
        //     GetReferencesToFurniture();
        //
        //     SMonitor.Log($"EnsureChestHidden: StorageChest from {StorageChest.TileLocation} to {HiddenChestPosition}",
        //         LogLevel.Debug);
        //     location.setObject(HiddenChestPosition, StorageChest);
        //     location.objects.Remove(VisibleChestPosition);
        //     // location.moveObject((int)StorageChest.TileLocation.X, (int)StorageChest.TileLocation.Y, (int)HiddenChestPosition.X, (int)HiddenChestPosition.Y);
        //     StorageChest.modData[$"{ModManifest.UniqueID}/MovedYou"] = "yes";
        //
        //     SMonitor.Log($"EnsureChestHidden: GrangeSign from {GrangeSign.TileLocation} to {HiddenSignPosition}",
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

            foreach (var store in Stores)
            {
                store.OnUpdateTicking();
            }
        }

        private void OnOneSecondUpdateTicking(object sender, OneSecondUpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (!IsMarketDay()) return;

            foreach (var store in Stores)
            {
                store.OnOneSecondUpdateTicking();
            }
        }


        private static void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!IsMarketDay()) return;
            foreach (var store in Stores)
            {
                store.OnRenderedWorld(e);
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

            SMonitor.Log($"OnButtonPressed", LogLevel.Info);
            if (Game1.currentLocation.objects.TryGetValue(new Vector2(x, y), out var objectAt))
            {
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeStorage", out chestOwner);
                objectAt.modData.TryGetValue($"{ModManifest.UniqueID}/GrangeSign", out signOwner);
                // SMonitor.Log($"OnButtonPressed GrabTile {x},{y} Obj: {objectAt.displayName} Chest: {chestOwner} Sign: {signOwner} Tool: {e.Button.IsUseToolButton()} Act: {e.Button.IsActionButton()}",
                //     LogLevel.Debug);
            }
            else
            {
                // SMonitor.Log($"OnButtonPressed GrabTile {x},{y} Tool: {e.Button.IsUseToolButton()} Act: {e.Button.IsActionButton()}",
                //     LogLevel.Debug);
            }

            // if (Game1.currentLocation.objects.TryGetValue(new Vector2(x, y), out var objectAtGrabTile))
            // {
            //     SMonitor.Log($"Fyi GrabTile {x},{y} is Obj: {objectAtGrabTile.displayName}", LogLevel.Debug);
            // }
            // else
            // {
            //     SMonitor.Log($"Fyi GrabTile {x},{y} is nothing", LogLevel.Debug);
            // }

            if (Game1.currentLocation.objects.TryGetValue(new Vector2(tx, ty), out var objectAtTile))
            {
                SMonitor.Log($"Fyi Tile {tx},{ty} is Obj: {objectAtTile.displayName}", LogLevel.Debug);
            }
            else
            {
                SMonitor.Log($"Fyi Tile {tx},{ty} is nothing", LogLevel.Debug);
            }



            if (e.Button.IsUseToolButton() && objectAt is not null)
            {
                if (signOwner is not null) {
                    if (signOwner == "Player")
                    {
                        // clicked on player shop sign, show the summary
                        SMonitor.Log($"Suppress and show summary: clicked on player shop sign, show the summary", LogLevel.Debug);
                        Helper.Input.Suppress(e.Button);
                        Stores.Find(store => store.Name == "Player")?.ShowSummary();
                        return;
                    }
                    
                    // clicked on NPC shop sign, open the store
                    SMonitor.Log($"Suppress and show shop: clicked on NPC shop sign, open the store", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    Stores.Find(store => store.Name == signOwner)?.ShowShopMenu();
                    return;
                }

                // stop player demolishing chests and signs
                if (chestOwner is not null && !Config.RuinTheFurniture)
                {
                    SMonitor.Log($"Suppress and shake: stop player demolishing chests and signs", LogLevel.Debug);
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
                    SMonitor.Log($"Suppress and shake: keep the player out of other people's chests", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    objectAt.shakeTimer = 500;
                    return;
                }

                // keep the player out of other people's signs
                if (signOwner is not null && signOwner != "Player")
                {
                    SMonitor.Log($"Suppress and show shop: keep the player out of other people's signs", LogLevel.Debug);
                    Helper.Input.Suppress(e.Button);
                    Stores.Find(store => store.Name == signOwner)?.ShowShopMenu();
                    return;
                }
            }
            
            if (e.Button.IsActionButton()) {
                // refer clicks on grange tiles to each store
                var tileIndexAt = Game1.currentLocation.getTileIndexAt((int) x, (int) y, "Buildings");
                if (tileIndexAt is < 349 or > 351) return;

                foreach (var store in Stores)
                {
                    store.OnActionButton(e);
                }
            }
        }

        private void OnLaunched(object sender, GameLaunchedEventArgs e)
        {
            Config = Helper.ReadConfig<ModConfig>();

            setupGMCM();

            StoresData = this.Helper.Data.ReadJsonFile<StoresListData>("Assets/stores.json") ?? new StoresListData();

            SMonitor.Log($"NPC stores:", LogLevel.Info);
            foreach (var store in StoresData.Stores)
            {
                SMonitor.Log($"    Store {store.NpcName} symbol {store.SignObject} color {store.Color}",
                    LogLevel.Debug);
            }

            SMonitor.Log($"OnLaunched", LogLevel.Info);
            BuildStores();

            ContentPatcherAPI =
                Helper.ModRegistry.GetApi<ContentPatcher.IContentPatcherAPI>("Pathoschild.ContentPatcher");
            // ContentPatcherAPI.RegisterToken(ModManifest, "FarmersMarketOpen",
            //     () => { return Context.IsWorldReady ? new[] {IsMarketDayJustForToken() ? "true" : "false"} : null; });
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
                   !Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason);
            //
            // if (MarketDayConditions is null || !MarketDayConditions.IsReady)
            // {
            //     SMonitor.Log($"IsMarketDay: MarketDayConditions null", LogLevel.Warn);
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
        //         SMonitor.Log($"IsMarketDayJustForToken: MarketDayConditions null", LogLevel.Warn);
        //         return false;
        //     }
        //
        //     if (!MarketDayConditions.IsReady)
        //     {
        //         SMonitor.Log($"IsMarketDayJustForToken: MarketDayConditions not ready", LogLevel.Warn);
        //         return false;
        //     }
        //
        //     MarketDayConditions.UpdateContext();
        //     SMonitor.Log(
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
        //     SMonitor.Log(
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
        //     SMonitor.Log(
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
        //     SMonitor.Log(
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
        //     SMonitor.Log(
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