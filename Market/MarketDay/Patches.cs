using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using HarmonyLib;
using MarketDay.Shop;
using MarketDay.Utility;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace MarketDay
{
    //     public override bool checkForAction(Farmer who, bool justCheckingForActivity = false)
    [HarmonyPatch(typeof(Chest))]
    [HarmonyPatch("checkForAction")]
    public class Prefix_Chest_checkForAction
    {
        public static bool Prefix(Chest __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity) return true;
            var owner = MapUtility.Owner(__instance);
            MarketDay.monitor.Log(
                $"Prefix_Chest_checkForAction checking {__instance} {__instance.DisplayName} owner {owner} at {__instance.TileLocation}",
                LogLevel.Debug);
            
            if (owner is null) return true;
            if (owner == "Player" || MarketDay.Config.PeekIntoChests) return true;

            MarketDay.monitor.Log(
                $"Prefix_Chest_checkForAction preventing action on object at {__instance.TileLocation} owned by {owner}",
                LogLevel.Debug);
            
            who.currentLocation.playSound("clank");
            __instance.shakeTimer = 500;
            __result = false;
            return false;

        }
    }
    
    //     public override bool checkForAction(Farmer who, bool justCheckingForActivity = false)
    [HarmonyPatch(typeof(Sign))]
    [HarmonyPatch("checkForAction")]
    public class Prefix_Sign_checkForAction
    {
        public static bool Prefix(Sign __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity) return true;
            var owner = MapUtility.Owner(__instance);
            MarketDay.monitor.Log(
                $"Prefix_Sign_checkForAction checking {__instance} {__instance.DisplayName} owner {owner} at {__instance.TileLocation}",
                LogLevel.Debug);

            if (owner is null or "Player") return true;
            
            MarketDay.monitor.Log(
                $"Prefix_Sign_checkForAction preventing action on object at {__instance.TileLocation} owned by {owner}",
                LogLevel.Debug);
            
            who.currentLocation.playSound("clank");
            __instance.shakeTimer = 500;
            __result = false;
            return false;
        }
    }

    // public virtual bool performUseAction(GameLocation location)
    // does not trip for Signs
    [HarmonyPatch(typeof(Object))]
    [HarmonyPatch("performUseAction")]
    public class Prefix_Object_performUseAction
    {
        public static bool Prefix(Object __instance, GameLocation location, ref bool __result)
        {
            var owner = MapUtility.Owner(__instance);
            MarketDay.monitor.Log(
                $"Prefix_Object_performUseAction checking {__instance} {__instance.DisplayName} owner {owner} at {__instance.TileLocation}",
                LogLevel.Debug);
            
            if (owner is null) return true;
            if (owner == "Player" || MarketDay.Config.PeekIntoChests) return true;

            MarketDay.monitor.Log(
                $"Prefix_Object_performUseAction preventing use of object at {__instance.TileLocation} owned by {owner}",
                LogLevel.Debug);
            
            location.playSound("clank");
            __instance.shakeTimer = 500;
            __result = false;
            return false;
        }
    }

    //     public virtual bool performToolAction(Tool t, GameLocation location)
    //    this one works leave it alone
    [HarmonyPatch(typeof(Object))]
    [HarmonyPatch("performToolAction")]
    public class Prefix_Object_performToolAction
    {
        public static bool Prefix(Object __instance, GameLocation location, ref bool __result)
        {
            var owner = MapUtility.Owner(__instance);
            MarketDay.monitor.Log(
                $"Prefix_Object_performToolAction checking {__instance} {__instance.DisplayName} owner {owner} at {__instance.TileLocation}",
                LogLevel.Debug);

            if (MarketDay.Config.RuinTheFurniture) return true;
            if (owner is null) return true;

            MarketDay.monitor.Log(
                $"Prefix_Object_performToolAction preventing damage to object at {__instance.TileLocation} owned by {owner}",
                LogLevel.Debug);
            location.playSound("clank");
            __instance.shakeTimer = 100;
            __result = false;
            return false;
        }
    }


    [HarmonyPatch(typeof(Chest))]
    [HarmonyPatch("draw")]
    [HarmonyPatch(new Type[] {typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)})]
    public class Postfix_draw
    {
        public static void Postfix(Chest __instance, SpriteBatch spriteBatch, int x, int y)
        {
            if (!__instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/{GrangeShop.StockChestKey}",
                out var shopName)) return;
            
            // get shop for shopName
            if (!ShopManager.GrangeShops.TryGetValue(shopName, out var grangeShop))
            {
                MarketDay.monitor.Log(
                    $"Postfix_draw: shop '{shopName}' not found in ShopManager.GrangeShops, can't draw",
                    LogLevel.Error);
                return;
            }

            var tileLocation = grangeShop.Origin;
            var drawLayer = Math.Max(0f, ((tileLocation.Y + 1) * 64 - 24) / 10000f) + tileLocation.X * 1E-05f;
            grangeShop.drawGrangeItems(tileLocation, spriteBatch, drawLayer);
        }
    }

    [HarmonyPatch(typeof(PathFindController))]
    [HarmonyPatch("findPathForNPCSchedules")]
    public class Prefix_findPathForNPCSchedules
    {
        public static bool Prefix(PathFindController __instance, ref Point startPoint, Point endPoint,
            GameLocation location, int limit, Character ___character, ref Stack<Point> __result)
        {
            if (location.Name != "Town") return true;
            if (!MarketDay.IsMarketDay()) return true;
            if (!MarketDay.Config.NPCVisitors) return true;

            if (MapUtility.ShopTiles() is null)
            {
                MarketDay.monitor.Log($"findPathForNPCSchedules: MarketDay.ShopLocations is null", LogLevel.Debug);
                return true;
            }
            
            if (MapUtility.ShopTiles().Count == 0)
            {
                MarketDay.monitor.Log($"findPathForNPCSchedules: MarketDay.ShopLocations.Count {MapUtility.ShopTiles().Count}", LogLevel.Debug);
                return true;
            }

            MarketDay.monitor.Log(
                $"findPathForNPCSchedules {___character.displayName}, {location.Name} {startPoint} -> {endPoint}",
                LogLevel.Trace);

            var placesToVisit = new List<Point>();

            foreach (var (shopX, shopY) in MapUtility.ShopTiles())
            {
                var visitPoint = new Point((int) shopX + Game1.random.Next(3), (int) shopY + 4);
                if (Game1.random.NextDouble() < MarketDay.Config.StallVisitChance) placesToVisit.Add(visitPoint);
            }

            StardewValley.Utility.Shuffle(Game1.random, placesToVisit);
            placesToVisit.Add(startPoint);
            
            if (placesToVisit.Count < 2)
            {
                MarketDay.monitor.Log($"findPathForNPCSchedules: placesToVisit.Count {placesToVisit.Count}", LogLevel.Debug);
                return true;
            }

            var waypoints = string.Join(", ", placesToVisit);
            MarketDay.monitor.Log($"    Waypoints: {waypoints}", LogLevel.Debug);

            // work backwards through the waypoints
            var path = new Stack<Point>();
            var thisEndPoint = endPoint;

            foreach (var (wptX, wptY) in placesToVisit)
            {
                var thisStartPoint = new Point(wptX, wptY);

                MarketDay.monitor.Log($"    Segment: {thisStartPoint} -> {thisEndPoint}", LogLevel.Debug);

                var originalPath = OriginalFindPathForNPCSchedules(thisStartPoint, thisEndPoint, location, limit);
                if (originalPath is null || originalPath.Count == 0) continue;
                
                var legPath = originalPath.ToList();
                legPath.Reverse();

                var segment = string.Join(", ", legPath);
                MarketDay.monitor.Log($"    Reversed path: {segment}", LogLevel.Debug);

                foreach (var pt in legPath) path.Push(pt);

                thisEndPoint = thisStartPoint;
            }

            var final = string.Join(", ", path);
            MarketDay.monitor.Log($"    Final Path   : {final}", LogLevel.Debug);

            __result = path;
            return false;
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathFindController), "findPathForNPCSchedules")]
        private static Stack<Point> OriginalFindPathForNPCSchedules(Point startPoint, Point endPoint,
            GameLocation location, int limit)
        {
            // its a stub so it has no initial content
            throw new NotImplementedException("It's a stub");
        }
    }
}