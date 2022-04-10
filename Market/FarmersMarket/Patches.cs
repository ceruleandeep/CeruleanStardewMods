using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.Characters;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.BellsAndWhistles;

namespace FarmersMarket
{
    [HarmonyPatch(typeof(PathFindController))]
    [HarmonyPatch("findPathForNPCSchedules")]
    public class Prefix_findPathForNPCSchedules
    {
        public static bool Prefix(PathFindController __instance, ref Point startPoint, Point endPoint, GameLocation location, int limit, ref Stack<Point> __result)
        {
            if (location.Name != "Town") return true;
            
            if (Game1.random.NextDouble() < FarmersMarket.VISIT_CHANCE + Game1.player.DailyLuck) return true;
            
            // FarmersMarket.SMonitor.Log($"findPathForNPCSchedules {location.Name} {startPoint} -> {endPoint}", LogLevel.Debug);

            // pick a point in front of the store as a waypoint
            var x = FarmersMarket.STORE_X + Game1.random.Next(3);
            var y = FarmersMarket.STORE_Y + 4;
            var p1 = OriginalFindPathForNPCSchedules(startPoint, new Point(x, y), location, limit);
            var p2 = OriginalFindPathForNPCSchedules(new Point(x, y), endPoint, location, limit);

            // var p1s = string.Join(", ", p1.ToList());
            // var p2s = string.Join(", ", p2.ToList());
            // FarmersMarket.SMonitor.Log($"p1 {p1s}", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p2 {p2s}", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p1 {p1.Count} p2 {p2.Count}", LogLevel.Debug);

            var p1r = p1.ToList();
            p1r.Reverse();
            foreach (var pt in p1r)
            {
                p2.Push(pt);
            }
                
            // p2s = string.Join(", ", p2.ToList());
            // FarmersMarket.SMonitor.Log($"p2 final {p2s} ", LogLevel.Debug);
            // FarmersMarket.SMonitor.Log($"p2 {p2.Count} ", LogLevel.Debug);

            __result = p2;
            return false;
        }
        
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathFindController), "findPathForNPCSchedules")]
        public static Stack<Point> OriginalFindPathForNPCSchedules(Point startPoint, Point endPoint, GameLocation location, int limit)
        {
            // its a stub so it has no initial content
            throw new NotImplementedException("It's a stub");
        }
    }
}