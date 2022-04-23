using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace MarketDay
{
    //     public virtual bool performToolAction(Tool t, GameLocation location)
    [HarmonyPatch(typeof(Object))]
    [HarmonyPatch("performToolAction")]
    [HarmonyDebug]
    public class Prefix_performToolAction
    {
        public static bool Prefix(Object __instance, GameLocation location, ref bool __result)
        {
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
            return true;
        }
    }

    [HarmonyPatch(typeof(Chest))]
    [HarmonyPatch("draw")]
    [HarmonyPatch(new Type[] {typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)})]
    [HarmonyDebug]
    public class Postfix_draw
    {
        public static void Postfix(Chest __instance, SpriteBatch spriteBatch, int x, int y)
        {
            // if (!__instance.modData.ContainsKey($"{MarketDay.SMod.ModManifest.UniqueID}/GrangeStorage")) return;
            
            var tileLocation = new Vector2(x-3, y-1);
            if (! MarketDay.ShopAtTile.TryGetValue(tileLocation, out var grangeShop))
            {
                return;
            }
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

            /*

[10:46:48 TRACE SMAPI] Synchronizing 'NewDay' task...
[10:46:49 INFO  Farmers Market] findPathForNPCSchedules: spring 6 600 2213
[10:46:49 TRACE SMAPI]    task complete.
[10:46:49 TRACE SMAPI] Context: before save.
[10:46:51 TRACE SMAPI] Context: after save, starting spring 6 Y1.
[10:46:51 TRACE Content Patcher] [CP] Farmers Market edited Maps/Town.
[10:46:51 TRACE SMAPI] Content Patcher edited Maps/Town.

             */
            /*
             [game] Failed parsing schedule for NPC Willy:
610 Forest 91 40 2 dick_fish/1400 Town 59 100 2 dick_fish/1900 Saloon 17 22 2 "Strings\schedules\Willy:Sat.000"/2300 FishShop 5 4 2
System.Exception: get trace
   at MarketDay.Prefix_findPathForNPCSchedules.Prefix(PathFindController __instance, Point& startPoint, Point endPoint, GameLocation location, Int32 limit, Character ___character, Stack`1& __result) in /Users/mab/Documents/ceruleandeep/CeruleanStardewMods/Market/MarketDay/Patches.cs:line 47
   at StardewValley.PathFindController.findPathForNPCSchedules_PatchedBy<ceruleandeep.MarketDay>(Point startPoint, Point endPoint, GameLocation location, Int32 limit)
   at StardewValley.NPC.pathfindToNextScheduleLocation(String startingLocation, Int32 startingX, Int32 startingY, String endingLocation, Int32 endingX, Int32 endingY, Int32 finalFacingDirection, String endBehavior, String endMessage)
   at StardewValley.NPC.parseMasterSchedule_Patch1(NPC this, String rawData)
   
             */

            MarketDay.monitor.Log(
                $"findPathForNPCSchedules {___character.displayName}, {location.Name} {startPoint} -> {endPoint}",
                LogLevel.Trace);
            
            // var originalPath = OriginalFindPathForNPCSchedules(startPoint, endPoint, location, limit).ToList();
            // var original = string.Join(", ", originalPath);
            // MarketDay.monitor.Log($"    Original path: {original}", LogLevel.Debug);

            // for now, visit every shop
            
            var placesToVisit = new List<Point>();

            // if (MarketDay.MarketData.ShopOwners.TryGetValue(___character.Name, out var OwnedShopName))
            // {
            //     var OwnedShop = MarketDay.ActiveShops().Find(shop => shop.ShopName == OwnedShopName);
            //     if (OwnedShop is not null) placesToVisit.Add(OwnedShop.OwnerPosition().ToPoint());
            // }
            
            foreach (var ShopLocation in MarketDay.MarketData.ShopLocations)
            {
                var visitPoint = new Point((int) ShopLocation.X + Game1.random.Next(3), (int) ShopLocation.Y + 4);
                if (Game1.random.NextDouble() < MarketDay.Config.StallVisitChance)
                {
                    placesToVisit.Add(visitPoint);
                }
            }
            //
            // foreach (var place in MarketDay.ActiveShops())
            // {
            //     var visitPoint = new Point((int) place.X + Game1.random.Next(3), (int) place.Y + 4);
            //     if (place.IsPlayerShop() && Game1.random.NextDouble() < MarketDay.Config.PlayerStallVisitChance)
            //     {
            //         placesToVisit.Add(visitPoint);
            //     }
            //     if (!place.IsPlayerShop() && Game1.random.NextDouble() < MarketDay.Config.NPCStallVisitChance)
            //     {
            //         placesToVisit.Add(visitPoint);
            //     }
            // }
            

            // var openStores = new Dictionary<string, Point>();
            // foreach (var store in MarketDay.Stores)
            // {
            //     openStores[store.Name] = new Point(store.X + 3, store.Y + 2);
            // }
            //
            // if (openStores.Keys.Contains(___character.Name))
            // {
            //     // MarketDay.monitor.Log($"    {___character.Name} has a store", LogLevel.Debug);
            //     placesToVisit.Add(openStores[___character.Name]);
            // }
            // else
            // {
            //     if (Game1.random.NextDouble() < MarketDay.Config.PlayerStallVisitChance + Game1.player.DailyLuck)
            //     {
            //         placesToVisit.Add(new Point(
            //                 MarketDay.PLAYER_STORE_X + Game1.random.Next(3),
            //                 MarketDay.PLAYER_STORE_Y + 4));
            //     }
            //     placesToVisit.AddRange(
            //         from place in MarketDay.ShopAtTile.Keys
            //         where Game1.random.NextDouble() < MarketDay.Config.NPCStallVisitChance
            //         select new Point((int) place.X + Game1.random.Next(3), (int) place.Y + 4));
            // }

            StardewValley.Utility.Shuffle(Game1.random, placesToVisit);
            placesToVisit.Add(startPoint);

            // var waypoints = string.Join(", ", placesToVisit);
            // MarketDay.monitor.Log($"    Waypoints: {waypoints}", LogLevel.Debug);

            // work backwards through the waypoints
            var path = new Stack<Point>();
            var thisEndPoint = endPoint;

            foreach (var waypoint in placesToVisit)
            {
                var thisStartPoint = new Point((int) waypoint.X, (int) waypoint.Y);

                // MarketDay.monitor.Log($"    Segment: {thisStartPoint} -> {thisEndPoint}", LogLevel.Debug);

                var legPath = OriginalFindPathForNPCSchedules(thisStartPoint, thisEndPoint, location, limit).ToList();
                legPath.Reverse();

                var segment = string.Join(", ", legPath);
                // MarketDay.monitor.Log($"    Reversed path: {segment}", LogLevel.Debug);

                foreach (var pt in legPath)
                {
                    path.Push(pt);
                }

                thisEndPoint = thisStartPoint;
            }

            var final = string.Join(", ", path);

            // MarketDay.monitor.Log($"    Final Path   : {final}", LogLevel.Debug);

            __result = path;
            return false;

            // pick a point in front of the store as a waypoint
            // var x1 = (int) sX + Game1.random.Next(3);
            // var y1 = (int) sY + 4;
            //
            // var p1 = OriginalFindPathForNPCSchedules(startPoint, new Point(x1, y1), location, limit);
            // var p2 = OriginalFindPathForNPCSchedules(new Point(x1, y1), endPoint, location, limit);

            // var p1s = string.Join(", ", p1.ToList());
            // var p2s = string.Join(", ", p2.ToList());
            // MarketDay.monitor.Log($"p1 {p1s}", LogLevel.Debug);
            // MarketDay.monitor.Log($"p2 {p2s}", LogLevel.Debug);
            // MarketDay.monitor.Log($"p1 {p1.Count} p2 {p2.Count}", LogLevel.Debug);

            // foreach (var pt in p1r)
            // {
            //     p2.Push(pt);
            // }

            // p2s = string.Join(", ", p2.ToList());
            // MarketDay.monitor.Log($"p2 final {p2s} ", LogLevel.Debug);
            // MarketDay.monitor.Log($"p2 {p2.Count} ", LogLevel.Debug);
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