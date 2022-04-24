using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using HarmonyLib;
using MarketDay.Shop;
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
            MarketDay.monitor.Log(
                $"Prefix_Chest_checkForAction {__instance} {__instance.DisplayName} {__instance.TileLocation}",
                LogLevel.Debug);

            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage", out var chestOwner);
            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign", out var signOwner);

            MarketDay.monitor.Log(
                $"Prefix_Chest_checkForAction checking use access to {__instance} {__instance.DisplayName} at {__instance.TileLocation} [PeekIntoChests={MarketDay.Config.PeekIntoChests}]",
                LogLevel.Debug);

            MarketDay.monitor.Log(
                $"{MarketDay.SMod.ModManifest.UniqueID} {chestOwner} {signOwner}",
                LogLevel.Debug);

            //if (MarketDay.Config.PeekIntoChests) return true;
            if (signOwner is null && chestOwner is null) return true;

            if (chestOwner == "Player" || MarketDay.Config.PeekIntoChests) return true;
            MarketDay.monitor.Log($"Suppress and shake: stop player opening chests", LogLevel.Debug);
            who.currentLocation.playSound("hammer");
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
            MarketDay.monitor.Log(
                $"Prefix_Sign_checkForAction {__instance} {__instance.DisplayName} {__instance.TileLocation}",
                LogLevel.Debug);

            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage", out var chestOwner);
            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign", out var signOwner);

            MarketDay.monitor.Log(
                $"Prefix_Sign_checkForAction checking use access to {__instance} {__instance.DisplayName} at {__instance.TileLocation} [PeekIntoChests={MarketDay.Config.PeekIntoChests}]",
                LogLevel.Debug);

            MarketDay.monitor.Log(
                $"{MarketDay.SMod.ModManifest.UniqueID} {chestOwner} {signOwner}",
                LogLevel.Debug);

            if (signOwner is null || signOwner == "Player") return true;
            MarketDay.monitor.Log($"Suppress and shake: stop player opening signs", LogLevel.Debug);
            who.currentLocation.playSound("hammer");
            __instance.shakeTimer = 500;
            __result = false;
            return false;
        }
    }

    // public virtual bool performUseAction(GameLocation location)
    // does not trip for Signs
    [HarmonyPatch(typeof(Object))]
    [HarmonyPatch("performUseAction")]
    public class Prefix_performUseAction
    {
        public static bool Prefix(Object __instance, GameLocation location, ref bool __result)
        {
            MarketDay.monitor.Log(
                $"Prefix_performUseAction checking use access to {__instance} {__instance.DisplayName} at {__instance.TileLocation}",
                LogLevel.Debug);

            if (__instance is Sign sign)
            {
                if (sign.modData.ContainsKey($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign"))
                {
                    MarketDay.monitor.Log(
                        $"Prefix_performToolAction preventing damage to sign at {__instance.TileLocation}",
                        LogLevel.Debug);
                    location.playSound("hammer");
                    __instance.shakeTimer = 100;
                    __result = false;
                    return false;
                }
            }

            if (__instance is Chest chest)
            {
                if (chest.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeChest", out var chestOwner))
                {
                    MarketDay.monitor.Log(
                        $"Prefix_performUseAction checking access to chest {chestOwner} at {__instance.TileLocation}",
                        LogLevel.Debug);
                    if (chestOwner is not null && chestOwner != "Player" && !MarketDay.Config.PeekIntoChests)
                    {
                        MarketDay.monitor.Log($"Suppress and shake: stop player opening chests", LogLevel.Debug);
                        location.playSound("hammer");
                        __instance.shakeTimer = 500;
                        __result = false;
                        return false;
                    }
                }
            }

            return true;
        }
    }

    //     public virtual bool performToolAction(Tool t, GameLocation location)
    //    this one works leave it alone
    [HarmonyPatch(typeof(Object))]
    [HarmonyPatch("performToolAction")]
    public class Prefix_performToolAction
    {
        public static bool Prefix(Object __instance, GameLocation location, ref bool __result)
        {
            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage", out var chestOwner);
            __instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeSign", out var signOwner);

            MarketDay.monitor.Log(
                $"Prefix_performToolAction checking tool access to {__instance} {__instance.DisplayName} at {__instance.TileLocation} [RuinTheFurniture={MarketDay.Config.RuinTheFurniture}]",
                LogLevel.Debug);

            MarketDay.monitor.Log(
                $"{MarketDay.SMod.ModManifest.UniqueID} {chestOwner} {signOwner}",
                LogLevel.Debug);

            if (MarketDay.Config.RuinTheFurniture) return true;
            if (signOwner is null && chestOwner is null) return true;

            MarketDay.monitor.Log(
                $"Prefix_performToolAction preventing damage to object at {__instance.TileLocation}",
                LogLevel.Debug);
            location.playSound("hammer");
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
            if (!__instance.modData.TryGetValue($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage",
                out var shopName)) return;

            // get shop for shopName
            if (!ShopManager.GrangeShops.TryGetValue(shopName, out var grangeShop))
            {
                MarketDay.monitor.Log(
                    $"Postfix_draw: shop '{shopName}' not found in ShopManager.GrangeShops, can't draw",
                    LogLevel.Error);
                return;
            }

            var tileLocation = new Vector2(grangeShop.X, grangeShop.Y);
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

            /*

[10:46:48 TRACE SMAPI] Synchronizing 'NewDay' task...
[10:46:49 INFO  Farmers Market] findPathForNPCSchedules: spring 6 600 2213
[10:46:49 TRACE SMAPI]    task complete.
[10:46:49 TRACE SMAPI] Context: before save.
[10:46:51 TRACE SMAPI] Context: after save, starting spring 6 Y1.
[10:46:51 TRACE Content Patcher] [CP] Farmers Market edited Maps/Town.
[10:46:51 TRACE SMAPI] Content Patcher edited Maps/Town.

             */

            MarketDay.monitor.Log(
                $"findPathForNPCSchedules {___character.displayName}, {location.Name} {startPoint} -> {endPoint}",
                LogLevel.Trace);

            var placesToVisit = new List<Point>();

            foreach (var (shopX, shopY) in MarketDay.ShopLocations)
            {
                var visitPoint = new Point((int) shopX + Game1.random.Next(3), (int) shopY + 4);
                if (Game1.random.NextDouble() < MarketDay.Config.StallVisitChance) placesToVisit.Add(visitPoint);
            }

            StardewValley.Utility.Shuffle(Game1.random, placesToVisit);
            placesToVisit.Add(startPoint);

            // var waypoints = string.Join(", ", placesToVisit);
            // MarketDay.monitor.Log($"    Waypoints: {waypoints}", LogLevel.Debug);

            // work backwards through the waypoints
            var path = new Stack<Point>();
            var thisEndPoint = endPoint;

            foreach (var (wptX, wptY) in placesToVisit)
            {
                var thisStartPoint = new Point(wptX, wptY);

                // MarketDay.monitor.Log($"    Segment: {thisStartPoint} -> {thisEndPoint}", LogLevel.Debug);

                var originalPath = OriginalFindPathForNPCSchedules(thisStartPoint, thisEndPoint, location, limit);
                if (originalPath is null || originalPath.Count == 0) continue;
                
                var legPath = originalPath.ToList();
                legPath.Reverse();

                var segment = string.Join(", ", legPath);
                // MarketDay.monitor.Log($"    Reversed path: {segment}", LogLevel.Debug);

                foreach (var pt in legPath) path.Push(pt);

                thisEndPoint = thisStartPoint;
            }

            var final = string.Join(", ", path);
            // MarketDay.monitor.Log($"    Final Path   : {final}", LogLevel.Debug);

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