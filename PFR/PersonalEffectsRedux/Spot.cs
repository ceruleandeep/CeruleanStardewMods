using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalEffects
{
    public class Spot
    {
        private readonly string NPC;
        private readonly string Location;
        private readonly int X;
        private readonly int Y;
        private readonly int PercentChance;

        private static List<Spot> Spots;
        public static List<ConfigLocation> ConfigLocs;

        private static IModHelper helper;

        private static readonly List<string> Labels = new() {"Panties", "Delicates", "Underpants", "Underwear"};

        private static void Roll(object sender, EventArgs e)
        {
            string undies;
            foreach (var ss in Spots)
            {
                // Modworks.Log.Trace($"Checking spot {ss.Location} for {ss.NPC}");
                //despawn old items
                var l = Game1.getLocationFromName(ss.Location);
                var pos = new Vector2(ss.X, ss.Y);
                if (l.objects.ContainsKey(pos))
                {
                    //if there's one of our own items here, remove it
                    var o1 = l.objects[pos];
                    if (o1 is {displayName: { }})
                    {
                        foreach (var p in Labels.Where(p => o1.displayName.Contains(p)))
                        {
                            if (Mod.Debug) Modworks.Log.Trace("Despawning item from " + ss.Location);
                            l.objects.Remove(pos);
                        }
                    }
                }

                if (ss.NPC == "Kent")
                {
                    if (Game1.year < 2) continue;
                }

                //spawn a new item, if desirable
                if (l.objects.ContainsKey(pos)) continue;
                if (Config.GetNPC(ss.NPC).InternalName == "{Unknown NPC}") continue;
                if (!Config.GetNPC(ss.NPC).Enabled) continue;
                
                var rnd = (int) (Modworks.RNG.Next(100) * (1f - Modworks.Player.GetLuckFactorFloat()));
                var chance = ss.PercentChance;

                if (rnd > chance) continue;
                var npcData = Config.Data[ss.NPC];

                var api = helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
                if (api is null)
                {
                    Modworks.Log.Warn("Could not get JsonAssets API");
                    continue;
                }

                var variant = new Random(DateTime.Now.Millisecond).Next(2);
                var gender = npcData.HasMaleItems() ? "m" : "f";

                if (gender == "m")
                {
                    undies = variant == 1 ? "Underwear" : "Underpants";
                }
                else
                {
                    undies = variant == 1 ? "Panties" : "Delicates";
                }

                var name = $"{ss.NPC}'s {undies}";
                var iid = api.GetObjectId(name);

                if (iid == -1) continue;

                Modworks.Log.Trace($"Spawning forage item {name} for {ss.NPC}: {iid}");

                var i = (StardewValley.Object) StardewValley.Objects.ObjectFactory.getItemFromDescription(0,
                    iid, 1);
                i.IsSpawnedObject = true;
                i.ParentSheetIndex = iid;
                l.objects.Add(pos, i);
                
                // Modworks.Log.Trace($"Spawning forage item for {ss.NPC}: " + sid + " at " + ss.Location + " (" + ss.X + ", " + ss.Y + ")" + "Strike: " + strikepoint + ", Chance: " + chance);
            }
        }

        public static void Setup(IModHelper im_helper)
        {
            helper = im_helper;
            helper.Events.GameLoop.DayStarted += Roll;
            Spots = new List<Spot>();

            foreach (var cl in ConfigLocations.Data)
            {
                var enabled = !(cl.LocationGender == "Female" && !Config.GetNPC(cl.NPC).IsFemale);
                if (cl.LocationGender == "Male" && Config.GetNPC(cl.NPC).IsFemale) enabled = false;
                switch (cl.LocationType)
                {
                    case "Home" when !Config.GetNPC(cl.NPC).HomeSpots:
                    case "Bath" when !Config.GetNPC(cl.NPC).BathSpots:
                    case "Other" when !Config.GetNPC(cl.NPC).OtherSpots:
                        enabled = false;
                        break;
                }

                if (enabled) Spots.Add(new Spot(cl.NPC, cl.Location, cl.X, cl.Y, cl.PercentChance()));
            }
        }

        private Spot(string npc, string loc, int x, int y, int chance)
        {
            NPC = npc;
            Location = loc;
            X = x;
            Y = y;
            PercentChance = chance;
        }
    }
}