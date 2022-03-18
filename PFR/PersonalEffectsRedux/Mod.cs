using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;
using System;
using System.Reflection;

namespace PersonalEffects
{
    public class Mod : StardewModdingAPI.Mod
    {
#if DEBUG
        internal static readonly bool Debug = true;
#else
        internal static readonly bool Debug = false;
#endif

        public override void Entry(IModHelper helper)
        {
            Modworks.Startup(this);
            Monitor.Log("ceruleandeep here! let's have some fun <3 " + Assembly.GetEntryAssembly().GetName().Version + (Debug ? " (DEBUG MODE ACTIVE)" : ""));

            Config.Load(Helper.DirectoryPath + System.IO.Path.DirectorySeparatorChar);
            if (Config.Ready)
            {
                //Modworks.Events.ItemEaten += Events_ItemEaten;
                // helper.Events.Input.ButtonPressed += Input_ButtonPressed;
                helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;

                //AddTrashLoot("Sam", 0);
                //AddTrashLoot("Jodi", 0);
                //AddTrashLoot("Haley", 1);
                //AddTrashLoot("Emily", 1);
                //AddTrashLoot("Lewis", 2);
                //AddTrashLoot("Clint", 4);
                ////saloon is one catch-all
                //AddTrashLoot("Gus", 5);
                //AddTrashLoot("Pam", 5);
                //AddTrashLoot("Emily", 5);
                //AddTrashLoot("Abigail", 5);
                //AddTrashLoot("Sebastian", 5);
                //AddTrashLoot("Marnie", 5);
                ////archeology is the other
                //AddTrashLoot("Maru", 3);
                //AddTrashLoot("Elliott", 3);
                //AddTrashLoot("Harvey", 3);
                //AddTrashLoot("Caroline", 3);
                //AddTrashLoot("Pierre", 3);
                //AddTrashLoot("Shane", 3);

            }

            ConfigLocations.Load(Helper.DirectoryPath + System.IO.Path.DirectorySeparatorChar);
            if (ConfigLocations.Ready)
            {
                Spot.Setup(helper);
            }

        }

        //internal void AddTrashLoot(string NPC, int can)
        //{
        //    if (Config.GetNPC(NPC) == null) return;
        //    if (!Config.GetNPC(NPC).Enabled) return;
        //    Modworks.Items.AddTrashLoot(Module, new bwdyworks.Structures.TrashLootEntry(Module, "px" + Config.GetNPC(NPC).Abbreviate() + (Config.GetNPC(NPC).HasMaleItems() ? "m" : "f") + 1, can));
        //    Modworks.Items.AddTrashLoot(Module, new bwdyworks.Structures.TrashLootEntry(Module, "px" + Config.GetNPC(NPC).Abbreviate() + (Config.GetNPC(NPC).HasMaleItems() ? "m" : "f") + 2, can));
        //}

        private static int tickUpdateLimiter;
        private static bool EatingPrimed;
        private static Item EatingItem;
        private static int eatingQuantity;
        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            tickUpdateLimiter++;
            if (tickUpdateLimiter < 10) return;
            tickUpdateLimiter = 0;
            if (Game1.player == null) return;
            if (EatingPrimed)
            {
                if (Game1.player.isEating) return;
                
                EatingPrimed = false;
                //make sure we didn't say no
                if (Game1.player.ActiveObject == null || Game1.player.ActiveObject.Stack < eatingQuantity)
                {
                    Events_ItemEaten(EatingItem);
                }
                EatingItem = null;
            }
            else if (Game1.player.isEating)
            {
                EatingPrimed = true;
                if (Game1.player.itemToEat == null) return;
                EatingItem = Game1.player.itemToEat;
                if (Game1.player.ActiveObject == null) return;
                eatingQuantity = Game1.player.ActiveObject.Stack;
            }
        }

        private void Events_ItemEaten(Item item)
        {
            string dn = item.DisplayName;
            string npc;

            if (dn.EndsWith("'s Panties")) npc = dn[..dn.IndexOf("'s Panties")];
            else if (dn.EndsWith("'s Underwear")) npc = dn[..dn.IndexOf("'s Underwear")];
            else if (dn.EndsWith("'s Delicates")) npc = dn[..dn.IndexOf("'s Delicates")];
            else if (dn.EndsWith("'s Underpants")) npc = dn[..dn.IndexOf("'s Underpants")];
            else return; //not one of ours

            if (string.IsNullOrWhiteSpace(npc)) return;

            var npcName = Config.LookupNPC(npc);
            if (npcName == null) return;

            var npcConfig = Config.GetNPC(npcName);
            int friendship = Modworks.Player.GetFriendshipPoints(npcName);

            if (friendship == 0) return; //don't reveal identities of unknown NPCs
            Game1.showGlobalMessage("You feel like this changes your relationship with " + npcConfig.DisplayName + ".");

            if (Modworks.RNG.NextDouble() < 0.25f + (Modworks.Player.GetLuckFactorFloat() * 0.75f))
            {
                Modworks.Player.SetFriendshipPoints(npcName, Math.Min(2500, friendship + Modworks.RNG.Next(250)));
                Modworks.Log.Trace("Relationship increased");
            }
            else
            {
                Modworks.Player.SetFriendshipPoints(npcName, Math.Max(friendship - Modworks.RNG.Next(100), 0));
                Modworks.Log.Trace("Relationship decreased");
            }
        }
    }
}