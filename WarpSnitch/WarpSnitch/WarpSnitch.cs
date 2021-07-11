using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using Harmony;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using StardewValley.Network;

namespace WarpSnitch
{
    /// <summary>The mod entry point.</summary>
    public class WarpSnitch : Mod
    {
        public static IMonitor SMonitor;
        
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;

            HarmonyInstance harmony = HarmonyInstance.Create("ceruleandeep.warpsnitch");
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "loadEndOfRouteBehavior"),
                prefix: new HarmonyMethod(typeof(WarpSnitch), nameof(NPC_loadEndOfRouteBehavior_Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "warpCharacter", parameters: new Type[] { typeof(NPC), typeof(GameLocation), typeof(Vector2) }),
                prefix: new HarmonyMethod(typeof(WarpSnitch), nameof(Game1_warpCharacter_Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "warpCharacter", parameters: new Type[] { typeof(NPC), typeof(string), typeof(Vector2) }),
                prefix: new HarmonyMethod(typeof(WarpSnitch), nameof(Game1_warpCharacter_String_Prefix))
            );
        }

        // An error occurred in the base update loop: System.FormatException: Input string was not in a correct format.
        //     at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
        // at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
        // at StardewValley.Utility.parseStringToIntArray(String s, Char delimiter)
        // at StardewValley.NPC.loadEndOfRouteBehavior(String name)
        // at StardewValley.NPC.getRouteEndBehaviorFunction(String behaviorName, String endMessage)
        // at StardewValley.NPC.checkSchedule(Int32 timeOfDay)
        
        [HarmonyPriority(Priority.High)]
        public static bool NPC_loadEndOfRouteBehavior_Prefix(NPC __instance, string name)
        {
            SMonitor.Log($"loadEndOfRouteBehavior: NPC: {__instance.displayName}, route name {name}");

            if (name is null)
            {
                SMonitor.Log($"loadEndOfRouteBehavior: route name is null [NPC: {__instance.displayName}]", LogLevel.Warn);
                return false;
            }

            Dictionary<string, string> animationDescriptions = Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions");
            if (animationDescriptions.ContainsKey(name))
            {
                SMonitor.Log($"loadEndOfRouteBehavior: animationDescription {animationDescriptions[name]} [NPC: {__instance.displayName}]", LogLevel.Trace);

                string[] rawData = animationDescriptions[name].Split('/');

                SMonitor.Log($"loadEndOfRouteBehavior: rawdata length {rawData.Count()} [NPC: {__instance.displayName}] (needs to be at least 3)", LogLevel.Trace);

                try
                {
                    Utility.parseStringToIntArray(rawData[0]);
                } catch
                {
                    SMonitor.Log($"loadEndOfRouteBehavior: could not parse routeEndIntro [NPC: {__instance.displayName}]", LogLevel.Warn);
                    SMonitor.Log($"the faulty int array is: '{rawData[0]}'", LogLevel.Warn);
                    return false;
                }
                try
                {
                    Utility.parseStringToIntArray(rawData[1]);
                } catch
                {
                    SMonitor.Log($"loadEndOfRouteBehavior: could not parse routeEndAnimation [NPC: {__instance.displayName}]", LogLevel.Warn);
                    SMonitor.Log($"the faulty int array is: '{rawData[1]}'", LogLevel.Warn);
                    return false;
                }
                try
                {
                    Utility.parseStringToIntArray(rawData[2]);
                } catch
                {
                    SMonitor.Log($"loadEndOfRouteBehavior: could not parse routeEndOutro [NPC: {__instance.displayName}]", LogLevel.Warn);
                    SMonitor.Log($"the faulty int array is: '{rawData[2]}'", LogLevel.Warn);
                    return false;
                }
            }
            return true;
        }
        
        [HarmonyPriority(Priority.High)]
        public static bool Game1_warpCharacter_String_Prefix(Game1 __instance, NPC character, string targetLocationName)
        {
            if (character is null)
            {
                SMonitor.Log($"warpCharacter: character is null", LogLevel.Warn);
                return false;
            }

            if (targetLocationName is null)
            {
                SMonitor.Log($"warpCharacter: {character.displayName}'s targetLocationName is null", LogLevel.Warn);
                return false;
            }

            if (Game1.getLocationFromName(targetLocationName) is null)
            {
                SMonitor.Log($"warpCharacter: getLocationFromName returns null for {targetLocationName} [NPC: {character.displayName}]", LogLevel.Warn);
                return false;
            }
            return true;
        }
        
        [HarmonyPriority(Priority.High)]
        public static bool Game1_warpCharacter_Prefix(Game1 __instance, NPC character, GameLocation targetLocation)
        {
            if (character is null)
            {
                SMonitor.Log($"warpCharacter: character is null", LogLevel.Warn);
                return false;
            }

            if (targetLocation is null)
            {
                SMonitor.Log($"warpCharacter: {character.displayName}'s targetLocation is null", LogLevel.Warn);
                return false;
            }
            
            SMonitor.Log($"Warping {character.displayName} to {targetLocation.Name}", LogLevel.Trace);
            return true;
        }
    }
}