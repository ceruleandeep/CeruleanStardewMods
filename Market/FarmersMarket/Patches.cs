using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using HarmonyLib;
using StardewModdingAPI;

namespace FarmersMarket
{
    [HarmonyPatch(typeof(PathFindController))]
    [HarmonyPatch("findPathForNPCSchedules")]
    public class Prefix_findPathForNPCSchedules
    {
        public static bool Prefix(PathFindController __instance, ref Point startPoint, Point endPoint,
            GameLocation location, int limit, ref Stack<Point> __result)
        {
            if (location.Name != "Town") return true;
            if (!FarmersMarket.IsMarketDay()) return true;

            var placesToVisit = new List<Point>();

            if (Game1.random.NextDouble() < FarmersMarket.VISIT_CHANCE + Game1.player.DailyLuck)
            {
                placesToVisit.Add(new Point(FarmersMarket.PLAYER_STORE_X + Game1.random.Next(3),
                    FarmersMarket.PLAYER_STORE_Y + 4));
            }

            foreach (var place in FarmersMarket.StoresData.StoreLocations)
            {
                if (Game1.random.NextDouble() < FarmersMarket.VISIT_CHANCE + Game1.player.DailyLuck)
                {
                    placesToVisit.Add(new Point((int) place.X + Game1.random.Next(3), (int) place.Y + 4));
                }
            }

            FarmersMarket.SMonitor.Log($"findPathForNPCSchedules {location.Name} {startPoint} -> {endPoint}",
                LogLevel.Warn);

            var originalPath = OriginalFindPathForNPCSchedules(startPoint, endPoint, location, limit).ToList();
            var original = string.Join(", ", originalPath);
            FarmersMarket.SMonitor.Log($"    Original path: {original}", LogLevel.Debug);

            Utility.Shuffle(Game1.random, placesToVisit);
            placesToVisit.Add(startPoint);

            var waypoints = string.Join(", ", placesToVisit);
            FarmersMarket.SMonitor.Log($"    Waypoints: {waypoints}", LogLevel.Debug);

            // work backwards through the waypoints
            var path = new Stack<Point>();
            var thisEndPoint = endPoint;

            foreach (var waypoint in placesToVisit)
            {
                var thisStartPoint = new Point((int) waypoint.X, (int) waypoint.Y);

                FarmersMarket.SMonitor.Log($"    Segment: {thisStartPoint} -> {thisEndPoint}", LogLevel.Debug);

                var legPath = OriginalFindPathForNPCSchedules(thisStartPoint, thisEndPoint, location, limit).ToList();
                legPath.Reverse();

                var segment = string.Join(", ", legPath);
                // FarmersMarket.SMonitor.Log($"    Reversed path: {segment}", LogLevel.Debug);

                foreach (var pt in legPath)
                {
                    path.Push(pt);
                }

                thisEndPoint = thisStartPoint;
            }

            var final = string.Join(", ", path);

            FarmersMarket.SMonitor.Log($"    Final Path   : {final}", LogLevel.Debug);

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
            // FarmersMarket.SMonitor.Log($"p1 {p1s}", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p2 {p2s}", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p1 {p1.Count} p2 {p2.Count}", LogLevel.Debug);

            // foreach (var pt in p1r)
            // {
            //     p2.Push(pt);
            // }

            // p2s = string.Join(", ", p2.ToList());
            // FarmersMarket.SMonitor.Log($"p2 final {p2s} ", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p2 {p2.Count} ", LogLevel.Debug);
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